using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts;
using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using Timer = Domain.Timer;

namespace Infrastructure.Services;

public static class DtoBuilder
{
    public static LobbyDto GetLobbyDtoObject(Lobby lobby)
    {
        List<LobbyPlayer> ordered = lobby.GetSeatedPlayersOrdered();
        List<LobbySeatDto> seats = [];
        for (int i = 0; i < lobby.MaxNumberOfPlayer; i++)
        {
            LobbyPlayer? occupant = lobby.GetPlayerAtSeat(i);
            seats.Add(new LobbySeatDto
            {
                SeatIndex = i,
                IsOccupied = occupant is not null,
                IsBot = occupant?.IsBot ?? false,
                BotDifficulty = occupant?.BotDifficulty,
                IsReady = occupant?.IsReady ?? false,
                Player = occupant is null ? null : new PlayerDto
                {
                    Id = occupant.Id,
                    Name = occupant.Login,
                    Elo = occupant.Score,
                    IsBot = occupant.IsBot,
                    BotDifficulty = occupant.BotDifficulty,
                    SeatIndex = occupant.SeatIndex
                }
            });
        }

        return new LobbyDto
        {
            Creator = new PlayerDto
            {
                Id = lobby.Creator.Id,
                Name = lobby.Creator.Login,
                Elo = lobby.Creator.Score,
                IsBot = lobby.Creator.IsBot,
                BotDifficulty = lobby.Creator.BotDifficulty,
                SeatIndex = lobby.Creator.SeatIndex
            },
            Players = ordered.Select(p => new PlayerDto
            {
                Id = p.Id,
                Name = p.Login,
                Elo = p.Score,
                IsBot = p.IsBot,
                BotDifficulty = p.BotDifficulty,
                SeatIndex = p.SeatIndex
            }).ToList(),
            Seats = seats,
            Id = lobby.Id,
            Name = lobby.Name,
            IsPublic = lobby.IsPublic,
            MaxNumberOfPlayer = lobby.MaxNumberOfPlayer,
            Ranked = lobby.Ranked,
            Code = lobby.Code,
            TableStyle = lobby.TableStyle,
            MatchFormat = lobby.MatchFormat,
            OneOnePlayoff = lobby.OneOnePlayoff,
            IsHouseTable = lobby.IsHouseTable
        };
    }

    public static ResponseObjectDto<LobbyDto> GetLobbyDto(Lobby lobby)
    {
        return new ResponseObjectDto<LobbyDto>
        {
            Object = GetLobbyDtoObject(lobby)
        };
    }

    public static PlayerDto GetPlayerDto(User user)
    {
        return new PlayerDto
        {
            Id = user.Id,
            Name = user.Login,
            Elo = user.Score
        };
    }

    public static ResponseObjectDto<InfoProfileDto> GetInfoProfileDto(User user)
    {
        return new ResponseObjectDto<InfoProfileDto>
        {
            Object = new InfoProfileDto
            {
                User = new UserDto
                {
                    Id = user.Id,
                    Login = user.Login,
                    Email = user.Email,
                    Score = user.Score,
                    BirthDate = user.Birthdate.ToString(Constants.ShortFormatDate),
                    DateSignup = user.DateSignup.ToString(Constants.LongFormatDate),
                    Nationality = user.Nationality
                },
                Statistic = new StatisticDto
                {
                    NumberOfGamePlayed = user.Statistics!.NumberOfGamePlayed,
                    NumberOfGameWon = user.Statistics.NumberOfGameWon,
                    NumberOfGameLost = user.Statistics.NumberOfGameLost
                }
            }
        };
    }

    public static ResponseObjectDto<LobbiesInfoDto> GetLobbiesInfoDto(List<Lobby> lobbies)
    {
        return new ResponseObjectDto<LobbiesInfoDto>
        {
            Object = new LobbiesInfoDto
            {
                Lobbies = lobbies.Select(l => new LobbyInfoDto
                {
                    Creator = l.Creator.Login,
                    Id = l.Id,
                    MaxNumberOfPlayer = l.MaxNumberOfPlayer,
                    NumberOfPlayer = l.PlayersInLobby.Count,
                    Name = l.Name,
                    IsPublic = l.IsPublic,
                    TableStyle = l.TableStyle,
                    MatchFormat = l.MatchFormat,
                    OneOnePlayoff = l.OneOnePlayoff,
                    IsHouseTable = l.IsHouseTable,
                    SeatedPlayers = l.GetSeatedPlayersOrdered()
                        .Select(p => p.IsBot ? $"{p.Login} ({p.BotDifficulty ?? "Pro"})" : p.Login)
                        .ToList()
                }).ToList()
            }
        };
    }

    public static TimerDto GetTimerDto(Timer timer)
    {
        return new TimerDto
        {
            Duration = timer.Duration,
            HasStarted = timer.HasStarted
        };
    }

    public static ResponseObjectDto<ConnectionResponseDto> GetConnectionResponseDto(Tuple<User, Guid> userAndGuid)
    {
        return new ResponseObjectDto<ConnectionResponseDto>
        {
            Object = new ConnectionResponseDto
            {
                User = new UserDto
                {
                    Email = userAndGuid.Item1.Email,
                    Id = userAndGuid.Item1.Id,
                    Login = userAndGuid.Item1.Login,
                    DateSignup = userAndGuid.Item1.DateSignup.ToString(Constants.LongFormatDate),
                    Score = userAndGuid.Item1.Score,
                    Nationality = userAndGuid.Item1.Nationality,
                    BirthDate = userAndGuid.Item1.Birthdate.ToString(Constants.ShortFormatDate)
                },
                Token = userAndGuid.Item2.ToString()
            }
        };
    }

    public static PlayerReadyDto GetPlayerReadyDto(User user, Lobby lobby)
    {
        return new PlayerReadyDto
        {
            PlayerId = user.Id,
            IsReady = lobby.PlayersInLobby.First(p => p.Id == user.Id).IsReady
        };
    }

    public static PossibleMovesDto GetPlayerPossibleMovesDto(Game game)
    {
        return new PossibleMovesDto
        {
            Moves = game.GetPlayerPossibleMoves(game.PlayerPlaying).Select(p => new MoveDto
            {
                Col = p.Col,
                Row = p.Row,
                Orientation = p.Orientation,
                Domino = new DominoDto
                {
                    Side1 = p.Domino.Side1,
                    Side2 = p.Domino.Side2,
                },
                PlayerId = p.PlayerId
            }).ToList()
        };
    }

    public static NumberOfDominoPerPlayerDto GetNumberOfDominoPerPlayerDto(Dictionary<long, int> numberOfDomino)
    {
        return new NumberOfDominoPerPlayerDto
        {
            NumberOfDomino = numberOfDomino,
        };
    }

    public static PlayerTurnDto GetPlayerTurnDto(long playerId, TimerDto timer)
    {
        return new PlayerTurnDto
        {
            IdUserPlaying = playerId,
            Timer = timer
        };
    }

    public static PlayerTurnDto GetPlayerTurnDto(Game game, TimerDto timer)
    {
        if (game.IsLegacyDrawAdvance)
        {
            return GetPlayerTurnDto(game.PlayerPlaying.Id, timer);
        }

        return new PlayerTurnDto
        {
            IdUserPlaying = game.PlayerPlaying.Id,
            Timer = timer,
            IsAutoPassTurn = game.IsCurrentPlayerAutoPassTurn(),
            IsBotControlled = game.PlayerPlaying.IsBotControlled,
            ConsecutiveAutoPlays = game.PlayerPlaying.ConsecutiveAutoPlays
        };
    }

    public static PlayerPlayDto GetPlayerPlayDto(long playerId, PlayActionType type)
    {
        return new PlayerPlayDto
        {
            IdUserPlayed = playerId,
            PlayActionType = type
        };
    }

    public static PlayerPlayDto CopyPlayerPlayDto(PlayerPlayDto dto)
    {
        return new PlayerPlayDto
        {
            IdUserPlayed = dto.IdUserPlayed,
            PlayActionType = dto.PlayActionType,
            Moves = dto.Moves
        };
    }

    public static EndGameDto GetEndGameDto(bool normalFinish, Game game)
    {
        List<PlayerRankingsDto> playerClassement = [];
        foreach (GamePlayer player in game.Classement)
        {
            PlayerRankingsDto newPlayerClassement = new()
            {
                Id = player.Id,
                Rank = game.Classement.IndexOf(player) + 1,
                Gain = player.Gain
            };
            playerClassement.Add(newPlayerClassement);
        }
        EndGameDto dto = new()
        {
            NormalFinish = normalFinish,
            Rankings = playerClassement
        };

        if (!game.IsLegacyDrawAdvance && game.LastHandResolution is not null)
        {
            dto.BreakTriggered = game.LastHandResolution.BreakTriggered;
            dto.MatchOver = game.LastHandResolution.MatchOver;
            dto.IsKeyWin = game.LastHandResolution.IsKeyWin;
            dto.PointsAwarded = game.LastHandResolution.PointsAwarded;
            dto.AvailableActions = new List<PostHandAction>(game.LastHandResolution.AvailablePostHandActions);
        }

        return dto;
    }

    public static EndGameDto GetEndGameDto(bool normalFinish)
    {
        return new EndGameDto
        {
            NormalFinish = normalFinish
        };
    }

    public static ResponseObjectDto<FriendRequestsDto> GetFriendRequestsDto(List<FriendRequest> friendRequests)
    {
        return new ResponseObjectDto<FriendRequestsDto>
        {
            Object = new FriendRequestsDto
            {
                FriendRequests = friendRequests.Select(fr => new FriendRequestDto
                {
                    Login = fr.UserApplicantName
                }).ToList()
            }
        };
    }

    public static ResponseObjectDto<FriendsDto> GetFriendsDto(List<Friend> friends)
    {
        return new ResponseObjectDto<FriendsDto>
        {
            Object = new FriendsDto
            {
                Friends = friends.Select(f => new FriendDto
                {
                    FriendSince = f.FriendSince,
                    Login = f.Name
                }).ToList()
            }
        };
    }

    public static PlayerPlayDto GetPlayerPlayPlaceDominoDto(Game game)
    {
        return new PlayerPlayDto
        {
            IdUserPlayed = game.LastGameMove.PlayerId,
            PlayActionType = PlayActionType.PlaceDomino,
            Moves = new MoveDto
            {
                Domino = new DominoDto
                {
                    Side1 = game.LastGameMove.Domino.Side1,
                    Side2 = game.LastGameMove.Domino.Side2
                },
                Col = game.LastGameMove.Col,
                Row = game.LastGameMove.Row,
                Orientation = game.LastGameMove.Orientation,
                PlayerId = game.LastGameMove.PlayerId
            }
        };
    }

    public static HandDto GetHandDto(GamePlayer player)
    {
        return new HandDto
        {
            Dominoes = player.Hand.Select(d => new DominoDto
            {
                Side1 = d.Side1,
                Side2 = d.Side2,
            }).ToList()
        };
    }

    public static HandDto GetHandDto(Game game, GamePlayer player)
    {
        HandDto dto = GetHandDto(player);
        if (!game.IsLegacyDrawAdvance)
        {
            dto.MatchId = game.Id;
            dto.LobbyId = game.LobbyId > 0 ? game.LobbyId : game.Id;
            dto.ReconnectToken = player.ReconnectToken;
        }
        return dto;
    }

    public static ResponseObjectDto<ActiveMatchCheckResponseDto> GetActiveMatchCheckResponseDto(
        Game? game,
        long playerId,
        int remainingGraceSeconds)
    {
        if (game is null)
        {
            return new ResponseObjectDto<ActiveMatchCheckResponseDto>
            {
                OkResult = true,
                Object = new ActiveMatchCheckResponseDto
                {
                    HasActiveMatch = false,
                    PlayerId = playerId,
                    RemainingGraceSeconds = 0
                }
            };
        }

        return new ResponseObjectDto<ActiveMatchCheckResponseDto>
        {
            OkResult = true,
            Object = new ActiveMatchCheckResponseDto
            {
                HasActiveMatch = true,
                MatchId = game.Id,
                LobbyId = game.LobbyId > 0 ? game.LobbyId : game.Id,
                PlayerId = playerId,
                RemainingGraceSeconds = remainingGraceSeconds,
                MatchFormat = game.MatchFormat,
                TableStyle = game.TableStyle,
                MatchTitle = game.MatchTitle
            }
        };
    }

    public static ResponseObjectDto<RejoinMatchSnapshotDto> GetRejoinMatchSnapshotDto(
        Game game,
        GamePlayer player,
        Guid authToken)
    {
        return new ResponseObjectDto<RejoinMatchSnapshotDto>
        {
            OkResult = true,
            Object = new RejoinMatchSnapshotDto
            {
                MatchId = game.Id,
                LobbyId = game.LobbyId > 0 ? game.LobbyId : game.Id,
                PlayerId = player.Id,
                ReconnectToken = player.ReconnectToken,
                AuthToken = authToken.ToString(),
                User = new UserDto
                {
                    Id = player.Id,
                    Login = player.Login,
                    Email = player.Email,
                    Score = player.Score,
                    BirthDate = player.Birthdate.ToString(Constants.ShortFormatDate),
                    DateSignup = player.DateSignup.ToString(Constants.LongFormatDate),
                    Nationality = player.Nationality
                },
                MatchTitle = game.MatchTitle,
                MatchFormat = game.MatchFormat,
                TableStyle = game.TableStyle,
                OneOnePlayoff = game.OneOnePlayoff,
                HandNumber = game.HandNumber,
                CurrentTurnPlayerId = game.PlayerPlaying.Id,
                TurnTimer = GetTimerDto(game.PlayerTurnTimer),
                IsAutoPassTurn = game.IsCurrentPlayerAutoPassTurn(),
                BoardMoves = game.MoveHistory.MoveHistory.Select(m => new MoveDto
                {
                    Domino = new DominoDto { Side1 = m.Domino.Side1, Side2 = m.Domino.Side2 },
                    Row = m.Row,
                    Col = m.Col,
                    Orientation = m.Orientation,
                    PlayerId = m.PlayerId
                }).ToList(),
                Hand = player.Hand.Select(d => new DominoDto { Side1 = d.Side1, Side2 = d.Side2 }).ToList(),
                PossibleMoves = game.PlayerPlaying.Id == player.Id ? GetPlayerPossibleMovesDto(game) : null,
                NumberOfDominoPerPlayer = game.GetNumberOfDominoAllPlayer(),
                TeamScores = new Dictionary<int, int>(game.TeamScores),
                PlayerScores = new Dictionary<long, int>(game.PlayerScores),
                Players = game.Players.Select(p => new PlayerDto
                {
                    Id = p.Id,
                    Name = p.Login,
                    Elo = p.Score
                }).ToList()
            }
        };
    }

    public static ChatDto GetChatDto(Chat chat)
    {
        return new ChatDto
        {
            Id = chat.Id,
            Name = chat.Name
        };
    }

    public static ResponseObjectDto<OtherUserInfoDto> GetOtherUserInfoDto(User user)
    {
        return new ResponseObjectDto<OtherUserInfoDto>
        {
            Object = new OtherUserInfoDto
            {
                Login = user.Login,
                Score = user.Score,
                Nationality = user.Nationality,
                DateSignup = user.DateSignup.ToString(Constants.LongFormatDate),
                Statistic = new StatisticDto
                {
                    NumberOfGamePlayed = user.Statistics!.NumberOfGamePlayed,
                    NumberOfGameWon = user.Statistics.NumberOfGameWon,
                    NumberOfGameLost = user.Statistics.NumberOfGameLost
                }
            }
        };
    }

    public static ResponseObjectDto<AllHistoryGamesDto> GetAllHistoryGamesDto(List<HistoryGame> historyGames)
    {
        return new ResponseObjectDto<AllHistoryGamesDto>
        {
            Object = new AllHistoryGamesDto
            {
                HistoryGames = historyGames.Select(h => new ShortHistoryGameDto
                {
                    Id = h.Id,
                    EndTime = h.EndTime.ToString(Constants.LongFormatDate),
                    Ranked = h.Mode == GameMode.Ranked
                }).ToList()
            }
        };
    }

    public static ResponseObjectDto<HistoryGameDto> GetHistoryGameDto(HistoryGame historyGame)
    {
        return new ResponseObjectDto<HistoryGameDto>
        {
            Object = new HistoryGameDto
            {
                Id = historyGame.Id,
                StartTime = historyGame.StartTime.ToString(Constants.LongFormatDate),
                EndTime = historyGame.EndTime.ToString(Constants.LongFormatDate),
                Ranked = historyGame.Mode == GameMode.Ranked,
                TotalBet = historyGame.TotalBet,
                WinnerId = historyGame.WinnerId,
                GeneralTimer = historyGame.GeneralTimer,
                PlayerTurnTimer = historyGame.PlayerTurnTimer,
                Moves = historyGame.Moves.Select(m => new MoveDto
                {
                    Domino = new DominoDto
                    {
                        Side1 = m.Domino.Side1,
                        Side2 = m.Domino.Side2,
                    },
                    Row = m.Row,
                    Col = m.Col,
                    Orientation = m.Orientation,
                    PlayerId = m.PlayerId
                }).ToList(),
                Players = historyGame.Players.Select(p => new HistoryGamePlayerDto
                {
                    Id = p.Id,
                    Name = p.Login,
                    Elo = p.Score,
                    Gain = p.Gain
                }).ToList()
            }
        };
    }
}
