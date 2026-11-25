using Application.Interfaces;
using Application.Interfaces.Managers;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Enums;

using Timer = Domain.Timer;

namespace Infrastructure.Services.Managers;

public class GameManager(IChatManager chatManager) : IGameManager
{
    private readonly List<Game> games = [];

    public Game? GetById(int gameId)
    {
        lock (games)
        {
            return games.FirstOrDefault(g => g.Id == gameId);
        }
    }

    public void CreateGameFromLobby(Lobby lobby, int seed)
    {
        List<LobbyPlayer> orderedSeats = lobby.GetSeatedPlayersOrdered();
        bool hasBots = orderedSeats.Any(p => p.IsBot);
        List<GamePlayer> players = orderedSeats.Select(lobbyPlayer =>
        {
            GamePlayer gp = new(lobbyPlayer)
            {
                IsHumanSeat = !lobbyPlayer.IsBot && lobbyPlayer.Id > 0
            };
            if (lobbyPlayer.IsBot)
            {
                gp.IsReady = true;
                gp.IsBotControlled = true;
                gp.IsPermanentlyBot = true;
                gp.IsHumanSeat = false;
                gp.BotDifficulty = lobbyPlayer.BotDifficulty ?? "Pro";
            }
            return gp;
        }).ToList();

        GameMode mode = lobby.Ranked ? GameMode.Ranked : GameMode.UnRanked;
        Game game;
        if (lobby.TableStyle.HasValue || lobby.MatchFormat.HasValue || lobby.OneOnePlayoff.HasValue || hasBots)
        {
            TableStyle tableStyle = lobby.TableStyle ?? TableStyle.DRAW;
            MatchFormat matchFormat = lobby.MatchFormat ?? MatchFormat.CUT_THROAT;
            bool isTeam2v2 = players.Count == 4 && matchFormat != MatchFormat.CUT_THROAT;
            bool oneOnePlayoff = lobby.OneOnePlayoff ?? false;
            game = new Game(lobby.Id, players, mode, tableStyle, matchFormat, seed, isTeam2v2: isTeam2v2, oneOnePlayoff: oneOnePlayoff)
            {
                ChatId = chatManager.CreateChat("Game"),
                LobbyId = lobby.Id,
                MatchTitle = lobby.Name,
                IsTeamChallenge = lobby.IsTeamChallenge,
                TeamChallengeId = lobby.TeamChallengeId,
                TeamAId = lobby.TeamAId,
                TeamAName = lobby.TeamAName,
                TeamATag = lobby.TeamATag,
                TeamBId = lobby.TeamBId,
                TeamBName = lobby.TeamBName,
                TeamBTag = lobby.TeamBTag
            };
        }
        else
        {
            game = new Game(lobby.Id, players, mode, seed)
            {
                ChatId = chatManager.CreateChat("Game"),
                LobbyId = lobby.Id,
                MatchTitle = lobby.Name
            };
        }
        lock (games)
        {
            games.Add(game);
        }
    }

    public Game? GetForClient(IClient client)
    {
        lock (games)
        {
            return games.FirstOrDefault(g => g.Players.Any(p => p.Id == client.User!.Id));
        }
    }

    public bool AllPlayersReadyForGame(int gameId)
    {
        lock (games)
        {
            Game game = games.First(g => g.Id == gameId);
            return game.Players.All(gp => gp.IsReady || gp.IsPermanentlyBot);
        }
    }

    public void StartTimerForGame(int gameId)
    {
        lock (games)
        {
            Game game = games.First(g => g.Id == gameId);
            game.StartTimer.HasStarted = true;
            game.StartTimer.Duration = IGameManager.GameTimerStarting;
        }
    }

    public Timer GetStartTimerForGame(int gameId)
    {
        lock (games)
        {
            return games.First(g => g.Id == gameId).StartTimer;
        }
    }

    public List<Game> GetGamesWithTimerStarted()
    {
        lock (games)
        {
            return games.Where(g => g.StartTimer.HasStarted).ToList();
        }
    }

    public void UpdateStartTimer(int gameId, int newDuration)
    {
        lock (games)
        {
            games.First(g => g.Id == gameId).StartTimer.Duration = newDuration;
        }
    }

    public void StartTimerGeneralGame(int gameId)
    {
        lock (games)
        {
            Game game = games.First(g => g.Id == gameId);
            game.GeneralTimer.HasStarted = true;
            game.GeneralTimer.Duration = IGameManager.GameTimerGeneral;
        }
    }

    public Timer GetGeneralTimerForGame(int gameId)
    {
        lock (games)
        {
            return games.First(g => g.Id == gameId).GeneralTimer;
        }
    }

    public List<Game> GetGamesWithGeneralTimerStarted()
    {
        lock (games)
        {
            return games.Where(g => g.GeneralTimer.HasStarted).ToList();
        }
    }

    public void UpdateGeneralTimer(int gameId, int newDuration)
    {
        lock (games)
        {
            games.First(g => g.Id == gameId).GeneralTimer.Duration = newDuration;
        }
    }

    public void StartTimerPlayerTurn(int gameId)
    {
        lock (games)
        {
            Game game = games.First(g => g.Id == gameId);
            game.PlayerTurnTimer.HasStarted = true;
            game.PlayerTurnTimer.Duration = game.IsLegacyDrawAdvance
                ? IGameManager.GameTimerPerPlayer
                : game.GetActiveTurnDurationSeconds();
        }
    }

    public Timer GetPlayerTurnTimer(int gameId)
    {
        lock (games)
        {
            return games.First(g => g.Id == gameId).PlayerTurnTimer;
        }
    }

    public List<Game> GetGamesWithPlayerTurnTimerStarted()
    {
        lock (games)
        {
            return games.Where(g => g.PlayerTurnTimer.HasStarted).ToList();
        }
    }

    public void UpdatePlayerTurnTimer(int gameId, int newDuration)
    {
        lock (games)
        {
            games.First(g => g.Id == gameId).PlayerTurnTimer.Duration = newDuration;
        }
    }

    public void DeleteGame(int gameId)
    {
        Game game;
        lock (games)
        {
            game = games.First(g => g.Id == gameId);
            games.Remove(game);
        }
        chatManager.DeleteChat(game.ChatId);
    }

    public bool PlayerLeave(int gameId, IClient playerLeaving)
    {
        lock (games)
        {
            Game game = games.First(g => g.Players.Any(p => p.Id == playerLeaving.User!.Id));
            return game.PlayerLeave(game.Players.First(p => p.Id == playerLeaving.User!.Id));
        }
    }

    public List<Game> GetAllActiveGames()
    {
        lock (games)
        {
            return games.ToList();
        }
    }

    public Game? FindRejoinableGame(int matchId, long playerId, string reconnectToken, out int remainingGraceSeconds)
    {
        remainingGraceSeconds = 0;
        if (string.IsNullOrWhiteSpace(reconnectToken))
        {
            return null;
        }

        lock (games)
        {
            Game? game = matchId > 0
                ? games.FirstOrDefault(g => g.Id == matchId)
                : games.FirstOrDefault(g => g.Players.Any(p => p.Id == playerId));

            if (game is null) return null;
            if (game.CanPlayerRejoin(playerId, reconnectToken, out remainingGraceSeconds))
            {
                return game;
            }
            return null;
        }
    }

    public bool AddSpectator(int gameId, long userId)
    {
        if (userId <= 0) return false;
        lock (games)
        {
            Game? target = games.FirstOrDefault(g => g.Id == gameId);
            if (target is null) return false;
            // Leave any other spectated game first so a user spectates at most one match
            foreach (Game g in games)
            {
                if (g.Id != gameId) g.RemoveSpectator(userId);
            }
            return target.AddSpectator(userId);
        }
    }

    public bool RemoveSpectator(int gameId, long userId)
    {
        lock (games)
        {
            Game? target = games.FirstOrDefault(g => g.Id == gameId);
            return target is not null && target.RemoveSpectator(userId);
        }
    }

    public List<Game> RemoveSpectatorFromAllGames(long userId)
    {
        List<Game> affected = [];
        if (userId <= 0) return affected;
        lock (games)
        {
            foreach (Game g in games)
            {
                if (g.RemoveSpectator(userId))
                {
                    affected.Add(g);
                }
            }
        }
        return affected;
    }

    public Game? GetSpectatedGameForUser(long userId)
    {
        if (userId <= 0) return null;
        lock (games)
        {
            return games.FirstOrDefault(g => g.HasSpectator(userId));
        }
    }
}
