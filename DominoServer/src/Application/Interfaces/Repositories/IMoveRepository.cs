using Domain.Game;

namespace Application.Interfaces.Repositories;

public interface IMoveRepository
{
    Task Create(long gameId, MoveRecorder moveRecorder);
}
