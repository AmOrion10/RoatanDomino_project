using Application.Common;
using Application.Interfaces;
using Application.Messages.Commands.Lobby;
using Application.Messages.Queries.Chat;
using Application.Messages.Queries.Lobby;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.FrameHandlers;

public class LobbyFrameHandlers(IMessageDispatcher dispatcher) : BaseFrameHandlers
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
            MatchFormat = dto.MatchFormat
        });
        ResponseObjectDto<LobbyDto> response = DtoBuilder.GetLobbyDto(lobby);
        Chat chat = await dispatcher.Dispatch(new GetChatQuery(lobby.ChatId));
        await client.SendAsync(new NetworkFrame([
            MessageFrameBuilder.ResponseFrame(response, frame.CommandType),
            MessageFrameBuilder.JoinChatFrame(DtoBuilder.GetChatDto(chat))
        ]));
    }

    private async Task JoinLobbyCallback(MessageFrame frame, IClient client)
    {
        JoinLobbyDto dto = frame.GetObjectFromMessage<JoinLobbyDto>();
        Lobby lobby = await dispatcher.Dispatch(new JoinLobbyCommand(client, dto.LobbyIdToJoin));
        await SendLobbyToClientAndInformOtherClientsInLobby(client, lobby);
    }

    private async Task JoinLobbyWithCodeCallback(MessageFrame frame, IClient client)
    {
        LobbyCodeDto dto = frame.GetObjectFromMessage<LobbyCodeDto>();
        Lobby lobby = await dispatcher.Dispatch(new JoinLobbyWithCodeCommand(client, dto.Code));
        await SendLobbyToClientAndInformOtherClientsInLobby(client, lobby);
    }

    private async Task LeaveLobbyCallback(MessageFrame frame, IClient client)
    {
        (Lobby? lobby, Chat? item2) = await dispatcher.Dispatch(new LeaveLobbyCommand(client));
        NetworkFrame networkFrame = new([
            MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), frame.CommandType),
            MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(item2))
        ]);
        await client.SendAsync(networkFrame);

        if (lobby is null) return; // The lobby has been deleted

        PlayerDto playerLeftDto = DtoBuilder.GetPlayerDto(client.User!);
        PlayerDto newPlayerCreator = DtoBuilder.GetPlayerDto(lobby.Creator);
        TimerDto timerDto = DtoBuilder.GetTimerDto(lobby.Timer);
        List<IClient> clients = await dispatcher.Dispatch(new GetAllClientsForLobbyQuery(lobby.Id));
        NetworkFrame networkFrameToSend = new([
            MessageFrameBuilder.PlayerLeftLobbyFrame(playerLeftDto),
            MessageFrameBuilder.LobbyCreatorChangedFrame(newPlayerCreator),
            MessageFrameBuilder.LobbyTimerUpdatedFrame(timerDto)
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
        List<IClient> clients = await dispatcher.Dispatch(new GetAllClientsForLobbyQuery(lobby.Id));

        await client.SendAsync(new NetworkFrame([
            MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), frame.CommandType),
            MessageFrameBuilder.PlayerReadyChangedFrame(playerReadyDto),
            MessageFrameBuilder.LobbyTimerUpdatedFrame(timerDto)
        ]));

        foreach (IClient clientInLobby in clients)
        {
            if (clientInLobby.Id == client.Id) continue;
            await clientInLobby.SendAsync(new NetworkFrame([
                MessageFrameBuilder.PlayerReadyChangedFrame(playerReadyDto),
                MessageFrameBuilder.LobbyTimerUpdatedFrame(timerDto)
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
    }

    private async Task SendLobbyToClientAndInformOtherClientsInLobby(IClient client, Lobby lobby)
    {
        PlayerDto playerDto = DtoBuilder.GetPlayerDto(client.User!);
        ResponseObjectDto<LobbyDto> response = DtoBuilder.GetLobbyDto(lobby);
        Chat chat = await dispatcher.Dispatch(new GetChatQuery(lobby.ChatId));
        List<MessageFrame> messageFramesToClientWhoJoined = [
            MessageFrameBuilder.ResponseFrame(response, FrameCommandType.JoinLobby),
            MessageFrameBuilder.JoinChatFrame(DtoBuilder.GetChatDto(chat))
        ];
        List<MessageFrame> messageFramesToAllClientsExceptTheClientWhoJoined = [MessageFrameBuilder.PlayerJoinedLobbyFrame(playerDto)];
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
