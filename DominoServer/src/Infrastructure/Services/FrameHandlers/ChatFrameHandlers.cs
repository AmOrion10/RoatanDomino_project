using Application.Common;
using Application.Interfaces;
using Application.Messages.Queries.Chat;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.FrameHandlers;

public class ChatFrameHandlers(IMessageDispatcher dispatcher) : BaseFrameHandlers
{
    public override void RegisterHandlers(IMessageFrameHandler handler)
    {
        handler.RegisterHandler(FrameCommandType.SendChatMessage, SendChatMessageCallback);
    }
    
    private async Task SendChatMessageCallback(MessageFrame frame, IClient client)
    {
        SendChatMessageDto dto = frame.GetObjectFromMessage<SendChatMessageDto>();
        List<IClient> clients = await dispatcher.Dispatch(new GetAllClientsForChatQuery(dto.ChatId));
        ReceiveChatMessageDto dtoToSend = new()
        {
            ChatId = dto.ChatId,
            Message = dto.Message,
            Sender = client.User!.Login
        };
        foreach (IClient clientToSend in clients)
        {
            await clientToSend.SendAsync(new NetworkFrame(MessageFrameBuilder.ReceiveMessageFrame(dtoToSend)));
        }
    }
}