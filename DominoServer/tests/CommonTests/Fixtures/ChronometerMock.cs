using Application.Interfaces;

namespace CommonTests.Fixtures;

public class ChronometerMock : IChronometer
{
    public TimeSpan GetTimedElapsedReturns { get; set; }

    public void StartFrame()
    {
        
    }

    public void EndFrame()
    {
        
    }

    public TimeSpan GetTimedElapsed()
    {
        return GetTimedElapsedReturns;
    }
}
