using Domain.Game;

namespace Application.Interfaces.Repositories;

public interface IGameRepository
{
    Task<long> Save(Game game);
}
