namespace Application.Interfaces;

public interface IChronometer
{
    void StartFrame();
    void EndFrame();

    TimeSpan GetTimedElapsed();
}
