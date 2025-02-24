using Application.Interfaces.Repositories;

using Domain.Game;

using Infrastructure.Database;
using Infrastructure.Database.Entities;

namespace Infrastructure.Services.Repositories;

public class MoveRepository(ApplicationDbContext dbContext, IDominoRepository dominoRepository) : IMoveRepository
{
    public async Task Create(long gameId, MoveRecorder moveRecorder)
    {
        List<MoveEntity> moveEntities = [];

        for (int i = 0; i < moveRecorder.MoveHistory.Count; i++)
        {
            GameMove move = moveRecorder.MoveHistory[i];
            moveEntities.Add(new MoveEntity
            {
                Row = move.Row,
                Col = move.Col,
                Orientation = move.Orientation,
                DominoId = await dominoRepository.GetBySides((short)move.Domino.Side1, (short)move.Domino.Side2),
                UserId = move.PlayerId,
                Order = i,
                GameId = gameId,
            });
        }
        await dbContext.Moves.AddRangeAsync(moveEntities);
        await dbContext.SaveChangesAsync();
    }
}
