using Application;
using Application.Common;
using Application.Interfaces;
using Application.Messages.Commands.User;
using Application.Messages.Queries.Chat;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Infrastructure.Services.Managers;

namespace Infrastructure.Services.FrameHandlers;

public class LoggingFrameHandlers(IMessageDispatcher dispatcher, ClientDisconnectionManager disconnectionManager, ResetPasswordManager resetPasswordManager) : BaseFrameHandlers
{
    public override void RegisterHandlers(IMessageFrameHandler handler)
    {
        handler.RegisterHandler(FrameCommandType.Registration, RegistrationCallback, needToBeLoggedIn: false);
        handler.RegisterHandler(FrameCommandType.Connection, ConnexionCallback, needToBeLoggedIn: false);
        handler.RegisterHandler(FrameCommandType.Disconnection, DeconnexionCallback);
        handler.RegisterHandler(FrameCommandType.ReadyConnected, ReadyConnectedCallback);
        handler.RegisterHandler(FrameCommandType.AskResetPassword, AskResetPasswordCallback, needToBeLoggedIn: false);
        handler.RegisterHandler(FrameCommandType.ResetPassword, ResetPasswordCallback, needToBeLoggedIn: false);
    }

    private async Task RegistrationCallback(MessageFrame frame, IClient client)
    {
        RegistrationDto dto = frame.GetObjectFromMessage<RegistrationDto>();
        await dispatcher.Dispatch(new CreateUserCommand
        {
            Login = dto.Login,
            Password = dto.Password,
            Email = dto.Email,
            Birthdate = dto.Birthdate
        });
        await SendOkToClient(client, frame.CommandType);
    }

    private async Task ConnexionCallback(MessageFrame frame, IClient client)
    {
        LoggingDto dto = frame.GetObjectFromMessage<LoggingDto>();
        Tuple<User, Guid> userAndGuid = await dispatcher.Dispatch(new LoginUserCommand
        {
            Login = dto.Login,
            Password = dto.Password,
            Client = client
        });
        ResponseObjectDto<ConnectionResponseDto> response = DtoBuilder.GetConnectionResponseDto(userAndGuid);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }

    private async Task DeconnexionCallback(MessageFrame frame, IClient client)
    {
        DisconnectionResult result = await dispatcher.Dispatch(new LogoutUserCommand(client));
        NetworkFrame networkFrame = new([
            MessageFrameBuilder.LeaveChatFrame(DtoBuilder.GetChatDto(result.GlobalChat)),
            MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), frame.CommandType)
        ]);
        await client.SendAsync(networkFrame);
        await disconnectionManager.ProcessDisconnection(result);
    }

    private async Task ReadyConnectedCallback(MessageFrame frame, IClient client)
    {
        Chat chat = await dispatcher.Dispatch(new GetGlobalChatQuery());
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.JoinChatFrame(DtoBuilder.GetChatDto(chat))));
    }

    private async Task AskResetPasswordCallback(MessageFrame frame, IClient client)
    {
        AskResetPasswordDto dto = frame.GetObjectFromMessage<AskResetPasswordDto>();
        AskResetPasswordResult result = await dispatcher.Dispatch(new AskResetPasswordCommand(dto.Email));
        await resetPasswordManager.Process(result);
        await SendOkToClient(client, frame.CommandType);
    }

    private async Task ResetPasswordCallback(MessageFrame frame, IClient client)
    {
        ResetPasswordDto dto = frame.GetObjectFromMessage<ResetPasswordDto>();
        await dispatcher.Dispatch(new ResetPasswordCommand(dto.Code, dto.Password, dto.Email));
        await SendOkToClient(client, frame.CommandType);
    }
}