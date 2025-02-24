using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

namespace Application.Interfaces;

public interface IMessageFrameHandler
{
    void RegisterHandler(FrameCommandType commandType, Func<MessageFrame, IClient, Task> handler, bool needToBeLoggedIn = true);
    Task HandleAsync(MessageFrame frame, IClient client);
}
