using Domain;
using Domain.Game;
using DominoCommon.com.domino.common.Scripts.Enums;

namespace UnitTests;

public class GameTests
{
    [Fact]
    public void GetPlayerPossibleMovesSuccess()
    {
        Game game = new(0, GetPlayersList(), GameMode.UnRanked, 2002);
        game.DistributeDominoes();
        game.PlaceFirstDomino();
        game.NextTurn();
        game.DisplayGrid();
        List<GameMove> moves = game.GetPlayerPossibleMoves(game.PlayerPlaying);
        Check.That(moves.Count).IsStrictlyGreaterThan(0);
    }
    
    [Fact]
    public void CheckPlaceFirstDominoSuccess()
    {
        Game game = new Game(0, GetPlayersList(), GameMode.UnRanked, 2002);
        game.DistributeDominoes();
        game.PlaceFirstDomino();
        Check.That(game.PlayerPlaying.Hand.Count).IsEqualTo(6);
    }

    [Fact]
    public void CheckLargeExampleSuccess()
    {
        Game game = new Game(0, GetPlayersList(), GameMode.UnRanked, 2002);
        game.Players[0].AddDominoToHand(new Domino(5, 5));
        game.Players[0].AddDominoToHand(new Domino(1, 5));
        game.Players[0].AddDominoToHand(new Domino(0, 1));
        game.Players[0].AddDominoToHand(new Domino(0, 2));
        game.Players[0].AddDominoToHand(new Domino(3, 3));
        game.Players[0].AddDominoToHand(new Domino(5, 6));
        game.Players[0].AddDominoToHand(new Domino(0, 4));
        game.Players[0].AddDominoToHand(new Domino(3, 4));
        game.Players[0].AddDominoToHand(new Domino(1, 6));
        game.Players[0].AddDominoToHand(new Domino(1, 4));
        game.Players[0].AddDominoToHand(new Domino(2, 6));
        game.Players[0].AddDominoToHand(new Domino(0, 3));
        game.Players[0].AddDominoToHand(new Domino(4, 5));
        game.Players[0].AddDominoToHand(new Domino(2, 5));

        game.Players[1].AddDominoToHand(new Domino(1, 1));
        game.Players[1].AddDominoToHand(new Domino(0, 0));
        game.Players[1].AddDominoToHand(new Domino(2, 3));
        game.Players[1].AddDominoToHand(new Domino(3, 5));
        game.Players[1].AddDominoToHand(new Domino(0, 6));
        game.Players[1].AddDominoToHand(new Domino(4, 4));
        game.Players[1].AddDominoToHand(new Domino(1, 3));
        game.Players[1].AddDominoToHand(new Domino(4, 6));
        game.Players[1].AddDominoToHand(new Domino(1, 2));
        game.Players[1].AddDominoToHand(new Domino(3, 6));
        game.Players[1].AddDominoToHand(new Domino(0, 5));
        game.Players[1].AddDominoToHand(new Domino(2, 4));
        game.Players[1].AddDominoToHand(new Domino(5, 5));

        game.PlaceFirstDomino();

        if (game.PlayerPlaying.Id != game.Players[0].Id) game.NextTurn();

        game.PlaceDomino(new GameMove(new Domino(1, 5), 30, 31, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalRight, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(1, 1), 30, 33, DominoCommon.com.domino.common.Scripts.Networking.Orientation.PerpendicularVerticalFromLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(0, 1), 30, 34, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalRight, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(0, 0), 30, 36, DominoCommon.com.domino.common.Scripts.Networking.Orientation.PerpendicularVerticalFromLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(0, 2), 30, 37, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalRight, game.PlayerPlaying.Id));
        game.NextTurn();
        foreach (GameMove move in game.GetPlayerPossibleMoves(game.PlayerPlaying))
        {
            if (move.Domino.Side1 == 2 && move.Domino.Side2 == 3)
                Console.WriteLine(move.ToString());
        }
        Console.WriteLine("##########");
        game.PlaceDomino(new GameMove(new Domino(2, 3), 30, 39, DominoCommon.com.domino.common.Scripts.Networking.Orientation.VerticalUp, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(3, 3), 28, 39, DominoCommon.com.domino.common.Scripts.Networking.Orientation.PerpendicularHorizontalFromBottom, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(3, 5), 27, 39, DominoCommon.com.domino.common.Scripts.Networking.Orientation.VerticalUp, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(5, 6), 25, 39, DominoCommon.com.domino.common.Scripts.Networking.Orientation.VerticalUp, game.PlayerPlaying.Id));
        game.NextTurn();
        foreach (GameMove move in game.GetPlayerPossibleMoves(game.PlayerPlaying))
        {
            if (move.Domino.Side1 == 0 && move.Domino.Side2 == 6)
                Console.WriteLine(move.ToString());
        }
        Console.WriteLine("##########");
        game.PlaceDomino(new GameMove(new Domino(0, 6), 23, 39, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(0, 4), 23, 37, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(4, 4), 23, 35, DominoCommon.com.domino.common.Scripts.Networking.Orientation.PerpendicularVerticalFromRight, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(3, 4), 23, 34, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(1, 3), 23, 32, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(1, 6), 23, 30, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(4, 6), 23, 28, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(1, 4), 23, 26, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        foreach (GameMove move in game.GetPlayerPossibleMoves(game.PlayerPlaying))
        {
            if (move.Domino.Side1 == 1 && move.Domino.Side2 == 2)
                Console.WriteLine(move.ToString());
        }
        Console.WriteLine("##########");
        game.PlaceDomino(new GameMove(new Domino(1, 2), 23, 24, DominoCommon.com.domino.common.Scripts.Networking.Orientation.VerticalDown, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(2, 6), 25, 24, DominoCommon.com.domino.common.Scripts.Networking.Orientation.VerticalDown, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(3, 6), 27, 24, DominoCommon.com.domino.common.Scripts.Networking.Orientation.VerticalDown, game.PlayerPlaying.Id));
        game.NextTurn();
        foreach (GameMove move in game.GetPlayerPossibleMoves(game.PlayerPlaying))
        {
            if (move.Domino.Side1 == 0 && move.Domino.Side2 == 3)
                Console.WriteLine(move.ToString());
        }
        Console.WriteLine("##########");
        game.PlaceDomino(new GameMove(new Domino(0, 3), 29, 24, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalRight, game.PlayerPlaying.Id));
        game.NextTurn();
        game.PlaceDomino(new GameMove(new Domino(0, 5), 29, 26, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalRight, game.PlayerPlaying.Id));
        game.NextTurn();
        foreach (GameMove move in game.GetPlayerPossibleMoves(game.PlayerPlaying))
        {
            if (move.Domino.Side1 == 2 && move.Domino.Side2 == 5)
                Console.WriteLine(move.ToString());
        }
        Console.WriteLine("##########");
        game.PlaceDomino(new GameMove(new Domino(2, 5), 30, 29, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        foreach (GameMove move in game.GetPlayerPossibleMoves(game.PlayerPlaying))
        {
            if (move.Domino.Side1 == 2 && move.Domino.Side2 == 4)
                Console.WriteLine(move.ToString());
        }
        Console.WriteLine("##########");
        game.PlaceDomino(new GameMove(new Domino(2, 4), 30, 27, DominoCommon.com.domino.common.Scripts.Networking.Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
        foreach (GameMove move in game.GetPlayerPossibleMoves(game.PlayerPlaying))
        {
            if (move.Domino.Side1 == 4 && move.Domino.Side2 == 5)
                Console.WriteLine(move.ToString());
        }
        Console.WriteLine("##########");
        game.DisplayGrid();
    }

    private static List<GamePlayer> GetPlayersList()
    {
        return
        [
            new GamePlayer(new User(string.Empty, "José"))
            {
                Id = 0
            },

            new GamePlayer(new User(string.Empty, "Jéso"))
            {
                Id = 1
            }
        ];
    }
}
