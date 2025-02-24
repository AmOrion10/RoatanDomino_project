using System.Timers;

using Application.Interfaces;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Timer = System.Timers.Timer;

namespace CommonTests.Fixtures;

public class ClientMock : IClient, IDisposable
{
    public const int ThreadSleepTime = 250;
    public bool IsConnectedReturns { get; set; } = true;
    public NetworkFrame ReceiveAsyncReturns { get; set; } = new(new MessageFrame(FrameCommandType.None, string.Empty));
    public NetworkFrame? LastFrameSended => FramesSended.Count != 0 ? FramesSended.Last() : null;
    public List<NetworkFrame> FramesSended { get; } = [];
    public int Id { get; set; } = idCounter++;
    public bool IsLoggedIn { get; set; } = false;
    public User? User { get; set; } = null;
    public string? Password { get; set; }
    private readonly Timer timer = new(new TimeSpan(0, 0, 2));
    private static int idCounter;

    public ClientMock(int? id = null)
    {
        if (id is not null) Id = id.Value;
        timer.Elapsed += OnTimeElapsed;
        timer.Start();
    }

    public void CloseConnection()
    {
        
    }

    public async Task<NetworkFrame> ReceiveAsync()
    {
        await Task.Delay(ThreadSleepTime);
        return ReceiveAsyncReturns;
    }

    public Task SendAsync(NetworkFrame frame)
    {
        FramesSended.Add(frame);
        return Task.CompletedTask;
    }

    public bool IsConnected()
    {
        Thread.Sleep(ThreadSleepTime);
        return IsConnectedReturns;
    }
    
    private void OnTimeElapsed(object? sender, ElapsedEventArgs e)
    {
        IsConnectedReturns = false;
    }

    public void Dispose()
    {
        timer.Elapsed -= OnTimeElapsed;
        timer.Stop();
        timer.Dispose();
        GC.SuppressFinalize(this);
    }

    public MessageFrame GetMessageFrameByCommandType(FrameCommandType commandType)
    {
        foreach (NetworkFrame networkFrame in FramesSended)
        {
            MessageFrame? frame = networkFrame.Frames.FirstOrDefault(mf => mf.CommandType == commandType);
            if (frame != null) return frame;
        }

        throw new MessageFrameNotFoundException($"Message frame of type {commandType} not found");
    }

    public MessageFrame GetLastMessageFrameByCommandType(FrameCommandType commandType)
    {
        List<NetworkFrame> reversedFrameSended = FramesSended.ToList();
        reversedFrameSended.Reverse();
        foreach (NetworkFrame networkFrame in reversedFrameSended)
        {
            MessageFrame? frame = networkFrame.Frames.FirstOrDefault(mf => mf.CommandType == commandType);
            if (frame != null) return frame;
        }

        throw new MessageFrameNotFoundException($"Message frame of type {commandType} not found");
    }
}
