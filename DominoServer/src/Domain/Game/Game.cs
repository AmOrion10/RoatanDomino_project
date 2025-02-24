using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking;

namespace Domain.Game;

public class Game
{
    private const int NumberOfDominoesPerPlayer = 7;
    private const int NumberOfRows = 60;
    private const int NumberOfCols = 60;

    private const double PercentageOfLevelBettedRanked = 0.01;
    private const int FlatBet = 10;

    public int Id { get; }
    public List<GamePlayer> Players { get; }
    public GamePlayer PlayerPlaying => Players[currentPlayerIndex];
    public Timer StartTimer { get; set; } = new();
    public Timer GeneralTimer { get; set; } = new();
    public Timer PlayerTurnTimer { get; set; } = new();
    public DominoStack Stack { get; }
    public GameMove LastGameMove => placer.MoveHistory.LastMove();
    public MoveRecorder MoveHistory => placer.MoveHistory;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public GameMode Mode { get; set; }
    public int TotalBet { get; set; }
    public int ChatId { get; set; }

    private readonly GameGridDto gridDto = new(NumberOfRows, NumberOfCols);
    private readonly DominoPlacer placer;
    private int currentPlayerIndex;
    public List<GamePlayer> Classement { get; } = [];


    public Game(int id, List<GamePlayer> players, GameMode mode, int? seed = null)
    {
        Stack = new DominoStack(seed);
        Id = id;
        Players = players;
        placer = new DominoPlacer(gridDto);
        Mode = mode;
    }

    public void DistributeDominoes()
    {
        StartTime = DateTime.Now;
        foreach (GamePlayer player in Players)
        {
            for (int i = 0; i < NumberOfDominoesPerPlayer; i++)
            {
                Domino? domino = Stack.Draw();
                if (domino is null) break;
                player.Hand.Add(domino);
            }
        }
    }

    public void PlaceFirstDomino()
    {
        (GamePlayer? playerWithBiggestDomino, Domino? biggestDomino) = PlayerWithBiggestDomino(focusOnDouble: true);
        if (playerWithBiggestDomino is not null && biggestDomino is not null)
        {
            GameMove move = new(biggestDomino, NumberOfRows / 2, NumberOfCols / 2, Orientation.PerpendicularVerticalFromLeft, playerWithBiggestDomino.Id);
            placer.PlaceFirstDomino(playerWithBiggestDomino, move);
        }
        else
        {
            (playerWithBiggestDomino, biggestDomino) = PlayerWithBiggestDomino(focusOnDouble: false);
            GameMove move = new(biggestDomino!, NumberOfRows / 2, NumberOfCols / 2, Orientation.HorizontalLeft, playerWithBiggestDomino!.Id);
            placer.PlaceFirstDomino(playerWithBiggestDomino, move);
        }

        currentPlayerIndex = Players.FindIndex(p => p.Id == playerWithBiggestDomino.Id);
    }

    public bool PlaceDomino(GameMove move)
    {
        if (move.PlayerId == PlayerPlaying.Id)
        {
            return placer.PlaceDomino(PlayerPlaying, new GameMove(move.Domino, move.Row, move.Col, move.Orientation, move.PlayerId));
        }
        return false;
    }

    public Domino? Draw(long idUser)
    {
        if (idUser == PlayerPlaying.Id)
        {
            Domino? domino = Stack.Draw();
            if (domino is not null)
            {
                PlayerPlaying.AddDominoToHand(domino);
                return domino;
            }
        }
        return null;
    }

    private (GamePlayer? player, Domino? domino) PlayerWithBiggestDomino(bool focusOnDouble)
    {
        GamePlayer? playerWithBiggestDomino = null;
        Domino? biggestDomino = null;
        int biggestDouble = 0;
        foreach (GamePlayer player in Players)
        {
            foreach (Domino domino in player.Hand)
            {
                if (focusOnDouble && domino.Side1 != domino.Side2) continue;

                int temp = domino.SumOfSides();

                if (temp <= biggestDouble) continue;
                biggestDomino = domino;
                biggestDouble = temp;
                playerWithBiggestDomino = player;
            }
        }
        return (playerWithBiggestDomino, biggestDomino);
    }

    public List<GameMove> GetPlayerPossibleMoves(GamePlayer player)
    {
        return placer.GetPlayerPossibleMoves(player);
    }

    public Dictionary<long, int> GetNumberOfDominoAllPlayer()
    {
        Dictionary<long, int> numberOfDomino = [];
        foreach (GamePlayer player in Players)
        {
            numberOfDomino.Add(player.Id, player.Hand.Count);
        }
        return numberOfDomino;
    }

    public bool CheckEndGame()
    {
        UpdateClassement();
        GamePlayer lastPlayer = Players.First(p => p.Id == LastGameMove.PlayerId);
        if (CheckWin(lastPlayer) || GameBlocked())
        {
            EndTime = DateTime.Now;
            return true;
        }
        return false;
    }

    private static bool CheckWin(GamePlayer player)
    {
        return player.Hand.Count == 0;
    }

    private bool GameBlocked()
    {
        bool gameBlocked = true;
        foreach (GamePlayer player in Players)
        {
            if (placer.GetPlayerPossibleMoves(player).Count != 0)
            {
                gameBlocked = false;
                break;
            }
        }
        return gameBlocked && Stack.IsEmpty();
    }

    public void NextTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % Players.Count;
    }

    public void UpdateClassement()
    {
        Classement.Clear();

        IOrderedEnumerable<GamePlayer> orderedPlayers = Players
            .OrderBy(player => player.Hand.Sum(domino => domino.SumOfSides()))
            .ThenBy(player => player.Hand.Count);

        foreach (GamePlayer player in orderedPlayers)
        {
            Classement.Add(player);
        }
    }

    public void CalculateTotalBet()
    {
        foreach (GamePlayer player in Players)
        {
            int playerBet = FlatBet;
            if (player.Score - (int)(player.Score * PercentageOfLevelBettedRanked) + FlatBet >= 100)
            {
                playerBet = (int)(player.Score * PercentageOfLevelBettedRanked) + FlatBet;
            }
            TotalBet += playerBet;
            player.Bet = playerBet;
        }
    }

    public void DistributeScore()
    {
        int sum = 0;
        for (int i = 1; i < Players.Count + 1; i++)
        {
            sum += i;
        }

        foreach (GamePlayer player in Players)
        {
            if (Classement.IndexOf(player) <= Players.Count)
            {
                double share = Players.Count - Classement.IndexOf(player);
                player.Gain = (int)(share / sum * TotalBet);
            }
        }
    }

    public bool PlayerLeave(GamePlayer player)
    {
        bool res = false;
        GamePlayer playerPlaying = Players[currentPlayerIndex];
        Players.Remove(player);
        if (playerPlaying.Id == player.Id)
        {
            NextTurn();
            res = true;
        }
        currentPlayerIndex = Players.IndexOf(playerPlaying);
        foreach (Domino domino in player.Hand)
        {
            Stack.Add(domino);
        }
        return res;
    }

    // Need for test only
    public void DisplayGrid() // Temp: print in the console
    {
        Console.WriteLine("Current State of the Grid:");
        for (int i = 0; i < gridDto.Grid.Count; i++)
        {
            for (int j = 0; j < gridDto.Grid[i].Count; j++)
            {
                if (gridDto.Grid[i][j] != GameGridDto.EmptySpotValue)
                    Console.Write($"{gridDto.Grid[i][j]} |"); // Temp: print in the console
                else
                    Console.Write("  |"); // Temp: print in the console
            }
            Console.WriteLine();
        }
    }
}