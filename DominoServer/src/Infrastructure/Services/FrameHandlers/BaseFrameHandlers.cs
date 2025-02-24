using Application.Interfaces;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.FrameHandlers;

public abstract class BaseFrameHandlers
{
    public abstract void RegisterHandlers(IMessageFrameHandler handler);

    protected static async Task SendOkToClient(IClient client, FrameCommandType subCommandType)
    {
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), subCommandType)));
    }
}
