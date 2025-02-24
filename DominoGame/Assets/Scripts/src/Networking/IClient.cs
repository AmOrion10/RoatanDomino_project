using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

public interface IClient
{
    MessageFrameEventHandler EventHandlers { get; }

    // Can be null if not connected
    UserDto User { get; } 
    List<FriendDto> Friends { get; } 
    List<FriendRequestDto> FriendRequests { get; }

    void Start();
    void Stop();
    void AddToSendQueue(MessageFrame frame);
    void AddToSendQueue(List<MessageFrame> frames);
}
