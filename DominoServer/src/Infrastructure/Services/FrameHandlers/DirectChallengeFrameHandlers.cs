using Application.Interfaces;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.FrameHandlers;

public class DirectChallengeFrameHandlers(DirectChallengeService directChallengeService) : BaseFrameHandlers
{
    public override void RegisterHandlers(IMessageFrameHandler handler)
    {
        handler.RegisterHandler(FrameCommandType.SendDirectChallenge, SendDirectChallengeCallback);
        handler.RegisterHandler(FrameCommandType.RespondDirectChallenge, RespondDirectChallengeCallback);
    }

    private async Task SendDirectChallengeCallback(MessageFrame frame, IClient client)
    {
        SendDirectChallengeDto dto = frame.GetObjectFromMessage<SendDirectChallengeDto>();
        DirectChallengeResult result = await directChallengeService.SendDirectChallengeAsync(client, dto);
        await SendResultAsync(client, frame.CommandType, result);
    }

    private async Task RespondDirectChallengeCallback(MessageFrame frame, IClient client)
    {
        RespondDirectChallengeDto dto = frame.GetObjectFromMessage<RespondDirectChallengeDto>();
        DirectChallengeResult result = await directChallengeService.RespondDirectChallengeAsync(client, dto);
        await SendResultAsync(client, frame.CommandType, result);
    }

    private static async Task SendResultAsync(IClient client, FrameCommandType commandType, DirectChallengeResult result)
    {
        ResponseObjectDto<DirectChallengeActionResponseDto> response = result.Ok
            ? new ResponseObjectDto<DirectChallengeActionResponseDto> { OkResult = true, Object = result.Data! }
            : new ResponseObjectDto<DirectChallengeActionResponseDto>
            {
                OkResult = false,
                ErrorType = ErrorTypes.Unknown,
                ErrorMessage = result.ErrorMessage
            };
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, commandType)));
    }
}
