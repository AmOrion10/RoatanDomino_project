using Application.Interfaces.Repositories;

using Domain.Game;

using Infrastructure.Database;
using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Repositories;

public class GameRepository(ApplicationDbContext dbContext, IMoveRepository moveRepository, IUserRepository userRepository) : IGameRepository
{
    public async Task<long> Save(Game game)
    {
        GameEntity entity = new()
        {
            StartTime = game.StartTime,
            EndTime = game.EndTime,
            GameMode = game.Mode,
            Bet = game.TotalBet,
            WinnerId = game.Classement.Count > 0 ? game.Classement[0].Id : null,
            GameDuration = 600,
            TurnDuration = 30 
        };
        await dbContext.Games.AddAsync(entity);
        await dbContext.SaveChangesAsync();

        foreach (GamePlayer player in game.Players)
        {
            entity.Users.Add(await dbContext.Users.FirstAsync(u => u.Id == player.Id));
        }
        await dbContext.SaveChangesAsync();

        await moveRepository.Create(entity.Id, game.MoveHistory);

        return entity.Id;
    }
}
