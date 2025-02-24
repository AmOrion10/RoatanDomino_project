using Domain.Game;
using DominoCommon.com.domino.common.Scripts.Enums;

namespace Infrastructure.Database.Entities;

public class GameEntity
{
    public long Id { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public GameMode GameMode { get; set; }

    public int Bet { get; set; }

    public long? WinnerId { get; set; }

    public int GameDuration { get; set; }

    public int TurnDuration { get; set; }

    public virtual ICollection<MoveEntity> Moves { get; set; } = [];

    public virtual ICollection<UserEntity> Users { get; set; } = [];

    public virtual ICollection<UserGameEntity> UserGames { get; set; } = [];

    public virtual ICollection<GameTournamentEntity> GameTournaments { get; set; } = [];
    
    public virtual UserEntity? Winner { get; set; }

    public HistoryGame ToHistoryDomainEntity()
    {
        List<GameMove> moves = [];
        List<HistoryGamePlayer> players = [];
        foreach (MoveEntity moveEntity in Moves)
        {
            moves.Add(moveEntity.ToDomainEntity());
        }
        foreach (UserEntity playerEntity in Users)
        {
            players.Add(new HistoryGamePlayer(playerEntity.ToDomainEntity()));
        }
        return new HistoryGame(Id, StartTime, EndTime, GameMode, Bet, WinnerId, GameDuration, TurnDuration, moves, players);
    }
}
