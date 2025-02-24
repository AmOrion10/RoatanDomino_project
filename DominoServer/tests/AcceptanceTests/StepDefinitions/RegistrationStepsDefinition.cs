using DominoCommon.com.domino.common.Scripts.ExtensionMethods;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "Registration")]
public class RegistrationStepsDefinition : BaseStepsDefinition
{
    private string? userEmail;

    public RegistrationStepsDefinition(ScenarioContext context) : base(context)
    {
        RegisterHandlers();
    }

    [Given(@"Un client déjà inscrit avec l'email ""([^""]*)""")]
    public async Task GivenUnClientDejaInscritAvecLemail(string email)
    {
        await DbContext.Users.AddAsync(new UserEntity
        {
            Email = email,
            Login = "login",
            Password = "password8"
        });
        await DbContext.SaveChangesAsync();
    }

    [Given(@"Un client déjà inscrit avec le login ""([^""]*)""")]
    public async Task GivenUnClientDejaInscritAvecLeLogin(string login)
    {
        await DbContext.Users.AddAsync(new UserEntity
        {
            Email = "user@user.fr",
            Login = login,
            Password = "password8"
        });
        await DbContext.SaveChangesAsync();
    }

    [When(@"Le client (.*) demande à s inscrire avec l email ""([^""]*)"", le login ""([^""]*)"" et le mdp ""([^""]*)""")]
    public async Task WhenLeClientDemandeAsInscrireAvecLEmailLeLoginEtLeMdp(int clientId, string email, string login, string password)
    {
        userEmail = email;
        RegistrationDto dto = new()
        {
            Email = email,
            Login = login,
            Password = password,
            Birthdate = "21/04/2002"
        };
        await SendRequest(MessageFrameBuilder.RegistrationFrame(dto), GetClientWithId(clientId));
    }

    [Then("Le client est inscrit")]
    public async Task ThenLeClientEstInscrit()
    {
        Check.That(await DbContext.Users.AnyAsync(u => u.Email.Equals(userEmail))).IsTrue();
    }
}
