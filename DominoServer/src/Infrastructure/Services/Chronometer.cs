using System.Diagnostics;

using Application.Interfaces;

namespace Infrastructure.Services;

public class Chronometer : IChronometer
{
    private readonly Stopwatch stopwatch = new();

    public void StartFrame()
    {
        stopwatch.Restart();
    }

    public void EndFrame()
    {
        stopwatch.Stop();
    }

    public TimeSpan GetTimedElapsed()
    {
        return stopwatch.Elapsed;
    }
}
