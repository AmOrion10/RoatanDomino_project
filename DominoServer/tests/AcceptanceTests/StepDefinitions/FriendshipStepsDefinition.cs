using CommonTests.Fixtures;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "Friendship")]
public class FriendshipStepsDefinition : BaseStepsDefinition
{
    protected FriendshipStepsDefinition(ScenarioContext context) : base(context)
    {
        RegisterHandlers();
    }

    [Given(@"Il n'existe pas un utilisateur nommé ""([^""]*)""")]
    public async Task GivenIlNexistePasUnUtilisateurNomme(string login)
    {
        Check.That(await DbContext.Users.AnyAsync(u => u.Login.Equals(login))).IsFalse();
    }

    [Given(@"Le client (.*) n a pas de demande d ami de ""([^""]*)""")]
    public async Task GivenLeClientNaPasDeDemandeDAmiDe(int clientId, string login)
    {
        long userId = await GetUserIdByLogin(login);
        ClientMock client = GetClientWithId(clientId);
        Check.That(await DbContext.FriendRequests.AnyAsync(f => f.UserIdApplicant == userId && f.UserIdAsked == client.User!.Id)).IsFalse();
    }

    [Given("Le client (.*) n a aucune demande d ami")]
    public async Task GivenLeClientNaAucuneDemandeDAmi(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        Check.That(await DbContext.FriendRequests.AnyAsync(f => f.UserIdAsked == client.User!.Id)).IsFalse();
    }

    [Given(@"Le client (.*) a une demande d'ami de ""([^""]*)""")]
    public void GivenLeClientAUneDemandeDamiDe(int clientId, string login)
    {
        ClientMock client = GetClientWithId(clientId);
        DbContext.FriendRequests.Add(new FriendRequestEntity
        {
            UserIdAsked = client.User!.Id,
            UserIdApplicant = DbContext.Users.First(u => u.Login.Equals(login)).Id
        });
        DbContext.SaveChanges();
    }

    [Given(@"Le client (.*) est déjà ami avec l'utilisateur nommé ""([^""]*)""")]
    public async Task GivenLeClientEstDejaAmiAvecLutilisateurNomme(int clientId, string login)
    {
        ClientMock client = GetClientWithId(clientId);
        long userId = await GetUserIdByLogin(login);
        await DbContext.Friendships.AddAsync(new FriendshipEntity
        {
            UserId = client.User!.Id,
            FriendId = userId
        });
        await DbContext.Friendships.AddAsync(new FriendshipEntity
        {
            UserId = userId,
            FriendId = client.User!.Id
        });
        await DbContext.SaveChangesAsync();
    }

    [Given(@"Le client (.*) n est pas ami avec l utilisateur ""([^""]*)""")]
    public async Task GivenLeClientNEstPasAmiAvecLUtilisateur(int clientId, string login)
    {
        long userId = await GetUserIdByLogin(login);
        ClientMock client = GetClientWithId(clientId);
        Check.That(await DbContext.Friendships.AnyAsync(f => f.UserId == userId && f.FriendId == client.User!.Id)).IsFalse();
        Check.That(await DbContext.Friendships.AnyAsync(f => f.UserId == client.User!.Id && f.FriendId == userId)).IsFalse();
    }

    [Given("Le client (.*) n'a aucun ami")]
    public async Task GivenLeClientNaAucunAmi(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        Check.That(await DbContext.Friendships.AnyAsync(f => f.UserId == client.User!.Id)).IsFalse();
    }

    [When(@"Le client (.*) demande à supprimer son amitié avec l'utilisateur ""([^""]*)""")]
    public async Task WhenLeClientDemandeASupprimerSonAmitieAvecLutilisateur(int clientId, string login)
    {
        await SendRequest(MessageFrameBuilder.RemoveFriendFrame(new FriendDto
        {
            Login = login
        }), GetClientWithId(clientId));
    }

    [When(@"Le client (.*) envoie une demande d'ami à ""([^""]*)""")]
    public async Task WhenLeClientEnvoieUneDemandeDamiA(int clientId, string login)
    {
        await SendRequest(MessageFrameBuilder.FriendRequestFrame(new FriendRequestDto
        {
            Login = login
        }), GetClientWithId(clientId));
    }

    [When(@"Le client (.*) accepte la demande d'ami de ""([^""]*)""")]
    public async Task WhenLeClientAccepteLaDemandeDamiDe(int clientId, string login)
    {
        await SendRequest(MessageFrameBuilder.AcceptFriendRequestFrame(new FriendRequestDto
        {
            Login = login
        }), GetClientWithId(clientId));
    }

    [When(@"Le client (.*) refuse la demande d'ami de ""([^""]*)""")]
    public async Task WhenLeClientRefuseLaDemandeDamiDe(int clientId, string login)
    {
        await SendRequest(MessageFrameBuilder.DeclineFriendRequestFrame(new FriendRequestDto
        {
            Login = login
        }), GetClientWithId(clientId));
    }

    [When("Le client (.*) demande à voir ses demandes d'ami")]
    public async Task WhenLeClientDemandeAVoirSesDemandesDami(int clientId)
    {
        await SendRequest(MessageFrameBuilder.GetAllFriendRequestsFrame(), GetClientWithId(clientId));
    }

    [Then(@"Le client (.*) n'est bien pas ami avec ""([^""]*)""")]
    public async Task ThenLeClientNestBienPasAmiAvec(int clientId, string login)
    {
        long userId = await GetUserIdByLogin(login);
        ClientMock client = GetClientWithId(clientId);
        Check.That(await DbContext.Friendships.AnyAsync(f => f.UserId == userId)).IsFalse();
        Check.That(await DbContext.Friendships.AnyAsync(f => f.UserId == client.User!.Id)).IsFalse();
    }

    [When("Le client (.*) demande sa liste d'ami")]
    public async Task WhenLeClientDemandeSaListeDami(int clientId)
    {
        await SendRequest(MessageFrameBuilder.GetAllFriendsFrame(), GetClientWithId(clientId));
    }

    [Then("Il existe bien une demande d'ami de la part du client (.*)")]
    public async Task ThenIlExisteBienUneDemandeDamiDeLaPartDuClient(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        Check.That(await DbContext.FriendRequests.AnyAsync(f => f.UserIdApplicant == client.User!.Id)).IsTrue();
    }

    [Then(@"Le client (.*) est bien ami avec ""([^""]*)""")]
    public async Task ThenLeClientEstBienAmiAvec(int clientId, string login)
    {
        ClientMock client = GetClientWithId(clientId);
        FriendshipEntity friendship = await DbContext.Friendships.FirstAsync(f => f.UserId == client.User!.Id);
        UserEntity userFriend = await DbContext.Users.FirstAsync(u => u.Login.Equals(login));
        Check.That(friendship.FriendId).IsEqualTo(userFriend.Id);
    }

    [Then(@"Il n existe plus de demande d ami entre le client (.*) et ""([^""]*)""")]
    public async Task ThenIlNExistePlusDeDemandeDAmiEntreLeClientEt(int clientId, string _)
    {
        ClientMock client = GetClientWithId(clientId);
        Check.That(await DbContext.FriendRequests.AnyAsync(f => f.UserIdAsked == client.User!.Id)).IsFalse();
        Check.That(await DbContext.FriendRequests.AnyAsync(f => f.UserIdApplicant == client.User!.Id)).IsFalse();
    }

    [Then("Le client (.*) reçoit bien ses (.*) demandes d'ami")]
    public void ThenLeClientRecoitBienSesDemandesDami(int clientId, int numberOfFriendRequest)
    {
        ClientMock client = GetClientWithId(clientId);
        ResponseObjectDto<FriendRequestsDto> dto = VerifyOkResultForClient<ResponseObjectDto<FriendRequestsDto>>(client);
        Check.That(dto.Object.FriendRequests).CountIs(numberOfFriendRequest);
    }

    [Then(@"Le client (.*) n est plus ami avec l utilisateur ""([^""]*)""")]
    public async Task ThenLeClientNEstPlusAmiAvecLUtilisateur(int clientId, string login)
    {
        await WaitOneCycle();
        long userId = await GetUserIdByLogin(login);
        ClientMock client = GetClientWithId(clientId);
        Check.That(await DbContext.Friendships.AnyAsync(f => f.UserId == userId && f.FriendId == client.User!.Id)).IsFalse();
        Check.That(await DbContext.Friendships.AnyAsync(f => f.UserId == client.User!.Id && f.FriendId == userId)).IsFalse();
    }

    [Then("Le client (.*) reçoit bien ses (.*) amis")]
    public void ThenLeClientRecoitBienSesAmis(int clientId, int numberOfFriends)
    {
        ResponseObjectDto<FriendsDto> dto = VerifyOkResultForClient<ResponseObjectDto<FriendsDto>>(GetClientWithId(clientId));
        Check.That(dto.Object.Friends).CountIs(numberOfFriends);
    }
}
