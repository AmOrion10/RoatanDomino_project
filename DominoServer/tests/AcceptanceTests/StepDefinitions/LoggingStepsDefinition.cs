using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;

using CommonTests;
using CommonTests.Fixtures;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Infrastructure.Services;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "Logging")]
public class LoggingStepsDefinition : BaseStepsDefinition
{
    private string? email;
    private string? login;
    private string? password;

    protected LoggingStepsDefinition(ScenarioContext context) : base(context)
    {
        RegisterHandlers();
    }

    [Given(@"Un client inscrit avec l'email ""([^""]*)"", le login ""([^""]*)"" et le mdp ""([^""]*)""")]
    public async Task GivenUnClientInscritAvecLemailLeLoginEtLeMdp(string emailClient, string loginClient, string passwordClient)
    {
        email = emailClient;
        login = loginClient;
        password = passwordClient;
        await GetService<IUserRepository>().Create(new User(email, login), password, DateOnly.MinValue);
    }

    [Given("Le client (.*) est logged in")]
    public void GivenLeClientEstLoggedIn(int cliendId)
    {
        ClientMock client = GetClientWithId(cliendId);
        client.IsLoggedIn = true;
        client.User = DbContext.Users.First(u => u.Login.Equals(login)).ToDomainEntity();
    }

    [Given("Le client (.*) n'est pas logged in")]
    public void GivenLeClientNestPasLoggedIn(int clientId)
    {
        Check.That(GetClientWithId(clientId).IsLoggedIn).IsFalse();
    }

    [When("Le client (.*) demande à se connecter avec les bons identifiants")]
    public async Task WhenLeClientDemandeASeConnecterAvecLesBonsIdentifiants(int cliendId)
    {
        LoggingDto dto = new()
        {
            Login = login!,
            Password = password!
        };

        await SendRequest(MessageFrameBuilder.ConnectionFrame(dto), GetClientWithId(cliendId));
    }

    [When("Le client (.*) demande à se connecter avec un login inconnu")]
    public async Task WhenLeClientDemandeASeConnecterAvecUnLoginInconnu(int cliendId)
    {
        LoggingDto dto = new()
        {
            Login = string.Empty,
            Password = password!
        };

        await SendRequest(MessageFrameBuilder.ConnectionFrame(dto), GetClientWithId(cliendId));
    }

    [When("Le client (.*) demande à se connecter avec un mauvais mot de passe")]
    public async Task WhenLeClientDemandeASeConnecterAvecUnMauvaisMotDePasse(int cliendId)
    {
        LoggingDto dto = new()
        {
            Login = login!,
            Password = string.Empty
        };

        await SendRequest(MessageFrameBuilder.ConnectionFrame(dto), GetClientWithId(cliendId));
    }

    [When("Le client (.*) demande à se déconnecter")]
    public async Task WhenLeClientDemandeASeDeconnecter(int cliendId)
    {
        await SendRequest(MessageFrameBuilder.DisconnectionFrame(), GetClientWithId(cliendId));
    }

    [When("Le client (.*) envoie l information qu il est connecté")]
    public async Task WhenLeClientEnvoieLInformationQuIlEstConnecte(int clientId)
    {
        await SendRequest(MessageFrameBuilder.ReadyConnectedFrame(), GetClientWithId(clientId));
    }

    [Then("Le client (.*) reçoit son token et son utilisateur")]
    public void ThenLeClientRecoitSonTokenEtSonUtilisateur(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        ResponseObjectDto<ConnectionResponseDto> dto = VerifyOkResultForClient<ResponseObjectDto<ConnectionResponseDto>>(client);
        Check.That(dto.Object.Token).IsNotEmpty();
        Check.That(dto.Object.User).IsNotNull();
        Check.That(dto.Object.User.Login).IsEqualTo(client.User!.Login);
    }

    [Then("Le client (.*) est connecté")]
    public void ThenLeClientEstConnecte(int cliendId)
    {
        ClientMock client = GetClientWithId(cliendId);
        Check.That(client.IsLoggedIn).IsTrue();
        Check.That(client.User).IsNotNull();
    }

    [Then("Le client (.*) est déconnecté")]
    public void ThenLeClientEstDeconnecte(int cliendId)
    {
        ClientMock client = GetClientWithId(cliendId);
        Check.That(client.IsLoggedIn).IsFalse();
    }

    [Then("Le client (.*) n'est plus dans aucun lobby")]
    public void ThenLeClientNestPlusDansAucunLobby(int clientId)
    {
        Check.That(GetService<ILobbyManager>().GetLobbyForClient(GetClientWithId(clientId))).IsNull();
    }

    [Then("Le client (.*) est bien déconnecté de tous les chats")]
    public async Task ThenLeClientEstBienDeconnecteDeTousLesChats(int clientId)
    {
        await WaitOneCycle();
        List<Chat> chats = GetService<IChatManager>().GetChatsForClient(GetClientWithId(clientId));
        Check.That(chats).CountIs(0);
    }

    [Then("Le client (.*) est connecté au chat global")]
    public void ThenLeClientEstConnecteAuChatGlobal(int clientId)
    {
        IChatManager chatManager = GetService<IChatManager>();
        Check.That(chatManager.ClientIsInChat(GetClientWithId(clientId), chatManager.GlobalChatId)).IsTrue();
    }

    [Then("Le client (.*) reçoit le chat global")]
    public void ThenLeClientRecoitLeChatGlobal(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.JoinChat);
        ChatDto chatDto = frame.GetObjectFromMessage<ChatDto>();
        Check.That(chatDto.Id).IsEqualTo(GetService<IChatManager>().GlobalChatId);
        Check.That(chatDto.Name).IsEqualTo("Global");
    }

    [Then("Tous les clients sont notifiés du départ de la game du client excepté le client (.*)")]
    public void ThenTousLesClientsSontNotifiesDuDepartDeLaGameDuClientExcepteLeClient(int clientId)
    {
        ClientMock clientLeft = GetClientWithId(clientId);
        Check.ThatCode(() => clientLeft.GetMessageFrameByCommandType(FrameCommandType.PlayerLeftGame)).Throws<MessageFrameNotFoundException>();
        foreach (ClientMock client in Clients)
        {
            if (client.Id == clientId) continue;
            MessageFrame mf = client.GetMessageFrameByCommandType(FrameCommandType.PlayerLeftGame);
            PlayerDto player = mf.GetObjectFromMessage<PlayerDto>();
            Check.That(player.Name).IsEqualTo(clientLeft.User!.Login);
            Check.That(player.Id).IsEqualTo(clientLeft.User!.Id);
        }
    }

    [Then("Le client (.*) est informé qu il n est plus dans le chat global")]
    public void ThenLeClientEstInformeQuIlNEstPlusDansLeChatGlobal(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LeaveChat);
        ChatDto chatDto = frame.GetObjectFromMessage<ChatDto>();
        Check.That(chatDto.Id).IsEqualTo(GetService<IChatManager>().GlobalChatId);
    }
}