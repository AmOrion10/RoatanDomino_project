using Application.Interfaces.Managers;

using CommonTests;
using CommonTests.Fixtures;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;
using Infrastructure.Services;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "Lobby")]
public class LobbyStepsDefinition : BaseStepsDefinition
{
    protected LobbyStepsDefinition(ScenarioContext context) : base(context)
    {
        RegisterHandlers();
    }

    [Given("Le client (.*) a déjà un lobby privé")]
    public void GivenLeClientADejaUnLobbyPrive(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        GetService<ILobbyManager>().CreateLobby(client, 4, false, "Lobby");
        Lobby = GetService<ILobbyManager>().GetLobbyForClient(client);
        Check.That(Lobby).IsNotNull();
    }

    [Given("Le lobby est complet")]
    public void GivenLeLobbyEstComplet()
    {
        Check.That(Lobby!.IsFull()).IsTrue();
    }

    [Given(@"Le client d'id (.*) a déjà un lobby classée")]
    public void GivenLeClientDidADejaUnLobbyClassee(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        GetService<ILobbyManager>().CreateLobby(client, 4, false, "Lobby", true);
        Lobby = GetService<ILobbyManager>().GetLobbyForClient(client);
        Check.That(Lobby).IsNotNull();
    }
    
    [Given("Le timer d'avant partie a débuté")]
    public void GivenLeTimerDavantPartieADebute()
    {
        GetService<ILobbyManager>().GetLobbyById(Lobby!.Id)!.Timer.HasStarted = true;
        GetService<ILobbyManager>().GetLobbyById(Lobby!.Id)!.Timer.Duration = ITimerManager.LobbyTimerAllReady;
    }

    [Given(@"Le client (.*) a déjà un lobby de code ""([^""]*)""")]
    public void GivenLeClientADejaUnLobbyDeCode(int clientId, string code)
    {
        ClientMock client = GetClientWithId(clientId);
        GetService<ILobbyManager>().CreateLobby(client, 4, true, "Lobby", code: code);
        Lobby = GetService<ILobbyManager>().GetLobbyForClient(client);
        Check.That(Lobby).IsNotNull();
    }

    [Given(@"Le client (.*) a déjà un lobby de code ""([^""]*)"" avec (.*) places")]
    public void GivenLeClientADejaUnLobbyDeCodeAvecPlaces(int clientId, string code, int maxNumberOfPlayer)
    {
        ClientMock client = GetClientWithId(clientId);
        GetService<ILobbyManager>().CreateLobby(client, maxNumberOfPlayer, true, "Lobby", code: code);
        Lobby = GetService<ILobbyManager>().GetLobbyForClient(client);
        Check.That(Lobby).IsNotNull();
    }

    [When(@"Le client (.*) envoie une requête pour rejoindre le lobby avec le code ""([^""]*)""")]
    public async Task WhenLeClientEnvoieUneRequetePourRejoindreLeLobbyAvecLeCode(int clientId, string code)
    {
        await SendRequest(MessageFrameBuilder.JoinLobbyWithCodeFrame(new LobbyCodeDto
        {
            Code = code
        }), GetClientWithId(clientId));
    }

    [When("Le client (.*) envoie une requête pour créer un lobby public")]
    public async Task WhenLeClientEnvoieUneRequetePourCreerUnLobbyPublic(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        CreateLobbyDto dto = new()
        {
            Name = "Lobby",
            MaxNumberOfPlayer = 4,
            Public = true
        };

        await SendRequest(MessageFrameBuilder.CreateLobbyFrame(dto), client);
    }

    [When("Le client (.*) envoie une requête pour créer un lobby privé")]
    public async Task WhenLeClientEnvoieUneRequetePourCreerUnLobbyPrive(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        CreateLobbyDto dto = new()
        {
            MaxNumberOfPlayer = 4,
            Public = false,
            Name = "Lobby"
        };

        await SendRequest(MessageFrameBuilder.CreateLobbyFrame(dto), client);
    }

    [When("Le client (.*) envoie une requête pour rejoindre le lobby")]
    public async Task WhenLeClientEnvoieUneRequetePourRejoindreLeLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        JoinLobbyDto dto = new()
        {
            LobbyIdToJoin = Lobby!.Id,
        };

        await SendRequest(MessageFrameBuilder.JoinLobbyFrame(dto), client);
    }

    [When("Le client (.*) envoie une requête pour quitter le lobby")]
    public async Task WhenLeClientEnvoieUneRequetePourQuitterLeLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        await SendRequest(MessageFrameBuilder.LeaveLobbyFrame(), client);
    }

    [When("Le client (.*) envoie une requête pour obtenir la liste des lobby")]
    public async Task WhenLeClientEnvoieUneRequetePourObtenirLaListeDesLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        await SendRequest(MessageFrameBuilder.GetAllLobbyFrame(), client);
    }

    [When(@"Le client (.*) envoie une requête pour obtenir la liste des lobby avec un nom spécifique")]
    public async Task WhenLeClientEnvoieUneRequetePourObtenirLaListeDesLobbyAvecUnNomSpecifique(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        LobbyNameDto dto = new LobbyNameDto
        {
            Name = Lobby is null ? "test" : Lobby.Name,
        };
        await SendRequest(MessageFrameBuilder.GetLobbiesWithNameFrame(dto), client);
    }

    [When(@"Le client (.*) envoie une requête pour obtenir la liste des lobby avec un nom différent du lobby existant")]
    public async Task WhenLeClientEnvoieUneRequetePourObtenirLaListeDesLobbyAvecUnNomDifferentDuLobbyExistant(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        LobbyNameDto dto = new LobbyNameDto
        {
            Name = "Guigs",
        };
        await SendRequest(MessageFrameBuilder.GetLobbiesWithNameFrame(dto), client);
    }

    [When("Le client (.*) envoie une requête pour changer son état de préparation")]
    public async Task WhenLeClientEnvoieUneRequetePourChangerSonEtatDePreparation(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        await SendRequest(MessageFrameBuilder.ReadyLobbyFrame(), client);
    }

    [When("Le client (.*) envoie une requête pour rejoindre une partie classée")]
    public async Task WhenLeClientEnvoieUneRequetePourRejoindreUnePartieClassee(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        await SendRequest(MessageFrameBuilder.JoinRankedFrame(), client);
    }

    [Then("Il existe un chat associé pour le lobby")]
    public void ThenIlExisteUnChatAssociePourLeLobby()
    {
        Check.That(GetService<IChatManager>().GetById(Lobby!.ChatId)).IsNotNull();
    }

    [Then("Le client (.*) est informé qu'il a rejoint le chat du lobby")]
    public void ThenLeClientEstInformeQuilARejointLeChatDuLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.JoinChat);
        ChatDto chatDto = frame.GetObjectFromMessage<ChatDto>();
        Check.That(chatDto.Id).IsEqualTo(Lobby!.ChatId);
    }

    [Then("Le client (.*) n'est plus dans le chat du lobby")]
    public void ThenLeClientNestPlusDansLeChatDuLobby(int clientId)
    {
        Check.That(GetService<IChatManager>().ClientIsInChat(GetClientWithId(clientId), Lobby!.ChatId)).IsFalse();
    }

    [Then("Le client (.*) rejoint le lobby")]
    public void ThenLeClientRejointLeLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        Check.That(GetService<ILobbyManager>().PlayerIsAlreadyInALobby(client)).IsTrue();
    }

    [Then("Le chat associé au lobby est supprimé")]
    public void ThenLeChatAssocieAuLobbyEstSupprime()
    {
        Check.That(GetService<IChatManager>().GetById(Lobby!.ChatId)).IsNull();
    }

    [Then("Le client (.*) reçoit une confirmation avec le lobby créé")]
    public void ThenLeClientRecoitUneConfirmationAvecLeLobbyCree(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        Check.That(client.LastFrameSended!.Frames.First().SubCommandType).IsEqualTo(FrameCommandType.CreationLobby);
        ResponseObjectDto<LobbyDto> dto = VerifyOkResultForClient<ResponseObjectDto<LobbyDto>>(client);
        Check.That(dto.Object.Creator.Name).IsEqualTo(client.User!.Login);
    }

    [Then("Le client (.*) reçoit les informations du lobby avec la liste des joueurs")]
    public void ThenLeClientRecoitLesInformationsDuLobbyAvecLaListeDesJoueurs(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        ResponseObjectDto<LobbyDto> dto = VerifyOkResultForClient<ResponseObjectDto<LobbyDto>>(client);
        Check.That(dto.Object.Players.Count).IsEqualTo(2);
        Check.That(dto.Object.Code).IsNotNull();
    }

    [Then("Tous les clients sont notifiés de l'arrivé du client de excepté le client (.*)")]
    public void ThenTousLesClientsSontNotifiesDeLarriveDuClientDeExcepteLeClient(int clientId)
    {
        ClientMock clientJoined = GetClientWithId(clientId);
        Check.That(clientJoined.FramesSended.Any(n => n.Frames.Any(mf => mf.CommandType == FrameCommandType.PlayerJoinedLobby))).IsFalse();
        foreach (ClientMock client in Clients)
        {
            if (client.Id == clientId) continue;
            NetworkFrame networkFrame = client.FramesSended.First(n => n.Frames.Any(mf => mf.CommandType == FrameCommandType.PlayerJoinedLobby));
            MessageFrame mf = networkFrame.Frames.First(mf => mf.CommandType == FrameCommandType.PlayerJoinedLobby);
            PlayerDto player = mf.GetObjectFromMessage<PlayerDto>();
            Check.That(player.Name).IsEqualTo(clientJoined.User!.Login);
        }   
    }

    [Then("Tous les clients sont notifiés du nouveau créateur du lobby excepté le client (.*)")]
    public void ThenTousLesClientsSontNotifiesDuNouveauCreateurDuLobbyExcepteLeClient(int clientId)
    {
        ClientMock clientLeft = GetClientWithId(clientId);
        Check.That(clientLeft.FramesSended.Any( n => n.Frames.Any(mf => mf.CommandType == FrameCommandType.LobbyCreatorChanged))).IsFalse();
        foreach (ClientMock client in Clients)
        {
            if (client.Id == clientId) continue;
            NetworkFrame networkFrame = client.FramesSended.First(n => n.Frames.Any(mf => mf.CommandType == FrameCommandType.LobbyCreatorChanged));
            MessageFrame mf = networkFrame.Frames.First(mf => mf.CommandType == FrameCommandType.LobbyCreatorChanged);
            PlayerDto player = mf.GetObjectFromMessage<PlayerDto>();
            Check.That(player.Name).IsNotEqualTo(clientLeft.User!.Login);
        }
    }

    [Then("Un nouveau lobby pour le client (.*) est créé")]
    public void ThenUnNouveauLobbyPourLeClientEstCree(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        Lobby = GetService<ILobbyManager>().GetLobbyForClient(client);
        Check.That(Lobby).IsNotNull();
    }

    [Then("Le client (.*) quitte le lobby")]
    public void ThenLeClientQuitteLeLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        Check.That(GetService<ILobbyManager>().GetLobbyForClient(client)).IsNull();
    }

    [Then("Le lobby est supprimé")]
    public void ThenLeLobbyEstSupprime()
    {
        Check.That(GetService<ILobbyManager>().GetLobbyById(Lobby!.Id)).IsNull();
    }

    [Then("La liste des lobby est retourné au client (.*)")]
    public void ThenLaListeDesLobbyEstRetourneAuClient(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        ResponseObjectDto<LobbiesInfoDto> dto = VerifyOkResultForClient<ResponseObjectDto<LobbiesInfoDto>>(client);
        Check.That(dto.Object.Lobbies.Count).IsStrictlyGreaterThan(0);
        Check.That(dto.Object.Lobbies.All(l => !string.IsNullOrWhiteSpace(l.Name))).IsTrue();
        Check.That(dto.Object.Lobbies.All(l => !string.IsNullOrWhiteSpace(l.Creator))).IsTrue();
    }

    [Then("La liste vide des lobby est retourné au client (.*)")]
    public void ThenLaListeVideDesLobbyEstRetourneAuClient(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        ResponseObjectDto<LobbiesInfoDto> dto = VerifyOkResultForClient<ResponseObjectDto<LobbiesInfoDto>>(client);
        Check.That(dto.Object.Lobbies.Count).IsEqualTo(0);
    }

    [Then("Tous les clients sont notifiés de l'état prêt du client (.*)")]
    public void ThenTousLesClientsSontNotifiesDeLetatPretDuClient(int clientId)
    {
        ClientMock clientReady = GetClientWithId(clientId);
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LobbyPlayerReadyChanged);
            PlayerReadyDto playerReadyDto = frame.GetObjectFromMessage<PlayerReadyDto>();
            Check.That(playerReadyDto.PlayerId).IsEqualTo(clientReady.User!.Id);
            Check.That(playerReadyDto.IsReady).IsTrue();
        }
    }

    [Then("Le client (.*) est notifié de l'état prêt du client (.*)")]
    public void ThenLeClientEstNotifieDeLetatPretDuClient(int clientId, int clientIdReady)
    {
        ClientMock client = GetClientWithId(clientId);
        ClientMock clientReady = GetClientWithId(clientIdReady);
        MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LobbyPlayerReadyChanged);
        PlayerReadyDto playerReadyDto = frame.GetObjectFromMessage<PlayerReadyDto>();
        Check.That(playerReadyDto.PlayerId).IsEqualTo(clientReady.User!.Id);
        Check.That(playerReadyDto.IsReady).IsTrue();
    }

    [Then("Tous les clients sont notifiés de l'état non prêt du client (.*)")]
    public void ThenTousLesClientsSontNotifiesDeLetatNonPretDuClient(int clientId)
    {
        ClientMock clientReady = GetClientWithId(clientId);
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LobbyPlayerReadyChanged);
            PlayerReadyDto playerReadyDto = frame.GetObjectFromMessage<PlayerReadyDto>();
            Check.That(playerReadyDto.PlayerId).IsEqualTo(clientReady.User!.Id);
            Check.That(playerReadyDto.IsReady).IsFalse();
        }
    }

    [Then("Un timer est envoyé aux clients")]
    public void ThenUnTimerEstEnvoyeAuxClients()
    {
        Check.That(Lobby!.Timer.Duration).Equals(ITimerManager.LobbyTimerAtLeastOneReady);
        Check.That(Lobby!.Timer.HasStarted).IsTrue();
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LobbyTimerUpdated);
            TimerDto dto = frame.GetObjectFromMessage<TimerDto>();
            Check.That(dto.Duration).IsEqualTo(ITimerManager.LobbyTimerAtLeastOneReady);
        }
    }
    
    [Then("Le timer envoyé aux clients est inchangé")]
    public void ThenLeTimerEnvoyeAuxClientsEstInchange()
    {
        Check.That(Lobby!.Timer.Duration).Equals(ITimerManager.LobbyTimerAtLeastOneReady);
        Check.That(Lobby!.Timer.HasStarted).IsTrue();
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LobbyTimerUpdated);
            TimerDto dto = frame.GetObjectFromMessage<TimerDto>();
            Check.That(dto.Duration).IsEqualTo(ITimerManager.LobbyTimerAtLeastOneReady);
        }
    }

    [Then("Le timer le plus court est envoyé aux clients")]
    public void ThenLeTimerLePlusCourtEstEnvoyeAuxClients()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LobbyTimerUpdated);
            TimerDto dto = frame.GetObjectFromMessage<TimerDto>();
            Check.That(dto.Duration).IsEqualTo(ITimerManager.LobbyTimerAllReady);
        }
    }

    [Then("Le timer annulé est envoyé aux clients")]
    public void ThenLeTimerAnnuleEstEnvoyeAuxClients()
    {
        Check.That(Lobby!.Timer.HasStarted).IsFalse();
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LobbyTimerUpdated);
            TimerDto dto = frame.GetObjectFromMessage<TimerDto>();
            Check.That(dto.Duration).IsEqualTo(ITimerManager.LobbyTimerCanceled);
        }
    }

    [Then("Le client (.*) reçoit une confirmation avec le lobby classée créé")]
    public void ThenLeClientRecoitUneConfirmationAvecLeLobbyClasseeCree(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        Check.That(client.LastFrameSended!.Frames.First().SubCommandType).IsEqualTo(FrameCommandType.CreationLobby);
        ResponseObjectDto<LobbyDto> dto = VerifyOkResultForClient<ResponseObjectDto<LobbyDto>>(client);
        Check.That(dto.Object.Ranked).IsTrue();
        Check.That(dto.Object.Players.Count).IsEqualTo(1);
        Lobby = GetService<ILobbyManager>().GetLobbyById(dto.Object.Id);
    }

    [Then("Le client (.*) est dans le chat du lobby")]
    public void ThenLeClientEstDansLeChatDuLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        Check.That(GetService<IChatManager>().ClientIsInChat(client, Lobby!.ChatId)).IsTrue();
    }

    [Then("Le timer annulé est envoyé aux clients sauf au client (.*)")]
    public void ThenLeTimerAnnuleEstEnvoyeAuxClientsSaufAuClient(int clientId)
    {
        Check.That(Lobby!.Timer.HasStarted).IsFalse();
        foreach (ClientMock client in Clients)
        {
            if (client.Id == clientId)
            {
                Check.ThatCode(() => client.GetMessageFrameByCommandType(FrameCommandType.LobbyTimerUpdated)).Throws<MessageFrameNotFoundException>();
            }
            else
            {
                MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LobbyTimerUpdated);
                TimerDto dto = frame.GetObjectFromMessage<TimerDto>();
                Check.That(dto.Duration).IsEqualTo(ITimerManager.LobbyTimerCanceled);
            }
        }
    }

    [Then("Un timer est envoyé aux clients sauf au client (.*)")]
    public void ThenUnTimerEstEnvoyeAuxClientsSaufAuClient(int clientId)
    {
        Check.That(Lobby!.Timer.HasStarted).IsTrue();
        foreach (ClientMock client in Clients)
        {
            if (client.Id == clientId)
            {
                Check.ThatCode(() => client.GetMessageFrameByCommandType(FrameCommandType.LobbyTimerUpdated)).Throws<MessageFrameNotFoundException>();
            }
            else
            {
                MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LobbyTimerUpdated);
                TimerDto dto = frame.GetObjectFromMessage<TimerDto>();
                Check.That(dto.HasStarted).IsTrue();
            }
        }
    }

    [Then("Le client (.*) reçoit l information qu il a quitté le chat du lobby")]
    public void ThenLeClientRecoitLInformationQuIlAQuitteLeChatDuLobby(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LeaveChat);
        ChatDto chatDto = frame.GetObjectFromMessage<ChatDto>();
        Check.That(chatDto.Id).IsEqualTo(Lobby!.ChatId);
    }
}
