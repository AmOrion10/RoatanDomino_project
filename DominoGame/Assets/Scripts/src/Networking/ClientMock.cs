using System.Collections.Generic;
using System.Threading.Tasks;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

public class ClientMock : IClient
{
    public MessageFrameEventHandler EventHandlers { get; } = new();

    public UserDto User { get; } = new()
    {
        Id = 1,
        Email = "UserTest@test.com",
        Login = "UserTest",
        DateSignup = "21/10/2023",
        Nationality = "France",
        BirthDate = "21/02/2002"
    };

    public List<FriendDto> Friends { get; } = new();
    public List<FriendRequestDto> FriendRequests { get; private set; } = new();

    public void Start()
    {
        
    }

    public void Stop()
    {
        
    }

    public void AddToSendQueue(MessageFrame frame)
    {
        Task.Factory.StartNew(async() =>
        {
            await Task.Delay(500);
            MainThread.Instance.AddActionToQueue(() =>
            {
                switch (frame.CommandType)
                {
                    case FrameCommandType.Registration:
                        EventHandlers.MessageFrameReceive(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), FrameCommandType.Registration));
                        //EventHandlers.MessageFrameReceive(MessageFrameBuilder.ResponseFrame(ResponseDto.Error(ErrorTypes.UserAlreadyExist), FrameCommandType.Registration));
                        break;
                    case FrameCommandType.Connection:
                        EventHandlers.MessageFrameReceive(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), FrameCommandType.Connection));
                        //EventHandlers.MessageFrameReceive(MessageFrameBuilder.ResponseFrame(ResponseDto.Error(ErrorTypes.WrongLoggingOrPassword), FrameCommandType.Registration));
                        break;
                    case FrameCommandType.Disconnection:
                        EventHandlers.MessageFrameReceive(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), FrameCommandType.Disconnection));
                        //EventHandlers.MessageFrameReceive(MessageFrameBuilder.ResponseFrame(ResponseDto.Error(ErrorTypes.UserAlreadyLoggedOut), FrameCommandType.Disconnection));
                        break;
                    case FrameCommandType.ReadyInGame:
                        EventHandlers.MessageFrameReceive(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), FrameCommandType.ReadyInGame));
                        break;
                }
            });
        });
    }

    public void AddToSendQueue(List<MessageFrame> frames)
    {
        
    }
}
