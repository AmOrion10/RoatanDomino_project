using CommonTests.Fixtures;

using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Infrastructure.Exceptions;
using Infrastructure.Services;

using Microsoft.Extensions.Logging;

namespace UnitTests;

public class MessageFrameHandlerTests
{
    private readonly MessageFrameHandler handler = new(new FakeLogger());
    
    [Fact]
    public async Task CallHandlerCorrectly()
    {
        bool handleMethodCalled = false;
        handler.RegisterHandler(FrameCommandType.None, (_, _) =>
        {
            handleMethodCalled = true;
            return Task.CompletedTask;
        }, false);

        await handler.HandleAsync(new MessageFrame(FrameCommandType.None, string.Empty), new ClientMock());
        Check.That(handleMethodCalled).IsTrue();
    }

    [Fact]
    public void RegisterHandlerTwice()
    {
        handler.RegisterHandler(FrameCommandType.None, (_, _) => Task.CompletedTask);
        Check.ThatCode(() => handler.RegisterHandler(FrameCommandType.None, (_, _) => Task.CompletedTask))
            .Throws<CommandTypeAlreadyHandleException>();
    }

    private class FakeLogger : ILogger<MessageFrameHandler>, IDisposable
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return this;
        }

        public void Dispose()
        {

        }
    }
}
