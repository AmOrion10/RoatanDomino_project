# Exemple scénario

Exemple de développement du scenario suivant : 
`Un utilisateur souhaite s'inscrire`

Cet exemple est voué à changer mais c'est pour comprendre les interactions entre les différentes couches de la solution DominoServer.
Il y a un [test d'acceptance](../DominoServer/tests/AcceptanceTests/Features/Registration.feature) en relation avec cette fonctionnalité.

Cette fonctionnalité sera appelée lorsque le serveur reçoit une certaine trame d'un client qui demande à s'inscrire.

## Command
[Fichier associé](../DominoServer/src/Application/Commands/CreateUserCommand.cs)

```c#
public class CreateUserCommand : ICommand
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string Login { get; set; }
}
```
Cette classe hérite de `ICommand` et ne sert juste qu'à passer des informations (aka c'est un Data Transfert Object abrégé DTO)

## CommandHandler
```c#
public class CreateUserCommandHandler(IUserRepository userRepository) : ICommandHandler<CreateUserCommand, Unit>
{
    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await userRepository.ExistsByEmail(request.Email))
        {
            throw new UserAlreadyExistException("User with this email already exists");
        }

        User user = new()
        {
            Email = request.Email,
            Login = request.Login
        };

        await userRepository.Create(user, request.Password);

        return Unit.Value;
    }
}
```
Cette classe prend en charge l'exécution de la commande asociée. Elle hérite donc de `ICommandHandler<CreateUserCommand, Unit>` le premier paramètre dans les chevrons est le type de commande associée, le deuxième est le retour de la commande (ici on ne veut rien retourner).

Cette classe utilise (via l'injection de dépendance) un service du type Repository ([explications](https://deviq.com/design-patterns/repository-pattern#generic-repository-interface)) qui permet de faire les opérations CRUD sur la BDD.

## Repository
[Interface](../DominoServer/src/Application/Interfaces/Repositories/IUserRepository.cs) et [Implémentation](../DominoServer/src/Infrastructure/Services/Repositories/UserRepository.cs).

⚠ ATTENTION ⚠ : L'interface est dans la couche application et l'implémentation est dans la couche infrastructure.
```c#
// Dans la couche application
public interface IUserRepository
{
    Task<User> Create(User user, string password);
    Task<bool> ExistsByEmail(string email);
}
```

```c#
// Dans la couche infrastructure
public class UserRepository(ApplicationDbContext dbContext) : IUserRepository
{
    public async Task<User> Create(User user, string password)
    {
        UserEntity entity = new()
        {
            Email = user.Email,
            Login = user.Login,
            Password = password // TODO: hash
        };
        await dbContext.Users.AddAsync(entity);
        await dbContext.SaveChangesAsync();
        return entity.ToDomainEntity();
    }

    public async Task<bool> ExistsByEmail(string email)
    {
        return await dbContext.Users.AnyAsync(u => u.Email.Equals(email));
    }
}
```
Dans la classe `UserRepository`, on a donc accès via l'injection de dépendance à la classe `ApplicationDbContext` qui est la classe qui permet de faire des opérations CRUD sur toutes les tables de la base de données.
