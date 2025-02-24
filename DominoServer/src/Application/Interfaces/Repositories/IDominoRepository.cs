using Domain.Game;

namespace Application.Interfaces.Repositories;

public interface IDominoRepository
{
    Task<short> GetBySides(short side1, short side2);
}
