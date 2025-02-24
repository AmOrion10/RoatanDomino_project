using Infrastructure.Services;

namespace UnitTests;

public class ChronometerTests
{
    [Fact]
    public void ShouldMeasureTimeCorrectly()
    {
        Chronometer chronometer = new();
        chronometer.StartFrame();
        Thread.Sleep(250);
        chronometer.EndFrame();
        TimeSpan timeElapsed = chronometer.GetTimedElapsed();
        Check.That(timeElapsed.TotalMilliseconds).IsStrictlyGreaterThan(250);
    }

    [Fact]
    public void ShouldMeasureCorrectlyInLoop()
    {
        Chronometer chronometer = new();
        for (int i = 0; i < 5; i++)
        {
            chronometer.StartFrame();
            Thread.Sleep(100);
            chronometer.EndFrame();
            TimeSpan timeElapsed = chronometer.GetTimedElapsed();
            Check.That(timeElapsed.TotalMilliseconds).IsStrictlyGreaterThan(100);
            Check.That(timeElapsed.TotalMilliseconds).IsStrictlyLessThan(200);
        }
    }
}
