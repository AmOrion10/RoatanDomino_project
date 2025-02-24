using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;
using Application.Messages.Commands.Game;
using CommonTests;
using CommonTests.ExtensionMethods;
using CommonTests.Fixtures;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Infrastructure.Database;
using Infrastructure.Database.Entities;
using Infrastructure.Services;
using Infrastructure.Services.Managers;

using Microsoft.EntityFrameworkCore;

using Web;

namespace AcceptanceTests.Common;

[Binding]
public class BaseStepsDefinition
{
    protected ScenarioContext Context { get; }
    protected TestEnvironment TestEnvironment => Context.Get<TestEnvironment>();
    protected Server? Server { get; set; }
    protected List<ClientMock> Clients { get; set; } = [];
    protected ApplicationDbContext DbContext => GetService<ApplicationDbContext>();
    protected IMessageDispatcher Dispatcher => GetService<IMessageDispatcher>();
    protected Lobby? Lobby { get; set; }
    protected Game? Game { get; set; }
    private Task? ServerTask { get; set; }
    private CancellationTokenSource CancellationTokenSource { get; } = new();
    private static int gameIdCounter;
    private static int userIdCounter;

    protected BaseStepsDefinition(ScenarioContext context)
    {
        Context = context;

        PopulateDominoesTable();
    }

    private void PopulateDominoesTable()
    {
        List<DominoEntity> dominoes = [];
        for (int i = 0; i < 7; i++)
        {
            for (int j = i; j < 7; j++)
            {
                dominoes.Add(new DominoEntity
                {
                    Side1 = (short)i,
                    Side2 = (short)j
                });
            }
        }
        DbContext.Dominos.AddRange(dominoes);
        DbContext.SaveChanges();
    }

    protected async Task<long> GetUserIdByLogin(string login)
    {
        return (await DbContext.Users.FirstAsync(u => u.Login.Equals(login))).Id;
    }

    [Given("Le serveur est en écoute sur le port (.*)")]
    public void GivenLeServeurEstEnEcouteSurLePort(int port)
    {
        Server = new Server(TestEnvironment.ServiceProvider, port, 4);
        ServerTask = Server.StartAsync(CancellationTokenSource.Token);
    }

    [Given("Un client d'id (.*) connecté au serveur")]
    public void GivenUnClientDidConnecteAuServeur(int clientId)
    {
        ClientMock client = new(clientId);
        Clients.Add(client);
        GetService<IClientManager>().HandleNewClient(client);
    }

    [Given(@"Il existe un utilisateur nommé ""([^""]*)""")]
    public void GivenIlExisteUnUtilisateurNomme(string name)
    {
        DbContext.Users.Add(new UserEntity
        {
            Login = name,
            Email = $"{name}@test.com",
            Password = name
        });
        DbContext.SaveChanges();
    }

    [Given(@"Il existe un utilisateur avec l'adresse mail ""([^""]*)""")]
    public void GivenIlExisteUnUtilisateurAvecLadresseMail(string email)
    {
        DbContext.Users.Add(new UserEntity
        {
            Login = email.Split("@")[0] + $"{userIdCounter++}",
            Email = email,
            Password = email
        });
        DbContext.SaveChanges();
    }

    [Given("Un client d'id (.*) connecté au serveur et logged in")]
    public async Task GivenUnClientDidConnecteAuServeurEtLoggedIn(int clientId)
    {
        ClientMock client = CreateClientMockLoggedIn(clientId);
        User user = await GetService<IUserRepository>().Create(new User(client.User!.Email, client.User!.Login), "GoodPassword8", DateOnly.MinValue);
        client.User!.Id = user.Id;
        Clients.Add(client);
        GetService<IClientManager>().HandleNewClient(client);
        GetService<IChatManager>().ClientJoinChat(client, GetService<IChatManager>().GlobalChatId);
    }

    [Given("Deux clients d'id (.*) et (.*) sont connectés et logged in")]
    public async Task GivenDeuxClientsDidEtSontConnectesEtLoggedIn(int clientId1, int clientId2)
    {
        await GivenUnClientDidConnecteAuServeurEtLoggedIn(clientId1);
        await GivenUnClientDidConnecteAuServeurEtLoggedIn(clientId2);
    }
    
    [Given("Trois clients d'id (.*), (.*) et (.*) sont connectés et logged in")]
    public async Task GivenTroisClientsDidEtSontConnectesEtLoggedIn(int clientId1, int clientId2, int clientId3)
    {
        await GivenUnClientDidConnecteAuServeurEtLoggedIn(clientId1);
        await GivenUnClientDidConnecteAuServeurEtLoggedIn(clientId2);
        await GivenUnClientDidConnecteAuServeurEtLoggedIn(clientId3);
    }

    [Given("Le client (.*) a rejoint le lobby")]
    public void GivenLeClientARejointLeLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        GetService<ILobbyManager>().AddPlayerToLobby(client, Lobby!.Id);
    }

    [Given("Deux clients (.*) et (.*) ont rejoint le lobby")]
    public void GivenDeuxClientsEtOntRejointLeLobby(int clientId1, int clientId2)
    {
        GivenLeClientARejointLeLobby(clientId1);
        GivenLeClientARejointLeLobby(clientId2);
    }

    [Given("Le client (.*) a déjà un lobby avec (.*) places")]
    public void GivenLeClientADejaUnLobbyAvecPlaces(int clientId, int maxNumberOfPlayers)
    {
        ClientMock client = GetClientWithId(clientId);
        GetService<ILobbyManager>().CreateLobby(client, maxNumberOfPlayers, true, "Lobby");
        Lobby = GetService<ILobbyManager>().GetLobbyForClient(client);
        Check.That(Lobby).IsNotNull();
    }

    [Given("Le client (.*) est déjà prêt")]
    public void GivenLeClientEstDejaPret(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        GetService<ILobbyManager>().ReadyPlayerStateChange(client, Lobby!.Id);
    }

    [Given("Les clients (.*) et (.*) sont prêts")]
    public void GivenLesClientsEtSontPrets(int clientId1, int clientId2)
    {
        GivenLeClientEstDejaPret(clientId1);
        GivenLeClientEstDejaPret(clientId2);
    }

    [Given("Les trois clients (.*), (.*) et (.*) sont prêts")]
    public void GivenLesTroisClientsEtSontPrets(int clientId1, int clientId2, int clientId3)
    {
        GivenLeClientEstDejaPret(clientId1);
        GivenLeClientEstDejaPret(clientId2);
        GivenLeClientEstDejaPret(clientId3);
    }

    [Given("Il existe une game avec le client (.*) et (.*) pour (.*) joueurs")]
    public void GivenIlExisteUneGameAvecLeClientEt(int clientId1, int clientId2, int numberOfPlayer)
    {
        int id = gameIdCounter++;
        LobbyPlayer player1 = new(GetClientWithId(clientId1).User!);
        GetService<IGameManager>().CreateGameFromLobby(new Lobby(id, player1, numberOfPlayer, true, "Lobby", false, "abcdef")
        {
            PlayersInLobby = [player1, new LobbyPlayer(GetClientWithId(clientId2).User!)]
        }, 2002);
        Game = GetService<IGameManager>().GetById(id);
    }

    [Given("Il existe une game avec trois clients (.*), (.*) et (.*) pour (.*) joueurs")]
    public void GivenIlExisteUneGameAvecTroisClientsEtPourJoueurs(int clientId1, int clientId2, int clientId3, int numberOfPlayer)
    {
        int id = gameIdCounter++;
        LobbyPlayer player1 = new(GetClientWithId(clientId1).User!);
        GetService<IGameManager>().CreateGameFromLobby(new Lobby(id, player1, numberOfPlayer, true, "Lobby", false, "abcdef")
        {
            PlayersInLobby = [player1, new LobbyPlayer(GetClientWithId(clientId2).User!), new LobbyPlayer(GetClientWithId(clientId3).User!)]
        }, 2002);
        Game = GetService<IGameManager>().GetById(id);
    }

    [Given(@"Il existe une game classée avec deux clients (.*) et (.*) pour (.*) joueurs avec chaque joueur ayant un score classée de (.*)")]
    public void GivenIlExisteUneGameClasseeAvecDeuxClientsEtPourJoueursAvecChaqueJoueurAyantUnScoreClasseeDe(int clientId1, int clientId2, int numberOfPlayer, int score)
    {
        int id = gameIdCounter++;
        LobbyPlayer player1 = new(GetClientWithId(clientId1).User!);
        GetService<IGameManager>().CreateGameFromLobby(new Lobby(id, player1, numberOfPlayer, true, "Lobby", true, "abcdef")
        {
            PlayersInLobby = [player1, new LobbyPlayer(GetClientWithId(clientId2).User!)]
        }, 2002);
        Game = GetService<IGameManager>().GetById(id);
        foreach (GamePlayer player in Game!.Players)
        {
            player.Score = score;
        }
    }


    [Given("Le client (.*) est déjà prêt dans la game")]
    public void GivenLeClientEstDejaPretDansLaGame(int clientId)
    {
        ClientMock mock = GetClientWithId(clientId);
        GetService<IGameManager>().GetForClient(mock)!.Players.First(gp => gp.Id == mock.User!.Id).IsReady = true;
    }

    [Given("Les clients (.*) et (.*) sont prêt dans la game")]
    public void GivenLesClientsEtSontPretDansLaGame(int clientId1, int clientId2)
    {
        GivenLeClientEstDejaPretDansLaGame(clientId1);
        GivenLeClientEstDejaPretDansLaGame(clientId2);
    }
    
    [Given(@"Trois clients (.*), (.*) et (.*) sont prêt dans la game")]
    public void GivenTroisClientsEtSontPretDansLaGame(int clientId1, int clientId2, int clientId3)
    {
        GivenLeClientEstDejaPretDansLaGame(clientId1);
        GivenLeClientEstDejaPretDansLaGame(clientId2);
        GivenLeClientEstDejaPretDansLaGame(clientId3);
    }

    [Given("Le client (.*) a déjà un lobby")]
    public void GivenLeClientADejaUnLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        GetService<ILobbyManager>().CreateLobby(client, 4, true, "Lobby");
        Lobby = GetService<ILobbyManager>().GetLobbyForClient(client);
        Check.That(Lobby).IsNotNull();
    }
    
    [Given("La partie a débuté")]
    public async Task GivenLaPartieADebute()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        _ = await Dispatcher.Dispatch(new GameStartingCommand(Game!.Id));
        game.PlaceFirstDomino();
        game.NextTurn();
    }

    [Then(@"Le client (.*) reçoit une erreur de type ""([^""]*)""")]
    public void ThenLeClientRecoitUneErreurDeType(int clientId, string error)
    {
        VerifyBadResultForClient(GetClientWithId(clientId), error.ToErrorType());
    }

    [Then("Le client (.*) reçoit une confirmation")]
    public void ThenLeClientRecoitUneConfirmation(int clientId)
    {
        VerifyOkResultForClient<ResponseDto>(GetClientWithId(clientId));
    }

    [Then("Tous les clients sont notifiés du départ du client excepté le client (.*)")]
    public void ThenTousLesClientsSontNotifiesDuDepartDuClientExcepteLeClient(int clientId)
    {
        ClientMock clientLeft = GetClientWithId(clientId);
        Check.That(clientLeft.FramesSended.Any(n => n.Frames.Any(mf => mf.CommandType == FrameCommandType.PlayerLeftLobby))).IsFalse();
        foreach (ClientMock client in Clients)
        {
            if (client.Id == clientId) continue;
            MessageFrame mf = client.GetMessageFrameByCommandType(FrameCommandType.PlayerLeftLobby);
            PlayerDto player = mf.GetObjectFromMessage<PlayerDto>();
            Check.That(player.Name).IsEqualTo(clientLeft.User!.Login);
        }
    }

    [Then("Tous les clients sont informés que la game est finie sauf le client (.*)")]
    public void ThenTousLesClientsSontInformesQueLaGameEstFinieSaufLeClient(int clientId)
    {
        ClientMock clientLeft = GetClientWithId(clientId);
        Check.ThatCode(() => clientLeft.GetMessageFrameByCommandType(FrameCommandType.EndGame)).Throws<MessageFrameNotFoundException>();
        foreach (ClientMock client in Clients)
        {
            if (client.Id == clientId) continue;
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.EndGame);
            EndGameDto dto = frame.GetObjectFromMessage<EndGameDto>();
            Check.That(dto.NormalFinish).IsFalse();
        }
    }

    [Then("Les clients sont déconnectés du chat de la game")]
    public void ThenLesClientsSontDeconnectesDuChatDeLaGame()
    {
        Check.That(GetService<IChatManager>().GetById(Game!.ChatId)).IsNull();
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LeaveChat);
            ChatDto chatDto = frame.GetObjectFromMessage<ChatDto>();
            Check.That(chatDto.Id).IsEqualTo(Game!.ChatId);
        }
    }

    protected async Task SendRequest(MessageFrame frame, IClient client)
    {
        await GetService<IMessageFrameHandler>().HandleAsync(frame, client);
    }

    protected static async Task WaitOneCycle()
    {
        await Task.Delay(TcpServerMock.ThreadSleepTime + 1);
    }

    protected static void VerifyBadResultForClient(ClientMock client, ErrorTypes error)
    {
        ResponseDto dto = client.LastFrameSended!.Frames.First().GetObjectFromMessage<ResponseDto>();
        Check.That(dto.OkResult).IsFalse();
        Check.That(dto.ErrorType).IsEqualTo(error);
    }

    protected static T VerifyOkResultForClient<T>(ClientMock client) where T : ResponseDto
    {
        T obj = client.LastFrameSended!.Frames.First().GetObjectFromMessage<T>();
        Check.That(obj.OkResult).IsTrue();
        return obj;
    }

    protected T GetService<T>()
    {
        var service = (T?)TestEnvironment.ServiceProvider.GetService(typeof(T));
        return service is null ? throw new Exception($"No service of type {typeof(T).Name} was found.") : service;
    }

    protected U GetService<T, U>() where U : class
    {
        return (GetService<T>() as U)!;
    }

    protected void ReplaceTestEnvironment()
    {
        Context.Set(TestEnvironment.BuildScope(ServiceCollectionBuilder.BuildServices(Context)));
    }

    protected void RegisterHandlers()
    {
        GetService<MessageFrameHandlers>().RegisterHandlers();
    }

    protected ClientMock GetClientWithId(int id) => Clients.First(c => c.Id == id);

    protected static ClientMock CreateClientMockLoggedIn(int clientId)
        => new(clientId)
        {
            IsLoggedIn = true,
            User = new User($"client{clientId}@user.com", $"Client{clientId}")
        };

    [AfterScenario]
    public async Task FinishScenario()
    {
        await CancellationTokenSource.CancelAsync();
        await (ServerTask ?? Task.CompletedTask);
    }
}