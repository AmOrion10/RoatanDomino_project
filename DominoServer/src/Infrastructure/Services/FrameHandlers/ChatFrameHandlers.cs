using Application.Common;
using Application.Interfaces;
using Application.Messages.Queries.Chat;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Newtonsoft.Json.Linq;

namespace Infrastructure.Services.FrameHandlers;

public class ChatFrameHandlers(IMessageDispatcher dispatcher, ChatService? chatService = null) : BaseFrameHandlers
{
    public override void RegisterHandlers(IMessageFrameHandler handler)
    {
        handler.RegisterHandler(FrameCommandType.SendChatMessage, SendChatMessageCallback);
        handler.RegisterHandler(FrameCommandType.GetChatHistory, GetChatHistoryCallback);
    }

    private async Task SendChatMessageCallback(MessageFrame frame, IClient client)
    {
        // Route Team / Direct ChatMessageDto payloads to ChatService while keeping legacy room chat 100% intact
        if (chatService is not null && IsSocialChatPayload(frame.Message))
        {
            ChatMessageDto socialDto = frame.GetObjectFromMessage<ChatMessageDto>();
            ChatActionResult<ChatMessageDto> result = await chatService.SendMessageAsync(client.User!.Id, socialDto);
            ResponseObjectDto<ChatMessageDto> response = result.Ok
                ? new ResponseObjectDto<ChatMessageDto> { OkResult = true, Object = result.Data! }
                : new ResponseObjectDto<ChatMessageDto>
                {
                    OkResult = false,
                    ErrorType = ErrorTypes.Unknown,
                    ErrorMessage = result.ErrorMessage
                };
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
            return;
        }

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

    private async Task GetChatHistoryCallback(MessageFrame frame, IClient client)
    {
        if (chatService is null)
        {
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Error(ErrorTypes.Unknown), frame.CommandType)));
            return;
        }

        GetChatHistoryDto req = string.IsNullOrWhiteSpace(frame.Message)
            ? new GetChatHistoryDto()
            : frame.GetObjectFromMessage<GetChatHistoryDto>();

        ChatActionResult<ChatHistoryDto> result = await chatService.GetHistoryAsync(client.User!.Id, req);
        ResponseObjectDto<ChatHistoryDto> response = result.Ok
            ? new ResponseObjectDto<ChatHistoryDto> { OkResult = true, Object = result.Data! }
            : new ResponseObjectDto<ChatHistoryDto>
            {
                OkResult = false,
                ErrorType = ErrorTypes.Unknown,
                ErrorMessage = result.ErrorMessage
            };
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }

    private static bool IsSocialChatPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            JObject obj = JObject.Parse(json);
            return obj["ct"] is not null || obj["b"] is not null;
        }
        catch
        {
            return false;
        }
    }
}
