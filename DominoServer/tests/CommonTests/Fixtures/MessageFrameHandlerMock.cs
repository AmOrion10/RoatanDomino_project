using Application.Interfaces;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

namespace CommonTests.Fixtures;

public class MessageFrameHandlerMock : IMessageFrameHandler
{
    private readonly Dictionary<FrameCommandType, bool> commandTypes = [];

    public MessageFrameHandlerMock()
    {
        foreach (FrameCommandType commandType in Enum.GetValues<FrameCommandType>())
        {
            commandTypes[commandType] = false;
        }
    }

    public void RegisterHandler(FrameCommandType commandType, Func<MessageFrame, IClient, Task> handler, bool needToBeLoggedIn = true)
    {
        
    }

    public Task HandleAsync(MessageFrame frame, IClient client)
    {
        commandTypes[frame.CommandType] = true;
        return Task.CompletedTask;
    }

    public bool CommandTypeWasCalled(FrameCommandType commandType) => commandTypes[commandType];
}
