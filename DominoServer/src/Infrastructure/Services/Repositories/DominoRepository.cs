using Application.Interfaces.Repositories;
using Domain.Game;
using Infrastructure.Database;
using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Repositories;

public class DominoRepository(ApplicationDbContext dbContext) : IDominoRepository
{
    public async Task<short> GetBySides(short side1, short side2)
    {
        return (await dbContext.Dominos.FirstAsync(u => u.Side1 == side1 && u.Side2 == side2)).Id;
    }
}
