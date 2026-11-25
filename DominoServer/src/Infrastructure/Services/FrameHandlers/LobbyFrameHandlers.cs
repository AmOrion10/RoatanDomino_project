using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Messages.Commands.Game;
using Application.Messages.Commands.Lobby;
using Application.Messages.Queries.Chat;
using Application.Messages.Queries.Lobby;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.FrameHandlers;

public class LobbyFrameHandlers(
    IMessageDispatcher dispatcher,
    ILobbyManager lobbyManager,
    IClientManager clientManager,
    ISeedGenerator seedGenerator,
    PresenceService presenceService) : BaseFrameHandlers
{
    public override void RegisterHandlers(IMessageFrameHandler handler)
    {
        handler.RegisterHandler(FrameCommandType.CreationLobby, CreateLobbyCallback);
        handler.RegisterHandler(FrameCommandType.JoinLobby, JoinLobbyCallback);
        handler.RegisterHandler(FrameCommandType.LeaveLobby, LeaveLobbyCallback);
        handler.RegisterHandler(FrameCommandType.GetAllLobbies, GetAllLobbiesCallback);
        handler.RegisterHandler(FrameCommandType.GetLobbiesWithName, GetLobbiesWithNameCallback);
        handler.RegisterHandler(FrameCommandType.ReadyLobbyStateChanged, ReadyLobbyStateChangeCallback);
        handler.RegisterHandler(FrameCommandType.JoinRanked, JoinRankedCallback);
        handler.RegisterHandler(FrameCommandType.JoinLobbyWithCode, JoinLobbyWithCodeCallback);
        handler.RegisterHandler(FrameCommandType.SwitchTableSeat, SwitchTableSeatCallback);
        handler.RegisterHandler(FrameCommandType.AddTableBot, AddTableBotCallback);
        handler.RegisterHandler(FrameCommandType.RemoveTableBot, RemoveTableBotCallback);
        handler.RegisterHandler(FrameCommandType.StartTableMatch, StartTableMatchCallback);
        handler.RegisterHandler(FrameCommandType.SendTableInvite, SendTableInviteCallback);
    }

    private async Task CreateLobbyCallback(MessageFrame frame, IClient client)
    {
        CreateLobbyDto dto = frame.GetObjectFromMessage<CreateLobbyDto>();
        Lobby lobby = await dispatcher.Dispatch(new CreateLobbyCommand
        {
            Creator = client,
            LobbyPublic = dto.Public,
            MaxNumberOfPlayer = dto.MaxNumberOfPlayer,
            Name = dto.Name,
            TableStyle = dto.TableStyle,
            MatchFormat = dto.MatchFormat,
            OneOnePlayoff = dto.OneOnePlayoff
        });
        ResponseObjectDto<LobbyDto> response = DtoBuilder.GetLobbyDto(lobby);
        Chat chat = await dispatcher.Dispatch(new GetChatQuery(lobby.ChatId));
        await client.SendAsync(new NetworkFrame([
            MessageFrameBuilder.ResponseFrame(response, frame.CommandType),
            MessageFrameBuilder.JoinChatFrame(DtoBuilder.GetChatDto(chat))
        ]));
        await presenceService.BroadcastRosterAsync(excludeClientId: client.Id);
    }

    private async Task JoinLobbyCallback(MessageFrame frame, IClient client)
    {
        JoinLobbyDto dto = frame.GetObjectFromMessage<JoinLobbyDto>();
        Lobby lobby = await dispatcher.Dispatch(new JoinLobbyCommand(client, dto.LobbyIdToJoin));
        await SendLobbyToClientAndInformOtherClientsInLobby(client, lobby);
        await presenceService.BroadcastRosterAsync(excludeClientId: client.Id);
    }

    private async Task JoinLobbyWithCodeCallback(MessageFrame frame, IClient client)
    {
        LobbyCodeDto dto = frame.GetObjectFromMessage<LobbyCodeDto>();
        Lobby lobby = await dispatcher.Dispatch(new JoinLobbyWithCodeCommand(client, dto.Code));
        await SendLobbyToClientAndInformOtherClientsInLobby(client, lobby);
        await presenceService.BroadcastRosterAsync(excludeClientId: client.Id);
    }

    private async Task LeaveLobbyCallback(MessageFrame frame, IClient client)
    {
        (Lobby? lobby, Chat? item2) = await dispatcher.Dispatch(new LeaveLobbyCommand(client));
        NetworkFrame networkFrame = new([
            MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), frame.CommandType),
            MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(item2))
        ]);
        await client.SendAsync(networkFrame);
        await presenceService.BroadcastRosterAsync(excludeClientId: client.Id);

        if (lobby is null) return; // The lobby has been deleted

        PlayerDto playerLeftDto = DtoBuilder.GetPlayerDto(client.User!);
        PlayerDto newPlayerCreator = DtoBuilder.GetPlayerDto(lobby.Creator);
        TimerDto timerDto = DtoBuilder.GetTimerDto(lobby.Timer);
        LobbyDto updatedTable = DtoBuilder.GetLobbyDtoObject(lobby);
        List<IClient> clients = await dispatcher.Dispatch(new GetAllClientsForLobbyQuery(lobby.Id));
        NetworkFrame networkFrameToSend = new([
            MessageFrameBuilder.PlayerLeftLobbyFrame(playerLeftDto),
            MessageFrameBuilder.LobbyCreatorChangedFrame(newPlayerCreator),
            MessageFrameBuilder.LobbyTimerUpdatedFrame(timerDto),
            MessageFrameBuilder.TableStateUpdatedFrame(updatedTable)
        ]);

        foreach (IClient clientInLobby in clients)
        {
            if (clientInLobby.Id == client.Id) continue; // We do not send to the user who left the lobby
            await clientInLobby.SendAsync(networkFrameToSend);
        }
    }

    private async Task GetAllLobbiesCallback(MessageFrame frame, IClient client)
    {
        List<Lobby> lobbies = await dispatcher.Dispatch(new GetAllLobbiesQuery());
        ResponseObjectDto<LobbiesInfoDto> response = DtoBuilder.GetLobbiesInfoDto(lobbies);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }

    private async Task GetLobbiesWithNameCallback(MessageFrame frame, IClient client)
    {
        LobbyNameDto dto = frame.GetObjectFromMessage<LobbyNameDto>();
        List<Lobby> lobbies = await dispatcher.Dispatch(new GetLobbiesWithNameQuery(dto.Name));
        ResponseObjectDto<LobbiesInfoDto> response = DtoBuilder.GetLobbiesInfoDto(lobbies);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }

    private async Task ReadyLobbyStateChangeCallback(MessageFrame frame, IClient client)
    {
        Lobby lobby = await dispatcher.Dispatch(new ReadyStateChangeCommand(client));
        PlayerReadyDto playerReadyDto = DtoBuilder.GetPlayerReadyDto(client.User!, lobby);
        TimerDto timerDto = DtoBuilder.GetTimerDto(lobby.Timer);
        LobbyDto updatedTable = DtoBuilder.GetLobbyDtoObject(lobby);
        List<IClient> clients = await dispatcher.Dispatch(new GetAllClientsForLobbyQuery(lobby.Id));

        await client.SendAsync(new NetworkFrame([
            MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), frame.CommandType),
            MessageFrameBuilder.PlayerReadyChangedFrame(playerReadyDto),
            MessageFrameBuilder.LobbyTimerUpdatedFrame(timerDto),
            MessageFrameBuilder.TableStateUpdatedFrame(updatedTable)
        ]));

        foreach (IClient clientInLobby in clients)
        {
            if (clientInLobby.Id == client.Id) continue;
            await clientInLobby.SendAsync(new NetworkFrame([
                MessageFrameBuilder.PlayerReadyChangedFrame(playerReadyDto),
                MessageFrameBuilder.LobbyTimerUpdatedFrame(timerDto),
                MessageFrameBuilder.TableStateUpdatedFrame(updatedTable)
            ]));
        }
    }

    private async Task JoinRankedCallback(MessageFrame frame, IClient client)
    {
        Lobby lobby = await dispatcher.Dispatch(new JoinRankedCommand(client));

        if (lobby.Creator.Id == client.User!.Id)
        {
            ResponseObjectDto<LobbyDto> response = DtoBuilder.GetLobbyDto(lobby);
            Chat chat = await dispatcher.Dispatch(new GetChatQuery(lobby.ChatId));
            await client.SendAsync(new NetworkFrame([
                MessageFrameBuilder.ResponseFrame(response, FrameCommandType.CreationLobby),
                MessageFrameBuilder.JoinChatFrame(DtoBuilder.GetChatDto(chat))
            ]));
        }
        else
        {
            await SendLobbyToClientAndInformOtherClientsInLobby(client, lobby);
        }
        await presenceService.BroadcastRosterAsync(excludeClientId: client.Id);
    }

    private async Task SwitchTableSeatCallback(MessageFrame frame, IClient client)
    {
        Lobby? lobby = lobbyManager.GetLobbyForClient(client);
        if (lobby is null)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.PlayerNotInALobby, "You are not currently seated at a table."),
                frame.CommandType)));
            return;
        }

        SwitchTableSeatDto dto = frame.GetObjectFromMessage<SwitchTableSeatDto>();
        if (!lobbyManager.SwitchSeat(client, lobby.Id, dto.TargetSeatIndex))
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.Unknown, "That seat is already taken."),
                frame.CommandType)));
            return;
        }

        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(DtoBuilder.GetLobbyDto(lobby), frame.CommandType)));
        await BroadcastTableStateAsync(lobby, excludeClientId: client.Id);
    }

    private async Task AddTableBotCallback(MessageFrame frame, IClient client)
    {
        Lobby? lobby = lobbyManager.GetLobbyForClient(client);
        if (lobby is null)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.PlayerNotInALobby, "You are not currently seated at a table."),
                frame.CommandType)));
            return;
        }

        if (!lobby.IsHost(client.User!.Id))
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.Unknown, "Only the table host can add bots."),
                frame.CommandType)));
            return;
        }

        if (lobby.IsFull())
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.LobbyFull, $"This table is full ({lobby.PlayersInLobby.Count}/{lobby.MaxNumberOfPlayer} players)."),
                frame.CommandType)));
            return;
        }

        AddTableBotDto dto = frame.GetObjectFromMessage<AddTableBotDto>();
        LobbyPlayer? added = lobbyManager.AddBotToLobby(client, lobby.Id, dto.SeatIndex, dto.Difficulty);
        if (added is null)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.Unknown, "Could not add a bot to that seat."),
                frame.CommandType)));
            return;
        }

        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(DtoBuilder.GetLobbyDto(lobby), frame.CommandType)));
        await BroadcastTableStateAsync(lobby, excludeClientId: client.Id);
    }

    private async Task RemoveTableBotCallback(MessageFrame frame, IClient client)
    {
        Lobby? lobby = lobbyManager.GetLobbyForClient(client);
        if (lobby is null)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.PlayerNotInALobby, "You are not currently seated at a table."),
                frame.CommandType)));
            return;
        }

        if (!lobby.IsHost(client.User!.Id))
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.Unknown, "Only the table host can remove bots."),
                frame.CommandType)));
            return;
        }

        RemoveTableBotDto dto = frame.GetObjectFromMessage<RemoveTableBotDto>();
        if (!lobbyManager.RemoveBotFromLobby(client, lobby.Id, dto.SeatIndex))
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.Unknown, "No bot found in that seat."),
                frame.CommandType)));
            return;
        }

        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(DtoBuilder.GetLobbyDto(lobby), frame.CommandType)));
        await BroadcastTableStateAsync(lobby, excludeClientId: client.Id);
    }

    private async Task StartTableMatchCallback(MessageFrame frame, IClient client)
    {
        Lobby? lobby = lobbyManager.GetLobbyForClient(client);
        if (lobby is null)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.PlayerNotInALobby, "You are not currently seated at a table."),
                frame.CommandType)));
            return;
        }

        if (!lobby.IsHost(client.User!.Id))
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.Unknown, "Only the table host can start the match."),
                frame.CommandType)));
            return;
        }

        int humanCount = lobby.GetHumanPlayers().Count;
        if (humanCount < 2)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.Unknown, $"Need at least 2 real players to start online ({humanCount}/2 humans seated). Use Offline Mode to play solo against bots."),
                frame.CommandType)));
            return;
        }

        if (lobby.PlayersInLobby.Count < lobby.MaxNumberOfPlayer)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.Unknown, $"Fill all {lobby.MaxNumberOfPlayer} seats (or add bots) before starting the match."),
                frame.CommandType)));
            return;
        }

        List<IClient> clients = await dispatcher.Dispatch(new GetAllClientsForLobbyQuery(lobby.Id));
        Chat chat = await dispatcher.Dispatch(new GetChatQuery(lobby.ChatId));

        // Create the live Game BEFORE notifying clients so their immediate ReadyInGame frame never races!
        await dispatcher.Dispatch(new CreateGameCommand(lobby.Id, seedGenerator.GetNewSeed()));
        await dispatcher.Dispatch(new DeleteLobbyCommand(lobby.Id));

        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), frame.CommandType)));

        NetworkFrame launchFrame = new([
            MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(chat)),
            MessageFrameBuilder.LobbyTimerExpiredFrame()
        ]);
        foreach (IClient clientInLobby in clients)
        {
            await clientInLobby.SendAsync(launchFrame);
        }

        await presenceService.BroadcastRosterAsync();
    }

    private async Task SendTableInviteCallback(MessageFrame frame, IClient client)
    {
        Lobby? lobby = lobbyManager.GetLobbyForClient(client);
        if (lobby is null)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.PlayerNotInALobby, "Join or create a table before inviting players."),
                frame.CommandType)));
            return;
        }

        if (lobby.IsFull())
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.LobbyFull, $"This table is full ({lobby.PlayersInLobby.Count}/{lobby.MaxNumberOfPlayer} players)."),
                frame.CommandType)));
            return;
        }

        TableInviteDto req = frame.GetObjectFromMessage<TableInviteDto>();
        IClient? targetClient = clientManager.GetAllLoggedInClients().FirstOrDefault(c =>
            c.User is not null &&
            ((req.TargetUserId > 0 && c.User.Id == req.TargetUserId) ||
             (!string.IsNullOrWhiteSpace(req.TargetUsername) && c.User.Login.Equals(req.TargetUsername, StringComparison.OrdinalIgnoreCase))));

        if (targetClient is null || targetClient.User is null)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(
                ResponseDto.Error(ErrorTypes.UserNotFound, "That player is currently offline."),
                frame.CommandType)));
            return;
        }

        TableInviteDto invitePayload = new()
        {
            TargetUserId = targetClient.User.Id,
            TargetUsername = targetClient.User.Login,
            FromUserId = client.User!.Id,
            FromUsername = client.User.Login,
            TableId = lobby.Id,
            TableName = lobby.Name,
            TableCode = lobby.Code
        };

        await targetClient.SendAsync(new NetworkFrame(MessageFrameBuilder.TableInviteReceivedFrame(invitePayload)));
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), frame.CommandType)));
    }

    private async Task BroadcastTableStateAsync(Lobby lobby, int? excludeClientId = null)
    {
        LobbyDto tableDto = DtoBuilder.GetLobbyDtoObject(lobby);
        NetworkFrame stateFrame = new(MessageFrameBuilder.TableStateUpdatedFrame(tableDto));
        List<IClient> clients = await dispatcher.Dispatch(new GetAllClientsForLobbyQuery(lobby.Id));
        foreach (IClient clientInLobby in clients)
        {
            if (excludeClientId.HasValue && clientInLobby.Id == excludeClientId.Value) continue;
            await clientInLobby.SendAsync(stateFrame);
        }
    }

    private async Task SendLobbyToClientAndInformOtherClientsInLobby(IClient client, Lobby lobby)
    {
        PlayerDto playerDto = DtoBuilder.GetPlayerDto(client.User!);
        ResponseObjectDto<LobbyDto> response = DtoBuilder.GetLobbyDto(lobby);
        LobbyDto updatedTable = response.Object;
        Chat chat = await dispatcher.Dispatch(new GetChatQuery(lobby.ChatId));
        List<MessageFrame> messageFramesToClientWhoJoined = [
            MessageFrameBuilder.ResponseFrame(response, FrameCommandType.JoinLobby),
            MessageFrameBuilder.JoinChatFrame(DtoBuilder.GetChatDto(chat))
        ];
        List<MessageFrame> messageFramesToAllClientsExceptTheClientWhoJoined = [
            MessageFrameBuilder.PlayerJoinedLobbyFrame(playerDto),
            MessageFrameBuilder.TableStateUpdatedFrame(updatedTable)
        ];
        if (lobby.Timer.HasStarted)
        {
            MessageFrame frameTimerUpdated = MessageFrameBuilder.LobbyTimerUpdatedFrame(DtoBuilder.GetTimerDto(lobby.Timer));
            messageFramesToClientWhoJoined.Add(frameTimerUpdated);
            messageFramesToAllClientsExceptTheClientWhoJoined.Add(frameTimerUpdated);
            foreach (LobbyPlayer player in lobby.PlayersInLobby.Where(p => p.IsReady))
            {
                messageFramesToClientWhoJoined.Add(MessageFrameBuilder.PlayerReadyChangedFrame(DtoBuilder.GetPlayerReadyDto(player, lobby)));
            }
        }

        await client.SendAsync(new NetworkFrame(messageFramesToClientWhoJoined));

        List<IClient> clients = await dispatcher.Dispatch(new GetAllClientsForLobbyQuery(lobby.Id));
        foreach (IClient clientInLobby in clients)
        {
            if (clientInLobby.Id == client.Id) continue; // We do not send to the user who joined the lobby
            await clientInLobby.SendAsync(new NetworkFrame(messageFramesToAllClientsExceptTheClientWhoJoined));
        }
    }
}
