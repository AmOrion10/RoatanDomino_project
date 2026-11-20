using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using UnityEngine;

public class OfflineBotClient : IClient
{
    private const int HumanPlayerId = 1;
    private const int OfflineChatId = 999;
    private const int GeneralMatchDurationSeconds = 600;
    private const int PlayerTurnDurationSeconds = 60;

    private static readonly string[] BotNames = { "Mateo", "Sofia", "Carlos" };

    public MessageFrameEventHandler EventHandlers { get; } = new MessageFrameEventHandler();
    public UserDto User { get; }
    public List<FriendDto> Friends { get; } = new List<FriendDto>();
    public List<FriendRequestDto> FriendRequests { get; } = new List<FriendRequestDto>();

    public BotDifficulty Difficulty { get; }
    public int BotCount { get; }
    public LobbyDto OfflineLobby { get; }
    public int BoneyardCount => engine != null ? engine.BoneyardCount : 0;
    public bool IsMatchEnded { get; private set; }
    public bool IsHumanTurnWaitingForMove { get; private set; }
    public string LastStatusMessage { get; private set; } = "Starting offline match...";

    public event Action<string> StatusUpdated;
    public event Action<EndGameDto, List<OfflineGameEngine.OfflinePlayer>> MatchEndedWithDetails;

    private OfflineGameEngine engine;
    private DominoBotAI botAi;
    private CancellationTokenSource cts;
    private bool matchStarted;

    public OfflineBotClient(BotDifficulty difficulty, int botCount = 3)
    {
        Difficulty = difficulty;
        BotCount = Mathf.Clamp(botCount, 1, 3);

        User = new UserDto
        {
            Id = HumanPlayerId,
            Email = "offline@roatandomino.local",
            Login = "You",
            DateSignup = DateTime.Now.ToString("dd/MM/yyyy"),
            Nationality = "Honduras",
            BirthDate = "01/01/2000",
            Score = 1000
        };

        List<PlayerDto> lobbyPlayers = new List<PlayerDto>
        {
            new PlayerDto { Id = User.Id, Name = User.Login, Elo = User.Score }
        };

        int botElo = GetBotElo(difficulty);
        string tag = difficulty.ToString();
        for (int i = 0; i < BotCount; i++)
        {
            lobbyPlayers.Add(new PlayerDto
            {
                Id = 100 + i,
                Name = $"{BotNames[i]} ({tag})",
                Elo = botElo
            });
        }

        OfflineLobby = new LobbyDto
        {
            Id = 1,
            Name = $"Offline vs {tag} Bots",
            Creator = lobbyPlayers[0],
            Players = lobbyPlayers,
            MaxNumberOfPlayer = lobbyPlayers.Count,
            IsPublic = false,
            Ranked = false,
            Code = "OFFLINE"
        };
    }

    public void Start()
    {
        cts = new CancellationTokenSource();
    }

    public void Stop()
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
    }

    public void AddToSendQueue(List<MessageFrame> frames)
    {
        if (frames == null) return;
        foreach (MessageFrame frame in frames)
        {
            AddToSendQueue(frame);
        }
    }

    public void AddToSendQueue(MessageFrame frame)
    {
        if (frame == null) return;

        switch (frame.CommandType)
        {
            case FrameCommandType.ReadyInGame:
                if (!matchStarted)
                {
                    matchStarted = true;
                    _ = RunMatchIntroAsync(cts.Token);
                }
                break;

            case FrameCommandType.PlayerPlays:
                HandleHumanPlayFrame(frame);
                break;

            case FrameCommandType.LeaveGame:
                Stop();
                EmitFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), FrameCommandType.LeaveGame));
                break;

            case FrameCommandType.GetOtherUserInfo:
                HandleGetOtherUserInfo(frame);
                break;

            case FrameCommandType.SendChatMessage:
                HandleLocalChatMessage(frame);
                break;

            case FrameCommandType.Disconnection:
                Stop();
                EmitFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), FrameCommandType.Disconnection));
                break;
        }
    }

    private async Task RunMatchIntroAsync(CancellationToken token)
    {
        try
        {
            IsMatchEnded = false;
            IsHumanTurnWaitingForMove = false;
            botAi = new DominoBotAI();

            List<OfflineGameEngine.OfflinePlayer> players = new List<OfflineGameEngine.OfflinePlayer>();
            foreach (PlayerDto p in OfflineLobby.Players)
            {
                bool isBot = p.Id != HumanPlayerId;
                players.Add(new OfflineGameEngine.OfflinePlayer
                {
                    Info = p,
                    IsBot = isBot,
                    BotDifficulty = isBot ? (BotDifficulty?)Difficulty : null
                });
            }

            engine = new OfflineGameEngine(players);

            EmitFrame(MessageFrameBuilder.GameStartingTimerFrame(new TimerDto
            {
                Duration = 2,
                HasStarted = true
            }));

            UpdateStatus($"Starting offline match vs {Difficulty} Bots...");
            await Task.Delay(2100, token);

            engine.DistributeDominoes();

            OfflineGameEngine.OfflinePlayer human = engine.Players.First(p => !p.IsBot);
            EmitFrame(MessageFrameBuilder.DominoDistributionFrame(new HandDto
            {
                Dominoes = human.Hand.Select(d => new DominoDto { Side1 = d.Side1, Side2 = d.Side2 }).ToList()
            }));

            EmitFrame(MessageFrameBuilder.JoinChatFrame(new ChatDto
            {
                Id = OfflineChatId,
                Name = $"Offline ({Difficulty})"
            }));

            await Task.Delay(800, token);

            MoveDto firstMove = engine.PlaceFirstDomino();
            OfflineGameEngine.OfflinePlayer starter = engine.Players.First(p => p.Info.Id == firstMove.PlayerId);
            engine.NextTurn();

            EmitFrame(MessageFrameBuilder.GameGeneralTimerFrame(new TimerDto
            {
                Duration = GeneralMatchDurationSeconds,
                HasStarted = true
            }));

            EmitFrame(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
            {
                IdUserPlayed = firstMove.PlayerId,
                PlayActionType = PlayActionType.PlaceDomino,
                Moves = firstMove
            }));

            EmitTurnAndCounts();

            string openerMsg = $"{starter.Info.Name} opened with [{firstMove.Domino.Side1}|{firstMove.Domino.Side2}].";
            SendSystemChat(openerMsg);
            UpdateStatus(openerMsg);

            await DriveTurnsUntilHumanInputOrEndAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OfflineBotClient] Intro error: {ex}");
        }
    }

    private void HandleHumanPlayFrame(MessageFrame frame)
    {
        if (engine == null || IsMatchEnded) return;
        if (engine.CurrentPlayer.IsBot) return;

        PlayerPlayDto dto = frame.GetObjectFromMessage<PlayerPlayDto>();
        if (dto == null) return;

        IsHumanTurnWaitingForMove = false;

        if (dto.PlayActionType == PlayActionType.PlaceDomino)
        {
            if (dto.Moves == null)
            {
                EmitFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Error(ErrorTypes.WrongPositionForDomino), FrameCommandType.PlayerPlays));
                IsHumanTurnWaitingForMove = true;
                return;
            }

            dto.Moves.PlayerId = HumanPlayerId;
            bool placed = engine.PlaceDomino(dto.Moves);
            if (!placed)
            {
                EmitFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Error(ErrorTypes.WrongPositionForDomino), FrameCommandType.PlayerPlays));
                IsHumanTurnWaitingForMove = true;
                return;
            }

            MoveDto placedMove = engine.LastMove;
            engine.NextTurn();

            EmitFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), FrameCommandType.PlayerPlays));
            EmitFrame(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
            {
                IdUserPlayed = HumanPlayerId,
                PlayActionType = PlayActionType.PlaceDomino,
                Moves = placedMove
            }));
            EmitTurnAndCounts();

            UpdateStatus($"You played [{placedMove.Domino.Side1}|{placedMove.Domino.Side2}].");
        }
        else if (dto.PlayActionType == PlayActionType.Draw)
        {
            List<int> openEnds = engine.GetOpenEndPipValues();
            botAi.RecordPlayerBlockedOnEnds(HumanPlayerId, openEnds);

            DominoDto drawn = engine.Draw(HumanPlayerId);
            if (drawn != null)
            {
                botAi.ClearPlayerVoidsOnDraw(HumanPlayerId);
            }
            engine.NextTurn();

            EmitFrame(MessageFrameBuilder.ResponseFrame(ResponseDto.Ok(), FrameCommandType.PlayerPlays));

            if (drawn != null)
            {
                EmitFrame(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
                {
                    IdUserPlayed = HumanPlayerId,
                    PlayActionType = PlayActionType.Draw,
                    Moves = new MoveDto
                    {
                        Domino = new DominoDto { Side1 = drawn.Side1, Side2 = drawn.Side2 },
                        PlayerId = HumanPlayerId
                    }
                }));
                UpdateStatus($"You drew [{drawn.Side1}|{drawn.Side2}].");
            }
            else
            {
                UpdateStatus("Boneyard empty — you passed.");
            }

            EmitTurnAndCounts();
        }

        if (cts != null && !cts.IsCancellationRequested)
        {
            _ = DriveTurnsUntilHumanInputOrEndAsync(cts.Token);
        }
    }

    private async Task DriveTurnsUntilHumanInputOrEndAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (engine.IsGameOver())
                {
                    FinishMatch();
                    return;
                }

                OfflineGameEngine.OfflinePlayer active = engine.CurrentPlayer;
                List<MoveDto> possibleMoves = engine.GetPlayerPossibleMoves(active);

                if (!active.IsBot)
                {
                    if (possibleMoves.Count > 0)
                    {
                        IsHumanTurnWaitingForMove = true;
                        EmitFrame(MessageFrameBuilder.PossibleMovesFrame(new PossibleMovesDto
                        {
                            Moves = possibleMoves
                        }));
                        UpdateStatus($"Your turn! Click a highlighted domino to place it. ({possibleMoves.Count} move{(possibleMoves.Count == 1 ? "" : "s")})");
                        return;
                    }

                    // Human has no playable moves — match server auto-draw/pass behavior
                    List<int> blockedEnds = engine.GetOpenEndPipValues();
                    botAi.RecordPlayerBlockedOnEnds(HumanPlayerId, blockedEnds);

                    UpdateStatus(engine.BoneyardCount > 0
                        ? "No playable moves — drawing from boneyard..."
                        : "No playable moves and boneyard is empty — passing turn...");

                    await Task.Delay(1100, token);

                    DominoDto drawn = engine.Draw(HumanPlayerId);
                    if (drawn != null)
                    {
                        botAi.ClearPlayerVoidsOnDraw(HumanPlayerId);
                    }
                    engine.NextTurn();

                    if (drawn != null)
                    {
                        EmitFrame(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
                        {
                            IdUserPlayed = HumanPlayerId,
                            PlayActionType = PlayActionType.Draw,
                            Moves = new MoveDto
                            {
                                Domino = new DominoDto { Side1 = drawn.Side1, Side2 = drawn.Side2 },
                                PlayerId = HumanPlayerId
                            }
                        }));
                        SendSystemChat($"You had no move and drew [{drawn.Side1}|{drawn.Side2}].");
                    }
                    else
                    {
                        SendSystemChat("You had no move and passed.");
                    }

                    EmitTurnAndCounts();
                    continue;
                }

                // Bot turn
                UpdateStatus($"{active.Info.Name} is thinking...");
                await Task.Delay(GetBotThinkDelayMs(Difficulty), token);

                if (possibleMoves.Count > 0)
                {
                    MoveDto chosen = botAi.ChooseMove(engine, active, possibleMoves, Difficulty);
                    if (chosen != null && engine.PlaceDomino(chosen))
                    {
                        MoveDto placed = engine.LastMove;
                        engine.NextTurn();

                        EmitFrame(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
                        {
                            IdUserPlayed = active.Info.Id,
                            PlayActionType = PlayActionType.PlaceDomino,
                            Moves = placed
                        }));
                        EmitTurnAndCounts();

                        string msg = $"{active.Info.Name} played [{placed.Domino.Side1}|{placed.Domino.Side2}].";
                        SendSystemChat(msg);
                        UpdateStatus(msg);
                        continue;
                    }
                }

                // Bot is blocked -> draw or pass
                List<int> ends = engine.GetOpenEndPipValues();
                botAi.RecordPlayerBlockedOnEnds(active.Info.Id, ends);

                DominoDto botDrawn = engine.Draw(active.Info.Id);
                if (botDrawn != null)
                {
                    botAi.ClearPlayerVoidsOnDraw(active.Info.Id);
                }
                engine.NextTurn();

                if (botDrawn != null)
                {
                    EmitFrame(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
                    {
                        IdUserPlayed = active.Info.Id,
                        PlayActionType = PlayActionType.Draw
                    }));
                    string msg = $"{active.Info.Name} drew a domino.";
                    SendSystemChat(msg);
                    UpdateStatus(msg);
                }
                else
                {
                    string msg = $"{active.Info.Name} passed.";
                    SendSystemChat(msg);
                    UpdateStatus(msg);
                }

                EmitTurnAndCounts();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OfflineBotClient] Turn loop error: {ex}");
        }
    }

    private void FinishMatch()
    {
        IsMatchEnded = true;
        IsHumanTurnWaitingForMove = false;

        EndGameDto endDto = engine.BuildEndGameDto();
        List<OfflineGameEngine.OfflinePlayer> ranked = engine.GetRankings();
        OfflineGameEngine.OfflinePlayer winner = ranked[0];

        string summary = winner.Info.Id == HumanPlayerId
            ? $"You won against {Difficulty} Bots!"
            : $"{winner.Info.Name} won the match!";

        SendSystemChat(summary);
        UpdateStatus(summary);

        EmitFrame(MessageFrameBuilder.EndGameFrame(endDto));
        EmitFrame(MessageFrameBuilder.LeaveChatFrame(new ChatDto
        {
            Id = OfflineChatId,
            Name = $"Offline ({Difficulty})"
        }));

        if (MainThread.Instance != null)
        {
            MainThread.Instance.AddActionToQueue(() => MatchEndedWithDetails?.Invoke(endDto, ranked));
        }
    }

    private void EmitTurnAndCounts()
    {
        EmitFrame(MessageFrameBuilder.ChangePlayerTurnFrame(new PlayerTurnDto
        {
            IdUserPlaying = engine.CurrentPlayer.Info.Id,
            Timer = new TimerDto
            {
                Duration = PlayerTurnDurationSeconds,
                HasStarted = true
            }
        }));

        EmitFrame(MessageFrameBuilder.NumberOfDominoPerPlayerFrame(new NumberOfDominoPerPlayerDto
        {
            NumberOfDomino = engine.GetNumberOfDominoPerPlayer()
        }));
    }

    private void HandleGetOtherUserInfo(MessageFrame frame)
    {
        GetOtherUserInfoDto req = frame.GetObjectFromMessage<GetOtherUserInfoDto>();
        PlayerDto botPlayer = OfflineLobby.Players.FirstOrDefault(p => p.Name == req.Login);
        if (botPlayer == null) return;

        int wins = Difficulty == BotDifficulty.Beginner ? 12 : (Difficulty == BotDifficulty.Novice ? 48 : 165);
        int losses = Difficulty == BotDifficulty.Beginner ? 28 : (Difficulty == BotDifficulty.Novice ? 34 : 42);

        ResponseObjectDto<OtherUserInfoDto> resp = new ResponseObjectDto<OtherUserInfoDto>
        {
            OkResult = true,
            Object = new OtherUserInfoDto
            {
                Login = botPlayer.Name,
                Score = botPlayer.Elo,
                DateSignup = "Bot AI",
                Nationality = "Honduras",
                Statistic = new StatisticDto
                {
                    NumberOfGamePlayed = wins + losses,
                    NumberOfGameWon = wins,
                    NumberOfGameLost = losses
                }
            }
        };

        EmitFrame(MessageFrameBuilder.ResponseFrame(resp, FrameCommandType.GetOtherUserInfo));
    }

    private void HandleLocalChatMessage(MessageFrame frame)
    {
        SendChatMessageDto dto = frame.GetObjectFromMessage<SendChatMessageDto>();
        if (dto == null || string.IsNullOrWhiteSpace(dto.Message)) return;

        EmitFrame(MessageFrameBuilder.ReceiveMessageFrame(new ReceiveChatMessageDto
        {
            ChatId = dto.ChatId,
            Sender = User.Login,
            Message = dto.Message
        }));
    }

    private void SendSystemChat(string message)
    {
        EmitFrame(MessageFrameBuilder.ReceiveMessageFrame(new ReceiveChatMessageDto
        {
            ChatId = OfflineChatId,
            Sender = "Referee",
            Message = message
        }));
    }

    private void UpdateStatus(string message)
    {
        LastStatusMessage = message;
        if (MainThread.Instance != null)
        {
            MainThread.Instance.AddActionToQueue(() => StatusUpdated?.Invoke(message));
        }
    }

    private void EmitFrame(MessageFrame frame)
    {
        if (MainThread.Instance != null)
        {
            MainThread.Instance.AddActionToQueue(() => EventHandlers.MessageFrameReceive(frame));
        }
        else
        {
            EventHandlers.MessageFrameReceive(frame);
        }
    }

    private static int GetBotElo(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Beginner: return 800;
            case BotDifficulty.Novice: return 1250;
            case BotDifficulty.Pro: return 1800;
            default: return 1000;
        }
    }

    private static int GetBotThinkDelayMs(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Beginner: return 800;
            case BotDifficulty.Novice: return 1000;
            case BotDifficulty.Pro: return 1150;
            default: return 900;
        }
    }
}
