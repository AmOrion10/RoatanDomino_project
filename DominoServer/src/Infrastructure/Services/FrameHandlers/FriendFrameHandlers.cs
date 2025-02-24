using Application.Common;
using Application.Interfaces;
using Application.Messages.Commands.Friends;
using Application.Messages.Queries.Friends;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.FrameHandlers;

public class FriendFrameHandlers(IMessageDispatcher dispatcher) : BaseFrameHandlers
{
    public override void RegisterHandlers(IMessageFrameHandler handler)
    {
        handler.RegisterHandler(FrameCommandType.SendFriendRequest, SendFriendRequestCallback);
        handler.RegisterHandler(FrameCommandType.AcceptFriendRequest, AcceptFriendRequestCallback);
        handler.RegisterHandler(FrameCommandType.DeclineFriendRequest, DeclineFriendRequestCallback);
        handler.RegisterHandler(FrameCommandType.GetAllFriendRequests, GetAllFriendRequestsCallback);
        handler.RegisterHandler(FrameCommandType.RemoveFriend, RemoveFriendCallback);
        handler.RegisterHandler(FrameCommandType.GetAllFriends, GetAllFriendsCallback);
    }

    private async Task SendFriendRequestCallback(MessageFrame frame, IClient client)
    {
        FriendRequestDto dto = frame.GetObjectFromMessage<FriendRequestDto>();
        await dispatcher.Dispatch(new SendFriendRequestCommand(dto.Login, client));
        await SendOkToClient(client, frame.CommandType);
    }

    private async Task DeclineFriendRequestCallback(MessageFrame frame, IClient client)
    {
        FriendRequestDto dto = frame.GetObjectFromMessage<FriendRequestDto>();
        await dispatcher.Dispatch(new DeclineFriendRequestCommand(dto.Login, client));
        await SendOkToClient(client, frame.CommandType);
    }

    private async Task AcceptFriendRequestCallback(MessageFrame frame, IClient client)
    {
        FriendRequestDto dto = frame.GetObjectFromMessage<FriendRequestDto>();
        await dispatcher.Dispatch(new AcceptFriendRequestCommand(dto.Login, client));
        await SendOkToClient(client, frame.CommandType);
    }

    private async Task GetAllFriendRequestsCallback(MessageFrame _, IClient client)
    {
        List<FriendRequest> friendRequests = await dispatcher.Dispatch(new GetAllFriendRequestsQuery(client));
        ResponseObjectDto<FriendRequestsDto> response = DtoBuilder.GetFriendRequestsDto(friendRequests);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, FrameCommandType.GetAllFriendRequests)));
    }

    private async Task RemoveFriendCallback(MessageFrame frame, IClient client)
    {
        FriendDto friendDto = frame.GetObjectFromMessage<FriendDto>();
        await dispatcher.Dispatch(new RemoveFriendCommand(friendDto.Login, client));
        await SendOkToClient(client, frame.CommandType);
    }

    private async Task GetAllFriendsCallback(MessageFrame _, IClient client)
    {
        List<Friend> friends = await dispatcher.Dispatch(new GetAllFriendsQuery(client));
        ResponseObjectDto<FriendsDto> response = DtoBuilder.GetFriendsDto(friends);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, FrameCommandType.GetAllFriends)));
    }
}