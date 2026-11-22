using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class RoatanOnlineController : MonoBehaviour
{
    private const string PrefHostKey = "RoatanDomino_ServerHost";
    private const string PrefPortKey = "RoatanDomino_ServerPort";

    private TMP_FontAsset uiFont;
    private Action onReturnHome;
    private Action onRejoinEnteredMatch;

    private GameObject onlineRoot;
    private GameObject authView;
    private GameObject hubView;
    private GameObject lobbyView;
    private GameObject onlineGameView;

    // Startup / Lobby Rejoin Modal ("Match in Progress — Rejoin?")
    private GameObject rejoinModal;
    private TMP_Text rejoinModalBodyLabel;
    private TMP_Text rejoinModalTimerLabel;
    private Button rejoinConfirmBtn;
    private RoatanMatchSessionStore.ActiveMatchSessionData pendingSavedSession;
    private bool waitingForConnectToCheckRejoin;
    private bool rejoinModalActive;
    private float rejoinModalRemainingSeconds;

    // Header & status
    private TMP_Text titleLabel;
    private TMP_Text backBtnLabel;
    private TMP_Text connectionBadgeText;
    private Image connectionBadgeBg;
    private TMP_Text feedbackToast;

    // Auth view
    private TMP_InputField hostInput;
    private TMP_InputField portInput;
    private TMP_Text connectBtnLabel;
    private Image signInTabImg;
    private Image registerTabImg;
    private TMP_Text signInTabLabel;
    private TMP_Text registerTabLabel;
    private GameObject signInPanel;
    private GameObject registerPanel;
    private TMP_InputField loginUserInput;
    private TMP_InputField loginPassInput;
    private TMP_InputField regEmailInput;
    private TMP_InputField regUserInput;
    private TMP_InputField regPassInput;
    private TMP_InputField regBirthInput;
    private TMP_Text signInSubmitLabel;
    private TMP_Text registerSubmitLabel;

    // Hub view
    private TMP_Text hubWelcomeLabel;
    private TMP_Text signOutBtnLabel;
    private TMP_Text quickRankedLabel;
    private TMP_Text createLobbyBtnLabel;
    private TMP_Text joinCodeBtnLabel;
    private TMP_Text refreshLobbiesBtnLabel;
    private TMP_InputField createLobbyNameInput;
    private TMP_InputField joinCodeInput;
    private RectTransform lobbiesListContent;
    private readonly List<GameObject> spawnedLobbyRows = new List<GameObject>();
    private int createLobbyMaxPlayers = 4;
    private TableStyle createTableStyle = TableStyle.BLOCK;
    private MatchFormat createMatchFormat = MatchFormat.SIX_LOV;
    private bool createOneOnePlayoff = false;
    private readonly Dictionary<int, Image> createMaxPlayerBtns = new Dictionary<int, Image>();
    private readonly Dictionary<int, GameObject> createMaxPlayerObjs = new Dictionary<int, GameObject>();
    private readonly Dictionary<TableStyle, Image> createTableStyleBtns = new Dictionary<TableStyle, Image>();
    private readonly Dictionary<MatchFormat, Image> createMatchFormatBtns = new Dictionary<MatchFormat, Image>();
    private Button createOneOnePlayoffBtn;
    private Image createOneOnePlayoffImg;
    private TMP_Text createOneOnePlayoffLabel;

    // In-Lobby view
    private TMP_Text lobbyTitleLabel;
    private TMP_Text lobbyInfoLabel;
    private TMP_Text lobbyTimerLabel;
    private RectTransform lobbyPlayersContent;
    private readonly List<GameObject> spawnedLobbyPlayerRows = new List<GameObject>();
    private TMP_Text readyBtnLabel;
    private Image readyBtnImg;
    private TMP_Text leaveLobbyBtnLabel;
    private bool localReady;
    private LobbyDto currentLobby;

    // Online Match View (Roatan Tropical Tabletop!)
    private TMP_Text onlineMatchHeader;
    private TMP_Text onlineMatchStatus;
    private RectTransform onlineBoardContent;
    private RectTransform onlineTilesLayer;
    private RectTransform onlineSlotsLayer;
    private RectTransform onlineHandContent;
    private Button onlineDrawBtn;
    private TMP_Text onlineDrawBtnLabel;
    private GameObject onlineTimerBadgeObj;
    private Image onlineTimerBadgeBg;
    private TMP_Text onlineTimerLabel;
    private GameObject onlineBotIndicatorObj;
    private Button onlineResumeControlBtn;
    private GameObject onlineEndOverlay;
    private TMP_Text onlineEndTitleLabel;
    private TMP_Text onlineEndBodyLabel;
    private Button onlineEndPrimaryBtn;
    private TMP_Text onlineEndPrimaryBtnLabel;
    private Button onlineEndNewMatchBtn;
    private readonly List<GameObject> onlineBoardObjs = new List<GameObject>();
    private readonly List<GameObject> onlineSlotObjs = new List<GameObject>();
    private readonly List<GameObject> onlineHandObjs = new List<GameObject>();
    private readonly List<DominoDto> onlineHand = new List<DominoDto>();
    private PossibleMovesDto onlinePossibleMoves;
    private DominoDto onlineSelectedDomino;
    private long onlineActiveUserId = -1;
    private readonly GameGridDto onlineGridValue = new GameGridDto(60, 60);

    // Online Match Resilience state (5s Auto-Pass, 10s Turn Timer, 3-Strike Bot Takeover & Instant Reclaim)
    private bool onlineTurnTimerActive;
    private bool onlineIsAutoPassTurn;
    private float onlineTurnTimeRemaining;
    private int onlineConsecutiveAutoPlays;
    private bool onlineIsBotControlled;
    private float onlineBotPacingRemaining;
    private bool onlineTurnActionDispatched;

    // Online Pre-Deal 28-Tile Shuffle & Mix state (pauses turn timer until shuffle completes)
    private bool onlineShufflePlaying;
    private Coroutine onlineShuffleRoutine;
    private readonly List<GameObject> onlineShuffleTiles = new List<GameObject>();
    private readonly List<PlayerPlayDto> pendingOnlinePlaysDuringShuffle = new List<PlayerPlayDto>();
    private PlayerTurnDto pendingOnlineTurnDuringShuffle;
    private PossibleMovesDto pendingOnlineMovesDuringShuffle;

    private IClient subscribedClient;
    private bool isConnecting;
    private bool isSignedIn;

    public void Initialize(RectTransform safeRoot, TMP_FontAsset font, Action returnHomeCallback, Action onRejoinEnteredMatchCallback = null)
    {
        uiFont = font;
        onReturnHome = returnHomeCallback;
        onRejoinEnteredMatch = onRejoinEnteredMatchCallback;

        BuildOnlineScreen(safeRoot);
        BuildRejoinModal(safeRoot);
        RoatanLocalization.LanguageChanged += RefreshLocalizedTexts;
        RefreshLocalizedTexts();
        onlineRoot.SetActive(false);
    }

    /// <summary>
    /// Checks PlayerPrefs on app startup / lobby initialization for an unexpired active match session
    /// and sends a lightweight CheckActiveMatch handshake to DominoServer.
    /// </summary>
    public void CheckPendingRejoinOnStartup()
    {
        RoatanMatchSessionStore.ActiveMatchSessionData saved = RoatanMatchSessionStore.LoadValidSession();
        if (saved == null || Networking.Instance == null)
        {
            return;
        }

        pendingSavedSession = saved;
        Networking.Instance.ConnectionEvent -= OnConnectionStatusChanged;
        Networking.Instance.ConnectionEvent += OnConnectionStatusChanged;
        SubscribeToCurrentClient();

        if (Networking.Instance.IsConnected && !Networking.Instance.IsOfflineMode && subscribedClient != null)
        {
            waitingForConnectToCheckRejoin = false;
            SendCheckActiveMatch(saved);
        }
        else if (!isConnecting)
        {
            waitingForConnectToCheckRejoin = true;
            isConnecting = true;
            string host = saved.ServerHost;
            int port = saved.ServerPort;
            Task.Run(() => Networking.Instance.ConnectOnline(host, port, false));
        }
    }

    private void SendCheckActiveMatch(RoatanMatchSessionStore.ActiveMatchSessionData session)
    {
        if (subscribedClient == null || session == null) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.CheckActiveMatchFrame(new RejoinSessionRequestDto
        {
            MatchId = session.MatchId,
            LobbyId = session.LobbyId,
            PlayerId = session.PlayerId,
            ReconnectToken = session.ReconnectToken
        }));
    }

    private void OnDestroy()
    {
        RoatanLocalization.LanguageChanged -= RefreshLocalizedTexts;
        UnsubscribeClient();
        if (Networking.Instance != null)
        {
            Networking.Instance.ConnectionEvent -= OnConnectionStatusChanged;
        }
    }

    public void ShowOnline()
    {
        onlineRoot.SetActive(true);
        if (Networking.Instance != null)
        {
            Networking.Instance.StopOfflineMode();
            Networking.Instance.ConnectionEvent -= OnConnectionStatusChanged;
            Networking.Instance.ConnectionEvent += OnConnectionStatusChanged;
            SubscribeToCurrentClient();
        }

        if (isSignedIn && Networking.Instance != null && Networking.Instance.IsConnected)
        {
            ShowSubView(hubView);
            RequestLobbiesList();
        }
        else
        {
            ShowSubView(authView);
        }

        RefreshConnectionBadge();
        RefreshLocalizedTexts();
        CheckPendingRejoinOnStartup();
    }

    public void HideOnline()
    {
        CancelOnlineShuffle();
        ChatManager.Instance?.SetActiveRoundChatLocked(false);
        onlineRoot.SetActive(false);
    }

    private void CancelOnlineShuffle()
    {
        onlineShufflePlaying = false;
        if (onlineShuffleRoutine != null)
        {
            StopCoroutine(onlineShuffleRoutine);
            onlineShuffleRoutine = null;
        }
        foreach (GameObject go in onlineShuffleTiles)
        {
            if (go != null) Destroy(go);
        }
        onlineShuffleTiles.Clear();
        pendingOnlinePlaysDuringShuffle.Clear();
        pendingOnlineTurnDuringShuffle = null;
        pendingOnlineMovesDuringShuffle = null;
        RoatanSoundFX.Instance?.StopShuffleAndMix();
    }

    // ==================== NETWORKING & EVENTS ====================

    private void SubscribeToCurrentClient()
    {
        UnsubscribeClient();
        if (Networking.Instance == null) return;

        subscribedClient = Networking.Instance.GetClient();
        if (subscribedClient == null) return;

        MessageFrameEventHandler eh = subscribedClient.EventHandlers;
        eh.ConnectionResponseEvent += OnLoginResponse;
        eh.RegistrationResponseEvent += OnRegisterResponse;
        eh.DisconnectionResponseEvent += OnLogoutResponse;
        eh.GetAllLobbiesResponseEvent += OnGetAllLobbiesResponse;
        eh.CreateLobbyResponseEvent += OnLobbyEnteredResponse;
        eh.JoinLobbyResponseEvent += OnLobbyEnteredResponse;
        eh.JoinLobbyWithCodeResponseEvent += OnJoinCodeResponse;
        eh.PlayerJoinedLobbyEvent += OnPlayerJoinedLobby;
        eh.PlayerLeftLobbyEvent += OnPlayerLeftLobby;
        eh.LobbyPlayerReadyStateChangedEvent += OnLobbyReadyChanged;
        eh.LobbyTimerUpdatedEvent += OnLobbyTimerUpdated;
        eh.LobbyTimerExpiredEvent += OnLobbyTimerExpired;
        eh.DominoDistrubitionEvent += OnOnlineHandDistributed;
        eh.PlayerPlaysEvent += OnOnlinePlayerPlays;
        eh.ChangePlayerTurnEvent += OnOnlineTurnChanged;
        eh.PossiblesMovesEvent += OnOnlinePossibleMoves;
        eh.NumberOfDominoPerPlayerEvent += OnOnlineCountsUpdated;
        eh.EndGameEvent += OnOnlineEndGame;
        eh.LeaveGameResponseEvent += OnOnlineLeaveGameResponse;
        eh.CheckActiveMatchResponseEvent += OnCheckActiveMatchResponse;
        eh.RejoinMatchResponseEvent += OnRejoinMatchResponse;
        eh.AbandonMatchResponseEvent += OnAbandonMatchResponse;
    }

    private void UnsubscribeClient()
    {
        if (subscribedClient == null) return;

        MessageFrameEventHandler eh = subscribedClient.EventHandlers;
        eh.ConnectionResponseEvent -= OnLoginResponse;
        eh.RegistrationResponseEvent -= OnRegisterResponse;
        eh.DisconnectionResponseEvent -= OnLogoutResponse;
        eh.GetAllLobbiesResponseEvent -= OnGetAllLobbiesResponse;
        eh.CreateLobbyResponseEvent -= OnLobbyEnteredResponse;
        eh.JoinLobbyResponseEvent -= OnLobbyEnteredResponse;
        eh.JoinLobbyWithCodeResponseEvent -= OnJoinCodeResponse;
        eh.PlayerJoinedLobbyEvent -= OnPlayerJoinedLobby;
        eh.PlayerLeftLobbyEvent -= OnPlayerLeftLobby;
        eh.LobbyPlayerReadyStateChangedEvent -= OnLobbyReadyChanged;
        eh.LobbyTimerUpdatedEvent -= OnLobbyTimerUpdated;
        eh.LobbyTimerExpiredEvent -= OnLobbyTimerExpired;
        eh.DominoDistrubitionEvent -= OnOnlineHandDistributed;
        eh.PlayerPlaysEvent -= OnOnlinePlayerPlays;
        eh.ChangePlayerTurnEvent -= OnOnlineTurnChanged;
        eh.PossiblesMovesEvent -= OnOnlinePossibleMoves;
        eh.NumberOfDominoPerPlayerEvent -= OnOnlineCountsUpdated;
        eh.EndGameEvent -= OnOnlineEndGame;
        eh.LeaveGameResponseEvent -= OnOnlineLeaveGameResponse;
        eh.CheckActiveMatchResponseEvent -= OnCheckActiveMatchResponse;
        eh.RejoinMatchResponseEvent -= OnRejoinMatchResponse;
        eh.AbandonMatchResponseEvent -= OnAbandonMatchResponse;
        subscribedClient = null;
    }

    private void AttemptServerConnect()
    {
        if (isConnecting || Networking.Instance == null) return;
        RoatanSoundFX.Instance?.PlayTap();

        string host = string.IsNullOrWhiteSpace(hostInput.text) ? GetDefaultHost() : hostInput.text.Trim();
        int port = 10000;
        int.TryParse(portInput.text, out port);
        if (port <= 0) port = 10000;

        PlayerPrefs.SetString(PrefHostKey, host);
        PlayerPrefs.SetInt(PrefPortKey, port);
        PlayerPrefs.Save();

        isConnecting = true;
        SetFeedback(RoatanLocalization.T("connecting"), RoatanVisualAssets.IslandGold);
        RefreshConnectionBadge();

        Task.Run(() => Networking.Instance.ConnectOnline(host, port, false));
    }

    private void OnConnectionStatusChanged()
    {
        isConnecting = false;
        SubscribeToCurrentClient();
        RefreshConnectionBadge();

        if (Networking.Instance != null && Networking.Instance.IsConnected)
        {
            SetFeedback(RoatanLocalization.T("server_connected"), RoatanVisualAssets.PalmGreen);
            if (waitingForConnectToCheckRejoin && pendingSavedSession != null)
            {
                waitingForConnectToCheckRejoin = false;
                SendCheckActiveMatch(pendingSavedSession);
            }
        }
        else
        {
            waitingForConnectToCheckRejoin = false;
            SetFeedback(RoatanLocalization.T("server_offline"), RoatanVisualAssets.SunsetCoral);
        }
    }

    private void SubmitLogin()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest()) return;

        subscribedClient.AddToSendQueue(MessageFrameBuilder.ConnectionFrame(new LoggingDto
        {
            Login = loginUserInput.text.Trim(),
            Password = loginPassInput.text
        }));
        SetFeedback("Signing in...", RoatanVisualAssets.IslandGold);
    }

    private void SubmitRegister()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest()) return;

        subscribedClient.AddToSendQueue(MessageFrameBuilder.RegistrationFrame(new RegistrationDto
        {
            Email = regEmailInput.text.Trim(),
            Login = regUserInput.text.Trim(),
            Password = regPassInput.text,
            Birthdate = string.IsNullOrWhiteSpace(regBirthInput.text) ? "01/01/2000" : regBirthInput.text.Trim()
        }));
        SetFeedback("Creating account...", RoatanVisualAssets.IslandGold);
    }

    private void SubmitLogout()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (subscribedClient != null)
        {
            subscribedClient.AddToSendQueue(MessageFrameBuilder.DisconnectionFrame());
        }
        isSignedIn = false;
        ShowSubView(authView);
    }

    private void RequestLobbiesList()
    {
        if (!EnsureConnectedForRequest()) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetAllLobbyFrame());
    }

    private void RequestQuickRanked()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest()) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinRankedFrame());
        SetFeedback("Searching for ranked match...", RoatanVisualAssets.IslandGold);
    }

    private void RequestCreateLobby()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest()) return;

        string lobbyName = string.IsNullOrWhiteSpace(createLobbyNameInput.text)
            ? "Roatan Table"
            : createLobbyNameInput.text.Trim();

        int effectiveMaxPlayers = createMatchFormat == MatchFormat.SIX_LOV
            ? 4
            : (createMatchFormat == MatchFormat.TWO_BEST && createLobbyMaxPlayers == 3 ? 2 : createLobbyMaxPlayers);

        subscribedClient.AddToSendQueue(MessageFrameBuilder.CreateLobbyFrame(new CreateLobbyDto
        {
            Name = lobbyName,
            MaxNumberOfPlayer = effectiveMaxPlayers,
            Public = true,
            TableStyle = createTableStyle,
            MatchFormat = createMatchFormat,
            OneOnePlayoff = createMatchFormat == MatchFormat.SIX_LOV ? createOneOnePlayoff : false
        }));
    }

    private void RequestJoinByCode()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest()) return;
        if (string.IsNullOrWhiteSpace(joinCodeInput.text)) return;

        subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyWithCodeFrame(new LobbyCodeDto
        {
            Code = joinCodeInput.text.Trim()
        }));
    }

    private void ToggleLobbyReady()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (subscribedClient == null) return;
        localReady = !localReady;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.ReadyLobbyFrame());
        RefreshLobbyReadyButton();
    }

    private void LeaveCurrentLobby()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (subscribedClient != null)
        {
            subscribedClient.AddToSendQueue(MessageFrameBuilder.LeaveLobbyFrame());
        }
        currentLobby = null;
        localReady = false;
        ShowSubView(hubView);
        RequestLobbiesList();
    }

    private bool EnsureConnectedForRequest()
    {
        if (Networking.Instance == null || !Networking.Instance.IsConnected || subscribedClient == null)
        {
            SetFeedback(RoatanLocalization.T("server_offline"), RoatanVisualAssets.SunsetCoral);
            return false;
        }
        return true;
    }

    // ==================== SERVER CALLBACKS ====================

    private void OnLoginResponse(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            isSignedIn = true;
            SetFeedback("Welcome back!", RoatanVisualAssets.PalmGreen);
            subscribedClient.AddToSendQueue(new List<MessageFrame>
            {
                MessageFrameBuilder.ReadyConnectedFrame(),
                MessageFrameBuilder.GetAllLobbyFrame()
            });
            string loginName = subscribedClient.User != null ? subscribedClient.User.Login : loginUserInput.text;
            int elo = subscribedClient.User != null ? subscribedClient.User.Score : 1000;
            hubWelcomeLabel.text = $"{loginName}  •  Elo {elo}";
            ShowSubView(hubView);
        }
        else
        {
            string err = dto.ErrorType.HasValue ? dto.ErrorType.Value.ToString() : "Login failed";
            SetFeedback($"Sign in failed: {err}", RoatanVisualAssets.SunsetCoral);
        }
    }

    private void OnRegisterResponse(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            SetFeedback("Account created! You can now Sign In.", RoatanVisualAssets.PalmGreen);
            SelectAuthTab(isSignIn: true);
            loginUserInput.text = regUserInput.text;
        }
        else
        {
            string err = dto.ErrorType.HasValue ? dto.ErrorType.Value.ToString() : "Registration failed";
            SetFeedback($"Registration error: {err}", RoatanVisualAssets.SunsetCoral);
        }
    }

    private void OnLogoutResponse(ResponseDto dto)
    {
        isSignedIn = false;
        ShowSubView(authView);
    }

    private void OnGetAllLobbiesResponse(ResponseObjectDto<LobbiesInfoDto> dto)
    {
        foreach (GameObject row in spawnedLobbyRows) Destroy(row);
        spawnedLobbyRows.Clear();

        if (!dto.OkResult || dto.Object == null || dto.Object.Lobbies == null || dto.Object.Lobbies.Count == 0)
        {
            GameObject emptyRow = CreateCard(lobbiesListContent, "EmptyRow", new Color(0.08f, 0.21f, 0.27f, 0.7f), Vector2.zero, new Vector2(860f, 84f));
            emptyRow.AddComponent<LayoutElement>().preferredHeight = 84f;
            CreateText(emptyRow.transform, "EmptyLbl", "No public lobbies open yet — create one above!", 24, FontStyles.Italic, new Color(0.82f, 0.92f, 0.95f), Vector2.zero, new Vector2(820f, 64f));
            spawnedLobbyRows.Add(emptyRow);
            return;
        }

        foreach (LobbyInfoDto lobby in dto.Object.Lobbies)
        {
            LobbyInfoDto captured = lobby;
            GameObject row = CreateCard(lobbiesListContent, $"Lobby_{lobby.Id}", new Color(0.08f, 0.23f, 0.29f, 0.95f), Vector2.zero, new Vector2(860f, 106f));
            row.AddComponent<LayoutElement>().preferredHeight = 106f;

            string modeBadge = FormatLobbyRulesBadge(lobby.MatchFormat, lobby.TableStyle, lobby.OneOnePlayoff);
            CreateText(row.transform, "LName", $"{lobby.Name}  ({lobby.NumberOfPlayer}/{lobby.MaxNumberOfPlayer})", 25, FontStyles.Bold, Color.white, new Vector2(-130f, 18f), new Vector2(560f, 38f)).alignment = TextAlignmentOptions.MidlineLeft;
            CreateText(row.transform, "LMode", modeBadge, 20, FontStyles.Normal, RoatanVisualAssets.IslandGold, new Vector2(-130f, -20f), new Vector2(560f, 34f)).alignment = TextAlignmentOptions.MidlineLeft;
            CreatePillButton(row.transform, "BtnJoin", "Join", RoatanVisualAssets.PalmGreen, new Vector2(315f, 0f), new Vector2(180f, 68f), 25, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyFrame(new JoinLobbyDto { LobbyIdToJoin = captured.Id }));
            });

            spawnedLobbyRows.Add(row);
        }
    }

    private void OnLobbyEnteredResponse(ResponseObjectDto<LobbyDto> dto)
    {
        if (!dto.OkResult || dto.Object == null)
        {
            string err = dto.ErrorType.HasValue ? dto.ErrorType.Value.ToString() : "Unable to enter lobby";
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            return;
        }

        currentLobby = dto.Object;
        localReady = false;
        InLobby.SetOfflineLobby(currentLobby);
        ShowSubView(lobbyView);
        RefreshLobbyView();
    }

    private void OnJoinCodeResponse(ResponseDto dto)
    {
        if (!dto.OkResult)
        {
            string err = dto.ErrorType.HasValue ? dto.ErrorType.Value.ToString() : "Invalid lobby code";
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
        }
    }

    private void OnPlayerJoinedLobby(PlayerDto player)
    {
        if (currentLobby == null) return;
        if (!currentLobby.Players.Any(p => p.Id == player.Id))
        {
            currentLobby.Players.Add(player);
        }
        RefreshLobbyView();
    }

    private void OnPlayerLeftLobby(PlayerDto player)
    {
        if (currentLobby == null) return;
        currentLobby.Players.RemoveAll(p => p.Id == player.Id);
        RefreshLobbyView();
    }

    private void OnLobbyReadyChanged(PlayerReadyDto dto)
    {
        if (subscribedClient?.User != null && dto.PlayerId == subscribedClient.User.Id)
        {
            localReady = dto.IsReady;
            RefreshLobbyReadyButton();
        }
    }

    private void OnLobbyTimerUpdated(TimerDto dto)
    {
        if (lobbyTimerLabel != null)
        {
            lobbyTimerLabel.text = dto.HasStarted ? $"Match starting in {dto.Duration}s..." : "Waiting for all players to be Ready...";
        }
    }

    private void OnLobbyTimerExpired()
    {
        EnterOnlineMatchTable();
    }

    // ==================== ONLINE MATCH ON ROATAN TABLETOP ====================

    private void EnterOnlineMatchTable()
    {
        CancelOnlineShuffle();
        ClearOnlineBoardVisuals();
        onlineHand.Clear();
        onlinePossibleMoves = null;
        onlineSelectedDomino = null;
        onlineActiveUserId = -1;
        onlineTurnTimerActive = false;
        onlineIsAutoPassTurn = false;
        onlineTurnTimeRemaining = 0f;
        onlineConsecutiveAutoPlays = 0;
        onlineIsBotControlled = false;
        onlineBotPacingRemaining = 0f;
        onlineTurnActionDispatched = false;
        if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);
        if (onlineEndOverlay != null) onlineEndOverlay.SetActive(false);
        RefreshOnlineTakeoverIndicators();

        for (int r = 0; r < 60; r++)
        {
            for (int c = 0; c < 60; c++)
            {
                onlineGridValue[r][c] = GameGridDto.EmptySpotValue;
            }
        }

        ChatManager.Instance?.SetActiveRoundChatLocked(true);
        ShowSubView(onlineGameView);
        onlineMatchHeader.text = currentLobby != null ? $"ONLINE  •  {currentLobby.Name}" : "ONLINE MATCH";
        onlineMatchStatus.text = "Waiting for server to deal dominoes...";
        subscribedClient?.AddToSendQueue(MessageFrameBuilder.ReadyInGameFrame());
    }

    private void Update()
    {
        // Live countdown inside the "Match in Progress — Rejoin?" modal
        if (rejoinModalActive && rejoinModal != null && rejoinModal.activeSelf)
        {
            rejoinModalRemainingSeconds -= Time.deltaTime;
            int sec = Mathf.Max(0, Mathf.CeilToInt(rejoinModalRemainingSeconds));
            if (rejoinModalTimerLabel != null)
            {
                rejoinModalTimerLabel.text = sec > 0
                    ? $"Time remaining to rejoin: {sec}s"
                    : "Rejoin window expired — seat converted to bot.";
                rejoinModalTimerLabel.color = sec <= 10 ? RoatanVisualAssets.SunsetCoral : RoatanVisualAssets.IslandGold;
            }
            if (rejoinModalRemainingSeconds <= 0f)
            {
                rejoinModalActive = false;
                if (rejoinConfirmBtn != null) rejoinConfirmBtn.interactable = false;
                RoatanMatchSessionStore.ClearSession();
                pendingSavedSession = null;
            }
        }

        if (onlineGameView == null || !onlineGameView.activeSelf) return;
        if (onlineShufflePlaying) return;
        if (subscribedClient?.User == null || onlineActiveUserId != subscribedClient.User.Id) return;
        if (onlineTurnActionDispatched) return;

        if (onlineIsBotControlled)
        {
            if (onlineBotPacingRemaining > 0f)
            {
                onlineBotPacingRemaining -= Time.deltaTime;
                if (onlineBotPacingRemaining <= 0f)
                {
                    ExecuteOnlineAutoTurn(incrementStrike: false);
                }
            }
            return;
        }

        if (!onlineTurnTimerActive) return;

        onlineTurnTimeRemaining -= Time.deltaTime;
        int displaySec = Mathf.Max(0, Mathf.CeilToInt(onlineTurnTimeRemaining));
        UpdateOnlineTimerUI(displaySec);

        if (onlineTurnTimeRemaining <= 0f)
        {
            onlineTurnTimerActive = false;
            if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);

            if (onlineIsAutoPassTurn)
            {
                // 5-second Auto-Pass: MUST NOT increment AFK auto-play counter
                ExecuteOnlineAutoPassNoPenalty();
            }
            else
            {
                // 10-second Turn Timeout: auto-play / auto-draw and increment AFK strike counter (+1)
                ExecuteOnlineAutoTurn(incrementStrike: true);
            }
        }
    }

    private void OnOnlineHandDistributed(HandDto dto)
    {
        CancelOnlineShuffle();
        ClearOnlineBoardVisuals();
        onlineHand.Clear();
        onlinePossibleMoves = null;
        onlineSelectedDomino = null;
        onlineTurnTimerActive = false;
        if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);
        if (onlineDrawBtn != null) onlineDrawBtn.gameObject.SetActive(false);

        List<DominoDto> dealtHand = new List<DominoDto>();
        if (dto?.Dominoes != null)
        {
            dealtHand.AddRange(dto.Dominoes);
        }

        // Persist active match session in PlayerPrefs so player can rejoin after disconnect/crash
        if (dto != null && !string.IsNullOrWhiteSpace(dto.ReconnectToken) && subscribedClient?.User != null)
        {
            int matchId = dto.MatchId ?? (currentLobby != null ? currentLobby.Id : 0);
            int lobbyId = dto.LobbyId ?? (currentLobby != null ? currentLobby.Id : matchId);
            string host = Networking.Instance != null ? Networking.Instance.CurrentHost : GetDefaultHost();
            int port = Networking.Instance != null ? Networking.Instance.CurrentPort : 10000;
            RoatanMatchSessionStore.SaveSession(matchId, lobbyId, subscribedClient.User.Id, dto.ReconnectToken, host, port);
        }

        onlineShufflePlaying = true;
        onlineShuffleRoutine = StartCoroutine(PlayOnlinePreDealShuffleCoroutine(dealtHand));
    }

    private IEnumerator PlayOnlinePreDealShuffleCoroutine(List<DominoDto> dealtHand)
    {
        onlineMatchStatus.text = "Shuffling & Mixing Tiles...";
        RoatanSoundFX.Instance?.PlayShuffleAndMix();
        float shuffleDuration = RoatanSoundFX.Instance != null
            ? RoatanSoundFX.Instance.GetShuffleClipDuration()
            : 5.0f;

        int playerCount = currentLobby != null && currentLobby.Players != null && currentLobby.Players.Count >= 2
            ? currentLobby.Players.Count
            : 4;

        yield return StartCoroutine(RoatanBoardView.PlayShuffleAndDealOnLayerCoroutine(
            onlineTilesLayer,
            shuffleDuration,
            playerCount,
            onlineShuffleTiles,
            msg => { if (onlineMatchStatus != null) onlineMatchStatus.text = msg; }));

        onlineShufflePlaying = false;
        onlineShuffleRoutine = null;

        // Reveal dealt hand & apply any opening play/turn frames that arrived during the shuffle
        onlineHand.Clear();
        onlineHand.AddRange(dealtHand);
        RebuildOnlineHandUI();

        List<PlayerPlayDto> queuedPlays = new List<PlayerPlayDto>(pendingOnlinePlaysDuringShuffle);
        pendingOnlinePlaysDuringShuffle.Clear();
        foreach (PlayerPlayDto queuedPlay in queuedPlays)
        {
            OnOnlinePlayerPlays(queuedPlay);
        }

        PlayerTurnDto queuedTurn = pendingOnlineTurnDuringShuffle;
        pendingOnlineTurnDuringShuffle = null;
        if (queuedTurn != null)
        {
            OnOnlineTurnChanged(queuedTurn);
        }

        PossibleMovesDto queuedMoves = pendingOnlineMovesDuringShuffle;
        pendingOnlineMovesDuringShuffle = null;
        if (queuedMoves != null)
        {
            OnOnlinePossibleMoves(queuedMoves);
        }
    }

    private void OnOnlinePlayerPlays(PlayerPlayDto dto)
    {
        if (dto == null) return;
        if (onlineShufflePlaying)
        {
            pendingOnlinePlaysDuringShuffle.Add(dto);
            return;
        }

        if (dto.PlayActionType == PlayActionType.PlaceDomino && dto.Moves != null)
        {
            if (subscribedClient?.User != null && dto.IdUserPlayed == subscribedClient.User.Id)
            {
                DominoDto found = onlineHand.FirstOrDefault(d =>
                    (d.Side1 == dto.Moves.Domino.Side1 && d.Side2 == dto.Moves.Domino.Side2) ||
                    (d.Side1 == dto.Moves.Domino.Side2 && d.Side2 == dto.Moves.Domino.Side1));
                if (found != null) onlineHand.Remove(found);
            }

            SpawnOnlineBoardMove(dto.Moves);
            RoatanSoundFX.Instance?.PlayTilePlace();
            onlinePossibleMoves = null;
            onlineSelectedDomino = null;
            ClearOnlineSlots();
            RebuildOnlineHandUI();
        }
        else if (dto.PlayActionType == PlayActionType.Draw)
        {
            if (subscribedClient?.User != null && dto.IdUserPlayed == subscribedClient.User.Id && dto.Moves?.Domino != null)
            {
                onlineHand.Add(dto.Moves.Domino);
                RoatanSoundFX.Instance?.PlayDraw();
                RebuildOnlineHandUI();
            }
        }
    }

    private void OnOnlineTurnChanged(PlayerTurnDto dto)
    {
        if (onlineShufflePlaying)
        {
            pendingOnlineTurnDuringShuffle = dto;
            return;
        }

        onlineActiveUserId = dto.IdUserPlaying;
        onlineTurnActionDispatched = false;
        bool isMyTurn = subscribedClient?.User != null && dto.IdUserPlaying == subscribedClient.User.Id;

        if (isMyTurn)
        {
            if (dto.ConsecutiveAutoPlays.HasValue)
            {
                onlineConsecutiveAutoPlays = dto.ConsecutiveAutoPlays.Value;
            }
            if (dto.IsBotControlled.HasValue)
            {
                onlineIsBotControlled = dto.IsBotControlled.Value;
            }

            onlineIsAutoPassTurn = dto.IsAutoPassTurn == true;
            RefreshOnlineTakeoverIndicators();

            if (onlineIsBotControlled)
            {
                onlineTurnTimerActive = false;
                onlineBotPacingRemaining = RoatanMatchEngine.BotTakeoverPacingSeconds;
                if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);
                onlineDrawBtn.gameObject.SetActive(false);
                onlineMatchStatus.text = "Bot Auto-Playing your turn... Tap any tile or Resume Control.";
            }
            else
            {
                onlineTurnTimeRemaining = onlineIsAutoPassTurn
                    ? RoatanMatchEngine.AutoPassTimeoutSeconds
                    : RoatanMatchEngine.TurnTimeoutSeconds;
                onlineTurnTimerActive = true;
                if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(true);
                UpdateOnlineTimerUI(Mathf.CeilToInt(onlineTurnTimeRemaining));

                onlineDrawBtn.gameObject.SetActive(true);
                onlineDrawBtn.GetComponent<Image>().color = onlineIsAutoPassTurn
                    ? RoatanVisualAssets.SunsetCoral
                    : RoatanVisualAssets.IslandGold;
                onlineMatchStatus.text = onlineIsAutoPassTurn
                    ? "No valid move — tap Pass or wait 5s for Auto-Pass (no penalty)"
                    : "Your turn! Tap a glowing domino to place.";
            }
        }
        else
        {
            onlineTurnTimerActive = false;
            onlineIsAutoPassTurn = false;
            if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);
            onlineDrawBtn.gameObject.SetActive(false);

            string playerName = currentLobby?.Players.FirstOrDefault(p => p.Id == dto.IdUserPlaying)?.Name ?? $"Player {dto.IdUserPlaying}";
            string botSuffix = dto.IsBotControlled == true ? " [Bot Auto-Playing]" : "";
            onlineMatchStatus.text = $"{playerName}{botSuffix}'s turn...";
        }
    }

    private void OnOnlinePossibleMoves(PossibleMovesDto dto)
    {
        if (onlineShufflePlaying)
        {
            pendingOnlineMovesDuringShuffle = dto;
            return;
        }

        onlinePossibleMoves = dto;

        bool isMyTurn = subscribedClient?.User != null && onlineActiveUserId == subscribedClient.User.Id;
        if (isMyTurn && !onlineIsBotControlled)
        {
            bool hasMoves = dto != null && dto.Moves != null && dto.Moves.Count > 0;
            bool isBlockMode = currentLobby != null && currentLobby.TableStyle == TableStyle.BLOCK;
            if (!hasMoves && isBlockMode && !onlineIsAutoPassTurn)
            {
                onlineIsAutoPassTurn = true;
                onlineTurnTimeRemaining = RoatanMatchEngine.AutoPassTimeoutSeconds;
                onlineDrawBtn.gameObject.SetActive(true);
                onlineDrawBtn.GetComponent<Image>().color = RoatanVisualAssets.SunsetCoral;
                UpdateOnlineTimerUI(Mathf.CeilToInt(onlineTurnTimeRemaining));
            }
            else if (hasMoves)
            {
                onlineIsAutoPassTurn = false;
                onlineDrawBtn.gameObject.SetActive(false);
            }
        }

        RebuildOnlineHandUI();
    }

    private void UpdateOnlineTimerUI(int displaySec)
    {
        if (onlineIsAutoPassTurn)
        {
            if (onlineDrawBtnLabel != null) onlineDrawBtnLabel.text = $"Pass ({displaySec}s)";
            if (onlineTimerLabel != null) onlineTimerLabel.text = $"AUTO-PASS {displaySec}s";
            if (onlineTimerBadgeBg != null) onlineTimerBadgeBg.color = RoatanVisualAssets.SunsetCoral;
        }
        else
        {
            if (onlineDrawBtnLabel != null) onlineDrawBtnLabel.text = "Draw / Pass";
            if (onlineTimerLabel != null) onlineTimerLabel.text = $"TURN TIMER: {displaySec}s";
            if (onlineTimerBadgeBg != null)
            {
                onlineTimerBadgeBg.color = displaySec <= 3
                    ? RoatanVisualAssets.SunsetCoral
                    : (displaySec <= 5 ? RoatanVisualAssets.AmberHighlight : new Color(0.08f, 0.34f, 0.38f, 0.96f));
            }
        }
    }

    private void ExecuteOnlineAutoPassNoPenalty()
    {
        if (subscribedClient?.User == null || onlineTurnActionDispatched) return;
        onlineTurnActionDispatched = true;
        onlineDrawBtn.gameObject.SetActive(false);
        RoatanSoundFX.Instance?.PlayPass();
        onlineMatchStatus.text = "Auto-passed (0s) — no AFK strike";
        subscribedClient.AddToSendQueue(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
        {
            IdUserPlayed = subscribedClient.User.Id,
            PlayActionType = PlayActionType.Pass
        }));
    }

    private void ExecuteOnlineAutoTurn(bool incrementStrike)
    {
        if (subscribedClient?.User == null || onlineTurnActionDispatched) return;
        onlineTurnActionDispatched = true;
        onlineDrawBtn.gameObject.SetActive(false);

        if (incrementStrike)
        {
            onlineConsecutiveAutoPlays++;
            if (onlineConsecutiveAutoPlays >= RoatanMatchEngine.BotTakeoverStrikeLimit)
            {
                onlineIsBotControlled = true;
            }
            RefreshOnlineTakeoverIndicators();
        }

        if (onlinePossibleMoves != null && onlinePossibleMoves.Moves != null && onlinePossibleMoves.Moves.Count > 0)
        {
            MoveDto chosen = onlinePossibleMoves.Moves
                .OrderByDescending(m => m.Domino.Side1 == m.Domino.Side2 ? 1 : 0)
                .ThenByDescending(m => m.Domino.Side1 + m.Domino.Side2)
                .First();

            onlineMatchStatus.text = onlineIsBotControlled
                ? $"Bot Auto-Playing [{chosen.Domino.Side1}|{chosen.Domino.Side2}] — Tap any tile or Resume Control"
                : $"Time's up! Auto-played [{chosen.Domino.Side1}|{chosen.Domino.Side2}] (AFK {onlineConsecutiveAutoPlays}/3)";

            subscribedClient.AddToSendQueue(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
            {
                IdUserPlayed = subscribedClient.User.Id,
                Moves = chosen,
                PlayActionType = PlayActionType.PlaceDomino
            }));
        }
        else
        {
            PlayActionType fallbackAction = onlineIsAutoPassTurn ? PlayActionType.Pass : PlayActionType.Draw;
            onlineMatchStatus.text = onlineIsBotControlled
                ? "Bot Auto-Playing... Tap any tile or Resume Control."
                : $"Time's up! Auto-{(fallbackAction == PlayActionType.Pass ? "passed" : "drew")} (AFK {onlineConsecutiveAutoPlays}/3)";

            subscribedClient.AddToSendQueue(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
            {
                IdUserPlayed = subscribedClient.User.Id,
                PlayActionType = fallbackAction
            }));
        }
    }

    private void ReclaimOnlineManualControl()
    {
        if (!onlineIsBotControlled) return;
        onlineIsBotControlled = false;
        onlineConsecutiveAutoPlays = 0;
        onlineBotPacingRemaining = 0f;
        RoatanSoundFX.Instance?.PlayTap();
        RefreshOnlineTakeoverIndicators();

        bool isMyTurn = subscribedClient?.User != null && onlineActiveUserId == subscribedClient.User.Id && !onlineTurnActionDispatched;
        if (isMyTurn)
        {
            onlineTurnTimeRemaining = onlineIsAutoPassTurn
                ? RoatanMatchEngine.AutoPassTimeoutSeconds
                : RoatanMatchEngine.TurnTimeoutSeconds;
            onlineTurnTimerActive = true;
            if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(true);
            UpdateOnlineTimerUI(Mathf.CeilToInt(onlineTurnTimeRemaining));
            bool hasMoves = onlinePossibleMoves != null && onlinePossibleMoves.Moves != null && onlinePossibleMoves.Moves.Count > 0;
            onlineDrawBtn.gameObject.SetActive(!hasMoves);
        }

        onlineMatchStatus.text = "Control resumed! You're back in full manual control.";
        RebuildOnlineHandUI();
    }

    private void RefreshOnlineTakeoverIndicators()
    {
        if (onlineBotIndicatorObj != null)
        {
            onlineBotIndicatorObj.SetActive(onlineIsBotControlled);
        }
        if (onlineResumeControlBtn != null)
        {
            onlineResumeControlBtn.gameObject.SetActive(onlineIsBotControlled);
        }
    }

    private void OnOnlineCountsUpdated(NumberOfDominoPerPlayerDto dto)
    {
        if (dto?.NumberOfDomino == null || currentLobby == null) return;
        List<string> parts = new List<string>();
        foreach (PlayerDto p in currentLobby.Players)
        {
            if (dto.NumberOfDomino.TryGetValue(p.Id, out int count))
            {
                parts.Add($"{p.Name}: {count}");
            }
        }
        onlineMatchHeader.text = string.Join("  •  ", parts);
    }

    private void OnOnlineEndGame(EndGameDto dto)
    {
        onlineTurnTimerActive = false;
        onlineIsBotControlled = false;
        if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);
        RefreshOnlineTakeoverIndicators();
        ChatManager.Instance?.SetActiveRoundChatLocked(false);

        bool breakTriggered = dto != null && dto.BreakTriggered == true;
        bool matchOver = dto == null || dto.MatchOver != false;

        // Clear saved session ONLY when match officially ends (MATCH_OVER)
        if (matchOver && !breakTriggered)
        {
            RoatanMatchSessionStore.ClearSession();
            pendingSavedSession = null;
        }

        if (breakTriggered)
        {
            RoatanSoundFX.Instance?.PlayBreakSting();
        }
        else
        {
            RoatanSoundFX.Instance?.PlayWin();
        }

        string winnerName = "Match Complete";
        if (dto?.Rankings != null && dto.Rankings.Count > 0 && currentLobby != null)
        {
            PlayerDto w = currentLobby.Players.FirstOrDefault(p => p.Id == dto.Rankings[0].Id);
            if (w != null) winnerName = $"Winner: {w.Name}!";
        }
        onlineMatchStatus.text = breakTriggered ? "GAME BRUK! (BREAK)" : winnerName;
        onlineDrawBtn.gameObject.SetActive(false);

        if (onlineEndOverlay != null)
        {
            onlineEndTitleLabel.text = breakTriggered ? "GAME BRUK! (BREAK)" : (matchOver ? "MATCH COMPLETE" : "HAND COMPLETE");
            onlineEndBodyLabel.text = breakTriggered
                ? $"{winnerName}\nScores reset to 0 — [6|6] poses next on Rematch."
                : winnerName;

            bool showRematchLayout = breakTriggered || matchOver;
            onlineEndPrimaryBtn.onClick.RemoveAllListeners();
            if (showRematchLayout)
            {
                onlineEndPrimaryBtnLabel.text = "Rematch";
                onlineEndPrimaryBtn.onClick.AddListener(() =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    onlineEndOverlay.SetActive(false);
                    if (currentLobby != null && subscribedClient != null)
                    {
                        ShowSubView(lobbyView);
                        RefreshLobbyView();
                    }
                    else
                    {
                        ShowSubView(hubView);
                        RequestLobbiesList();
                    }
                });
                onlineEndNewMatchBtn.gameObject.SetActive(true);
            }
            else
            {
                onlineEndPrimaryBtnLabel.text = "Next Round";
                onlineEndPrimaryBtn.onClick.AddListener(() =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    onlineEndOverlay.SetActive(false);
                    EnterOnlineMatchTable();
                });
                onlineEndNewMatchBtn.gameObject.SetActive(false);
            }

            onlineEndOverlay.transform.SetAsLastSibling();
            onlineEndOverlay.SetActive(true);
        }
    }

    private void OnOnlineLeaveGameResponse(ResponseDto dto)
    {
        RoatanMatchSessionStore.ClearSession();
        pendingSavedSession = null;
        ChatManager.Instance?.SetActiveRoundChatLocked(false);
        ShowSubView(hubView);
        RequestLobbiesList();
    }

    // ==================== REJOIN HANDSHAKE & SNAPSHOT SYNC ====================

    private void OnCheckActiveMatchResponse(ResponseObjectDto<ActiveMatchCheckResponseDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null || !dto.Object.HasActiveMatch || dto.Object.RemainingGraceSeconds <= 0)
        {
            RoatanMatchSessionStore.ClearSession();
            pendingSavedSession = null;
            rejoinModalActive = false;
            if (rejoinModal != null) rejoinModal.SetActive(false);
            return;
        }

        ActiveMatchCheckResponseDto info = dto.Object;
        rejoinModalRemainingSeconds = info.RemainingGraceSeconds;
        rejoinModalActive = true;

        if (rejoinModal != null)
        {
            string rules = FormatLobbyRulesBadge(info.MatchFormat, info.TableStyle, null);
            string title = string.IsNullOrWhiteSpace(info.MatchTitle) ? $"Match #{info.MatchId}" : info.MatchTitle;
            rejoinModalBodyLabel.text = $"You disconnected from an active online match:\n<b>{title}</b>\n{rules}\n\nRejoin now to reclaim your seat from the bot!";
            rejoinModalTimerLabel.text = $"Time remaining to rejoin: {info.RemainingGraceSeconds}s";
            rejoinModalTimerLabel.color = RoatanVisualAssets.IslandGold;
            if (rejoinConfirmBtn != null) rejoinConfirmBtn.interactable = true;
            rejoinModal.transform.SetAsLastSibling();
            rejoinModal.SetActive(true);
        }
    }

    private void RequestRejoinMatch()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (pendingSavedSession == null || subscribedClient == null)
        {
            if (rejoinModal != null) rejoinModal.SetActive(false);
            return;
        }

        if (rejoinConfirmBtn != null) rejoinConfirmBtn.interactable = false;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.RejoinMatchFrame(new RejoinSessionRequestDto
        {
            MatchId = pendingSavedSession.MatchId,
            LobbyId = pendingSavedSession.LobbyId,
            PlayerId = pendingSavedSession.PlayerId,
            ReconnectToken = pendingSavedSession.ReconnectToken
        }));
    }

    private void RequestAbandonMatch()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (pendingSavedSession != null && subscribedClient != null)
        {
            subscribedClient.AddToSendQueue(MessageFrameBuilder.AbandonMatchFrame(new RejoinSessionRequestDto
            {
                MatchId = pendingSavedSession.MatchId,
                LobbyId = pendingSavedSession.LobbyId,
                PlayerId = pendingSavedSession.PlayerId,
                ReconnectToken = pendingSavedSession.ReconnectToken
            }));
        }

        RoatanMatchSessionStore.ClearSession();
        pendingSavedSession = null;
        rejoinModalActive = false;
        if (rejoinModal != null) rejoinModal.SetActive(false);
    }

    private void OnAbandonMatchResponse(ResponseDto dto)
    {
        RoatanMatchSessionStore.ClearSession();
        pendingSavedSession = null;
        rejoinModalActive = false;
        if (rejoinModal != null) rejoinModal.SetActive(false);
    }

    private void OnRejoinMatchResponse(ResponseObjectDto<RejoinMatchSnapshotDto> dto)
    {
        rejoinModalActive = false;
        if (rejoinModal != null) rejoinModal.SetActive(false);

        if (dto == null || !dto.OkResult || dto.Object == null)
        {
            RoatanMatchSessionStore.ClearSession();
            pendingSavedSession = null;
            SetFeedback("Match expired or already ended.", RoatanVisualAssets.SunsetCoral);
            return;
        }

        ApplyRejoinSnapshot(dto.Object);
    }

    private void ApplyRejoinSnapshot(RejoinMatchSnapshotDto snap)
    {
        CancelOnlineShuffle();
        isSignedIn = true;
        currentLobby = new LobbyDto
        {
            Id = snap.LobbyId,
            Name = string.IsNullOrWhiteSpace(snap.MatchTitle) ? $"Match #{snap.MatchId}" : snap.MatchTitle,
            MatchFormat = snap.MatchFormat,
            TableStyle = snap.TableStyle,
            OneOnePlayoff = snap.OneOnePlayoff,
            Players = snap.Players ?? new List<PlayerDto>()
        };

        // Refresh saved session token/expiry in PlayerPrefs
        string host = Networking.Instance != null ? Networking.Instance.CurrentHost : GetDefaultHost();
        int port = Networking.Instance != null ? Networking.Instance.CurrentPort : 10000;
        RoatanMatchSessionStore.SaveSession(snap.MatchId, snap.LobbyId, snap.PlayerId, snap.ReconnectToken, host, port);

        // Switch UI into Online Match view cleanly (hides Home screen if triggered on app startup)
        onRejoinEnteredMatch?.Invoke();
        onlineRoot.SetActive(true);
        ShowSubView(onlineGameView);
        if (onlineEndOverlay != null) onlineEndOverlay.SetActive(false);

        // Reset board & sync live tiles + player hand + possible moves + turn timer
        ClearOnlineBoardVisuals();
        onlineHand.Clear();
        onlineSelectedDomino = null;
        onlineConsecutiveAutoPlays = 0;
        onlineIsBotControlled = false;
        onlineTurnActionDispatched = false;
        RefreshOnlineTakeoverIndicators();

        if (snap.BoardMoves != null)
        {
            foreach (MoveDto move in snap.BoardMoves)
            {
                SpawnOnlineBoardMove(move);
            }
        }

        if (snap.Hand != null)
        {
            onlineHand.AddRange(snap.Hand);
        }

        onlinePossibleMoves = snap.PossibleMoves;
        onlineActiveUserId = snap.CurrentTurnPlayerId;
        onlineIsAutoPassTurn = snap.IsAutoPassTurn;

        // Update header with live counts & scores
        List<string> headerParts = new List<string>();
        string rulesShort = FormatLobbyRulesBadge(snap.MatchFormat, snap.TableStyle, snap.OneOnePlayoff);
        headerParts.Add(rulesShort);
        if (snap.Players != null && snap.NumberOfDominoPerPlayer != null)
        {
            foreach (PlayerDto p in snap.Players)
            {
                if (snap.NumberOfDominoPerPlayer.TryGetValue(p.Id, out int cnt))
                {
                    int pts = snap.PlayerScores != null && snap.PlayerScores.TryGetValue(p.Id, out int sc) ? sc : 0;
                    headerParts.Add($"{p.Name}:{cnt}({pts}W)");
                }
            }
        }
        onlineMatchHeader.text = string.Join(" • ", headerParts);

        bool isMyTurn = snap.CurrentTurnPlayerId == snap.PlayerId;
        if (isMyTurn)
        {
            onlineTurnTimeRemaining = snap.IsAutoPassTurn
                ? RoatanMatchEngine.AutoPassTimeoutSeconds
                : (snap.TurnTimer != null && snap.TurnTimer.Duration > 0 ? snap.TurnTimer.Duration : RoatanMatchEngine.TurnTimeoutSeconds);
            onlineTurnTimerActive = true;
            if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(true);
            UpdateOnlineTimerUI(Mathf.CeilToInt(onlineTurnTimeRemaining));

            bool hasMoves = snap.PossibleMoves != null && snap.PossibleMoves.Moves != null && snap.PossibleMoves.Moves.Count > 0;
            onlineDrawBtn.gameObject.SetActive(!hasMoves);
            onlineDrawBtn.GetComponent<Image>().color = snap.IsAutoPassTurn ? RoatanVisualAssets.SunsetCoral : RoatanVisualAssets.IslandGold;
            onlineMatchStatus.text = "Rejoined live match! It's your turn!";
        }
        else
        {
            onlineTurnTimerActive = false;
            if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);
            onlineDrawBtn.gameObject.SetActive(false);
            string activeName = snap.Players?.FirstOrDefault(p => p.Id == snap.CurrentTurnPlayerId)?.Name ?? $"Player {snap.CurrentTurnPlayerId}";
            onlineMatchStatus.text = $"Rejoined live match! Waiting for {activeName}...";
        }

        ChatManager.Instance?.SetActiveRoundChatLocked(true);
        RebuildOnlineHandUI();
        RoatanSoundFX.Instance?.PlayWin();
    }

    private void SpawnOnlineBoardMove(MoveDto move)
    {
        const float cellSize = 46f;
        Vector2 pos = new Vector2((move.Col - 30) * cellSize, (30 - move.Row) * cellSize);
        float rotZ = 0f;

        switch (move.Orientation)
        {
            case Orientation.HorizontalRight:
                pos.x += cellSize * 0.5f;
                rotZ = -90f;
                break;
            case Orientation.HorizontalLeft:
                pos.x -= cellSize * 0.5f;
                rotZ = 90f;
                break;
            case Orientation.VerticalUp:
                pos.y += cellSize * 0.5f;
                rotZ = 0f;
                break;
            case Orientation.VerticalDown:
                pos.y -= cellSize * 0.5f;
                rotZ = 180f;
                break;
            case Orientation.PerpendicularHorizontalFromBottom:
            case Orientation.PerpendicularHorizontalFromTop:
                rotZ = 90f;
                break;
            case Orientation.PerpendicularVerticalFromLeft:
            case Orientation.PerpendicularVerticalFromRight:
                rotZ = 0f;
                break;
        }

        GameObject go = new GameObject("OnlineTile");
        go.transform.SetParent(onlineTilesLayer, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(46f, 92f);
        rect.anchoredPosition = pos;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotZ);

        Image img = go.AddComponent<Image>();
        img.sprite = RoatanVisualAssets.GetDominoSprite(move.Domino.Side1, move.Domino.Side2);
        img.raycastTarget = false;
        onlineBoardObjs.Add(go);
    }

    private void RebuildOnlineHandUI()
    {
        foreach (GameObject go in onlineHandObjs) Destroy(go);
        onlineHandObjs.Clear();

        foreach (DominoDto tile in onlineHand)
        {
            DominoDto captured = tile;
            bool playable = onlinePossibleMoves != null && onlinePossibleMoves.Moves.Any(m =>
                (m.Domino.Side1 == tile.Side1 && m.Domino.Side2 == tile.Side2) ||
                (m.Domino.Side1 == tile.Side2 && m.Domino.Side2 == tile.Side1));

            GameObject slot = new GameObject("OnlineHandTile");
            slot.transform.SetParent(onlineHandContent, false);
            LayoutElement le = slot.AddComponent<LayoutElement>();
            le.preferredWidth = 104f;
            le.preferredHeight = 220f;

            GameObject btnObj = new GameObject("Face");
            btnObj.transform.SetParent(slot.transform, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(104f, 208f);
            rect.anchoredPosition = new Vector2(0f, playable ? 10f : -6f);

            Image img = btnObj.AddComponent<Image>();
            img.sprite = RoatanVisualAssets.GetDominoSprite(tile.Side1, tile.Side2);
            img.color = playable ? Color.white : new Color(0.75f, 0.78f, 0.82f, 0.85f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnOnlineHandTileClicked(captured));

            onlineHandObjs.Add(slot);
        }
    }

    private void OnOnlineHandTileClicked(DominoDto tile)
    {
        // Instant Manual Reclaim: tapping ANY tile in hand while Bot Auto-Playing immediately reclaims control
        if (onlineIsBotControlled)
        {
            ReclaimOnlineManualControl();
        }

        if (onlinePossibleMoves == null) return;
        List<MoveDto> matching = onlinePossibleMoves.Moves.Where(m =>
            (m.Domino.Side1 == tile.Side1 && m.Domino.Side2 == tile.Side2) ||
            (m.Domino.Side1 == tile.Side2 && m.Domino.Side2 == tile.Side1)).ToList();

        if (matching.Count == 0) return;
        RoatanSoundFX.Instance?.PlayTap();

        onlineSelectedDomino = tile;
        ClearOnlineSlots();

        const float cellSize = 46f;
        foreach (MoveDto move in matching)
        {
            MoveDto captured = move;
            GameObject slotObj = new GameObject("OnlineSlot");
            slotObj.transform.SetParent(onlineSlotsLayer, false);
            RectTransform rect = slotObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(54f, 102f);
            rect.anchoredPosition = new Vector2((move.Col - 30) * cellSize, (30 - move.Row) * cellSize);

            Image img = slotObj.AddComponent<Image>();
            img.sprite = RoatanVisualAssets.GlowSlotSprite;

            Button btn = slotObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                onlineTurnTimerActive = false;
                onlineTurnActionDispatched = true;
                onlineConsecutiveAutoPlays = 0;
                onlineIsBotControlled = false;
                if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);
                RefreshOnlineTakeoverIndicators();
                subscribedClient?.AddToSendQueue(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
                {
                    IdUserPlayed = subscribedClient.User.Id,
                    Moves = captured,
                    PlayActionType = PlayActionType.PlaceDomino
                }));
            });

            onlineSlotObjs.Add(slotObj);
        }
    }

    private void ClearOnlineBoardVisuals()
    {
        foreach (GameObject go in onlineBoardObjs) Destroy(go);
        onlineBoardObjs.Clear();
        ClearOnlineSlots();
        foreach (GameObject go in onlineHandObjs) Destroy(go);
        onlineHandObjs.Clear();
    }

    private void ClearOnlineSlots()
    {
        foreach (GameObject go in onlineSlotObjs) Destroy(go);
        onlineSlotObjs.Clear();
    }

    // ==================== UI BUILDERS (ROATAN TROPICAL THEME) ====================

    private void BuildOnlineScreen(RectTransform safeRoot)
    {
        onlineRoot = CreateStretchPanel(safeRoot, "RoatanOnlineRoot", Color.clear);

        // Top Navigation Bar
        GameObject topBar = new GameObject("OnlineTopBar");
        topBar.transform.SetParent(onlineRoot.transform, false);
        RectTransform topRect = topBar.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.sizeDelta = new Vector2(0f, 106f);
        topRect.anchoredPosition = new Vector2(0f, -6f);

        Button backBtn = CreatePillButton(topBar.transform, "BtnOnlineHome", "< Home", new Color(0.06f, 0.17f, 0.22f, 0.94f), new Vector2(-395f, 0f), new Vector2(190f, 76f), 26, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            if (onlineGameView != null && onlineGameView.activeSelf)
            {
                RoatanMatchSessionStore.ClearSession();
                pendingSavedSession = null;
                subscribedClient?.AddToSendQueue(MessageFrameBuilder.LeaveGameFrame());
            }
            HideOnline();
            onReturnHome?.Invoke();
        });
        backBtnLabel = backBtn.GetComponentInChildren<TMP_Text>();

        GameObject titleCard = CreateCard(topBar.transform, "OnlineTitleCard", new Color(0.06f, 0.17f, 0.22f, 0.94f), new Vector2(90f, 0f), new Vector2(720f, 76f));
        titleLabel = CreateText(titleCard.transform, "OnlineTitle", "ONLINE MULTIPLAYER", 30, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(680f, 56f));

        // Connection Status Banner
        GameObject badgeCard = CreateCard(onlineRoot.transform, "ConnBadge", new Color(0.08f, 0.21f, 0.27f, 0.94f), Vector2.zero, new Vector2(940f, 64f));
        RectTransform badgeRect = badgeCard.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 1f);
        badgeRect.anchorMax = new Vector2(0.5f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, -124f);
        connectionBadgeBg = badgeCard.GetComponent<Image>();
        connectionBadgeText = CreateText(badgeCard.transform, "ConnText", "", 23, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(900f, 52f));

        // Feedback Toast at bottom
        GameObject toastCard = CreateCard(onlineRoot.transform, "OnlineToast", new Color(0.05f, 0.15f, 0.20f, 0.92f), Vector2.zero, new Vector2(940f, 72f));
        RectTransform toastRect = toastCard.GetComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0.5f, 0f);
        toastRect.anchorMax = new Vector2(0.5f, 0f);
        toastRect.pivot = new Vector2(0.5f, 0f);
        toastRect.anchoredPosition = new Vector2(0f, 22f);
        feedbackToast = CreateText(toastCard.transform, "ToastText", "Enter your DominoServer IP or return Home to play Offline vs Bots.", 22, FontStyles.Normal, new Color(0.88f, 0.96f, 0.98f), Vector2.zero, new Vector2(900f, 60f));

        BuildAuthView(onlineRoot.transform);
        BuildHubView(onlineRoot.transform);
        BuildLobbyView(onlineRoot.transform);
        BuildOnlineGameView(onlineRoot.transform);
    }

    private void BuildAuthView(Transform parent)
    {
        authView = CreateStretchPanel(parent, "AuthView", Color.clear);

        // Server Connection Card
        GameObject serverCard = CreateCard(authView.transform, "ServerCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, 525f), new Vector2(940f, 230f));
        CreateText(serverCard.transform, "ServerHdr", "SERVER CONNECTION", 24, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 78f), new Vector2(880f, 36f));

        string savedHost = PlayerPrefs.GetString(PrefHostKey, GetDefaultHost());
        int savedPort = PlayerPrefs.GetInt(PrefPortKey, 10000);

        hostInput = CreateInputField(serverCard.transform, "HostInput", "Server IP (e.g. 10.0.2.2)", savedHost, new Vector2(-150f, 12f), new Vector2(560f, 68f));
        portInput = CreateInputField(serverCard.transform, "PortInput", "Port", savedPort.ToString(), new Vector2(285f, 12f), new Vector2(250f, 68f));

        Button connBtn = CreatePillButton(serverCard.transform, "BtnConnect", "Connect / Retry", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -72f), new Vector2(440f, 68f), 26, AttemptServerConnect);
        connectBtnLabel = connBtn.GetComponentInChildren<TMP_Text>();

        // Sign In / Create Account Card
        GameObject formCard = CreateCard(authView.transform, "AuthFormCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, -85f), new Vector2(940f, 890f));

        Button tabIn = CreatePillButton(formCard.transform, "TabSignIn", "Sign In", RoatanVisualAssets.BrightLagoon, new Vector2(-215f, 375f), new Vector2(410f, 76f), 28, () => SelectAuthTab(true));
        signInTabImg = tabIn.GetComponent<Image>();
        signInTabLabel = tabIn.GetComponentInChildren<TMP_Text>();

        Button tabReg = CreatePillButton(formCard.transform, "TabRegister", "Create Account", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(215f, 375f), new Vector2(410f, 76f), 28, () => SelectAuthTab(false));
        registerTabImg = tabReg.GetComponent<Image>();
        registerTabLabel = tabReg.GetComponentInChildren<TMP_Text>();

        // Sign In Panel
        signInPanel = CreateStretchPanel(formCard.transform, "SignInPanel", Color.clear);
        loginUserInput = CreateInputField(signInPanel.transform, "LoginUser", "Username", "", new Vector2(0f, 170f), new Vector2(820f, 84f));
        loginPassInput = CreateInputField(signInPanel.transform, "LoginPass", "Password", "", new Vector2(0f, 55f), new Vector2(820f, 84f), isPassword: true);
        Button loginSubmit = CreatePillButton(signInPanel.transform, "BtnSubmitLogin", "Sign In", RoatanVisualAssets.PalmGreen, new Vector2(0f, -100f), new Vector2(620f, 102f), 34, SubmitLogin);
        signInSubmitLabel = loginSubmit.GetComponentInChildren<TMP_Text>();

        // Register Panel
        registerPanel = CreateStretchPanel(formCard.transform, "RegisterPanel", Color.clear);
        regEmailInput = CreateInputField(registerPanel.transform, "RegEmail", "Email Address", "", new Vector2(0f, 225f), new Vector2(820f, 78f));
        regUserInput = CreateInputField(registerPanel.transform, "RegUser", "Username", "", new Vector2(0f, 125f), new Vector2(820f, 78f));
        regPassInput = CreateInputField(registerPanel.transform, "RegPass", "Password", "", new Vector2(0f, 25f), new Vector2(820f, 78f), isPassword: true);
        regBirthInput = CreateInputField(registerPanel.transform, "RegBirth", "Birthdate (DD/MM/YYYY)", "01/01/2000", new Vector2(0f, -75f), new Vector2(820f, 78f));
        Button regSubmit = CreatePillButton(registerPanel.transform, "BtnSubmitReg", "Create Account", RoatanVisualAssets.PalmGreen, new Vector2(0f, -210f), new Vector2(620f, 96f), 32, SubmitRegister);
        registerSubmitLabel = regSubmit.GetComponentInChildren<TMP_Text>();

        SelectAuthTab(isSignIn: true);
    }

    private void BuildHubView(Transform parent)
    {
        hubView = CreateStretchPanel(parent, "HubView", Color.clear);

        GameObject profileBar = CreateCard(hubView.transform, "HubProfileCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, 605f), new Vector2(940f, 96f));
        hubWelcomeLabel = CreateText(profileBar.transform, "HubWelcome", "Online Player", 26, FontStyles.Bold, Color.white, new Vector2(-140f, 0f), new Vector2(580f, 58f));
        hubWelcomeLabel.alignment = TextAlignmentOptions.MidlineLeft;
        Button outBtn = CreatePillButton(profileBar.transform, "BtnSignOut", "Sign Out", RoatanVisualAssets.SunsetCoral, new Vector2(335f, 0f), new Vector2(210f, 64f), 24, SubmitLogout);
        signOutBtnLabel = outBtn.GetComponentInChildren<TMP_Text>();

        Button rankedBtn = CreatePillButton(hubView.transform, "BtnQuickRanked", "Quick Ranked Match", RoatanVisualAssets.IslandGold, new Vector2(0f, 495f), new Vector2(940f, 84f), 30, RequestQuickRanked);
        quickRankedLabel = rankedBtn.GetComponentInChildren<TMP_Text>();
        quickRankedLabel.color = new Color(0.10f, 0.10f, 0.12f, 1f);

        // Create Lobby Card (with Table Style, Match Format, 1-1 Playoff & Player Count)
        GameObject createCard = CreateCard(hubView.transform, "CreateLobbyCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, 205f), new Vector2(940f, 455f));
        createLobbyNameInput = CreateInputField(createCard.transform, "LobbyNameInput", "New Lobby Name", "Roatan Table", new Vector2(0f, 174f), new Vector2(860f, 64f));

        // Table Style Row (Block | Draw)
        TableStyle[] styles = { TableStyle.BLOCK, TableStyle.DRAW };
        string[] styleLabels = { "Block (Cerrado)", "Classic Draw" };
        float[] styleXs = { -218f, 218f };
        for (int i = 0; i < styles.Length; i++)
        {
            TableStyle st = styles[i];
            Button sb = CreatePillButton(createCard.transform, $"Style_{st}", styleLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(styleXs[i], 96f), new Vector2(416f, 62f), 24, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                createTableStyle = st;
                RefreshCreateLobbySelections();
            });
            createTableStyleBtns[st] = sb.GetComponent<Image>();
        }

        // Match Format Row (Six-Lov 2v2 | Two-Best | Cut-Throat)
        MatchFormat[] formats = { MatchFormat.SIX_LOV, MatchFormat.TWO_BEST, MatchFormat.CUT_THROAT };
        string[] formatLabels = { "Six-Lov (2v2)", "Two-Best", "Cut-Throat" };
        float[] fmtXs = { -290f, 0f, 290f };
        for (int i = 0; i < formats.Length; i++)
        {
            MatchFormat mf = formats[i];
            Button fb = CreatePillButton(createCard.transform, $"Fmt_{mf}", formatLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(fmtXs[i], 18f), new Vector2(272f, 66f), 23, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                createMatchFormat = mf;
                RefreshCreateLobbySelections();
            });
            createMatchFormatBtns[mf] = fb.GetComponent<Image>();
        }

        // Player Count / 1-1 Playoff Row
        int[] maxChoices = { 2, 3, 4 };
        float[] xs = { -290f, 0f, 290f };
        for (int i = 0; i < maxChoices.Length; i++)
        {
            int count = maxChoices[i];
            Button b = CreatePillButton(createCard.transform, $"MaxP_{count}", $"{count} Players", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(xs[i], -62f), new Vector2(272f, 62f), 23, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                createLobbyMaxPlayers = count;
                RefreshCreateLobbySelections();
            });
            createMaxPlayerBtns[count] = b.GetComponent<Image>();
            createMaxPlayerObjs[count] = b.gameObject;
        }

        createOneOnePlayoffBtn = CreatePillButton(createCard.transform, "BtnOnlineOneOne", "4P (2v2)  •  1-1 Playoff: OFF", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(0f, -62f), new Vector2(856f, 62f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            createOneOnePlayoff = !createOneOnePlayoff;
            RefreshCreateLobbySelections();
        });
        createOneOnePlayoffImg = createOneOnePlayoffBtn.GetComponent<Image>();
        createOneOnePlayoffLabel = createOneOnePlayoffBtn.GetComponentInChildren<TMP_Text>();

        Button createBtn = CreatePillButton(createCard.transform, "BtnCreateLobby", "Create Lobby", RoatanVisualAssets.PalmGreen, new Vector2(0f, -156f), new Vector2(640f, 76f), 28, RequestCreateLobby);
        createLobbyBtnLabel = createBtn.GetComponentInChildren<TMP_Text>();

        RefreshCreateLobbySelections();

        // Join by Code Row
        GameObject codeCard = CreateCard(hubView.transform, "CodeCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, -88f), new Vector2(940f, 92f));
        joinCodeInput = CreateInputField(codeCard.transform, "JoinCodeInput", "Private Lobby Code", "", new Vector2(-150f, 0f), new Vector2(560f, 64f));
        Button codeBtn = CreatePillButton(codeCard.transform, "BtnJoinCode", "Join Code", RoatanVisualAssets.BrightLagoon, new Vector2(290f, 0f), new Vector2(280f, 64f), 24, RequestJoinByCode);
        joinCodeBtnLabel = codeBtn.GetComponentInChildren<TMP_Text>();

        // Public Lobbies Scroll List
        GameObject listCard = CreateCard(hubView.transform, "LobbiesCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, -455f), new Vector2(940f, 600f));
        Button refreshBtn = CreatePillButton(listCard.transform, "BtnRefreshLobbies", "Refresh Lobbies", RoatanVisualAssets.BrightLagoon, new Vector2(0f, 245f), new Vector2(380f, 60f), 24, RequestLobbiesList);
        refreshLobbiesBtnLabel = refreshBtn.GetComponentInChildren<TMP_Text>();

        GameObject scrollObj = new GameObject("LobbiesScroll");
        scrollObj.transform.SetParent(listCard.transform, false);
        RectTransform srRect = scrollObj.AddComponent<RectTransform>();
        srRect.anchorMin = Vector2.zero;
        srRect.anchorMax = Vector2.one;
        srRect.offsetMin = new Vector2(24f, 24f);
        srRect.offsetMax = new Vector2(-24f, -106f);
        scrollObj.AddComponent<RectMask2D>();
        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        lobbiesListContent = contentObj.AddComponent<RectTransform>();
        lobbiesListContent.anchorMin = new Vector2(0f, 1f);
        lobbiesListContent.anchorMax = new Vector2(1f, 1f);
        lobbiesListContent.pivot = new Vector2(0.5f, 1f);
        VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = lobbiesListContent;
        sr.viewport = srRect;
    }

    private void BuildLobbyView(Transform parent)
    {
        lobbyView = CreateStretchPanel(parent, "LobbyView", Color.clear);

        GameObject card = CreateCard(lobbyView.transform, "InLobbyCard", new Color(0.06f, 0.18f, 0.24f, 0.95f), new Vector2(0f, 20f), new Vector2(940f, 1180f));
        lobbyTitleLabel = CreateText(card.transform, "LTitle", "Lobby", 40, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 505f), new Vector2(860f, 62f));
        lobbyInfoLabel = CreateText(card.transform, "LInfo", "", 23, FontStyles.Normal, new Color(0.84f, 0.95f, 0.98f), new Vector2(0f, 438f), new Vector2(860f, 76f));
        lobbyTimerLabel = CreateText(card.transform, "LTimer", "Waiting for players...", 25, FontStyles.Italic, Color.white, new Vector2(0f, 366f), new Vector2(860f, 44f));

        GameObject playersBox = new GameObject("PlayersBox");
        playersBox.transform.SetParent(card.transform, false);
        lobbyPlayersContent = playersBox.AddComponent<RectTransform>();
        lobbyPlayersContent.anchoredPosition = new Vector2(0f, 40f);
        lobbyPlayersContent.sizeDelta = new Vector2(860f, 560f);
        VerticalLayoutGroup vlg = playersBox.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 16f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        Button readyBtn = CreatePillButton(card.transform, "BtnLobbyReady", "Ready", RoatanVisualAssets.PalmGreen, new Vector2(0f, -385f), new Vector2(600f, 102f), 34, ToggleLobbyReady);
        readyBtnImg = readyBtn.GetComponent<Image>();
        readyBtnLabel = readyBtn.GetComponentInChildren<TMP_Text>();

        Button leaveBtn = CreatePillButton(card.transform, "BtnLobbyLeave", "Leave Lobby", RoatanVisualAssets.SunsetCoral, new Vector2(0f, -505f), new Vector2(420f, 82f), 28, LeaveCurrentLobby);
        leaveLobbyBtnLabel = leaveBtn.GetComponentInChildren<TMP_Text>();
    }

    private void BuildOnlineGameView(Transform parent)
    {
        onlineGameView = CreateStretchPanel(parent, "OnlineGameView", Color.clear);

        GameObject hdr = CreateCard(onlineGameView.transform, "OnlineMatchHdr", new Color(0.06f, 0.17f, 0.22f, 0.92f), Vector2.zero, new Vector2(940f, 68f));
        RectTransform hdrRect = hdr.GetComponent<RectTransform>();
        hdrRect.anchorMin = new Vector2(0.5f, 1f);
        hdrRect.anchorMax = new Vector2(0.5f, 1f);
        hdrRect.pivot = new Vector2(0.5f, 1f);
        hdrRect.anchoredPosition = new Vector2(0f, -200f);
        onlineMatchHeader = CreateText(hdr.transform, "Text", "ONLINE MATCH", 22, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(900f, 54f));

        // Board area
        GameObject boardVp = new GameObject("OnlineBoardVP");
        boardVp.transform.SetParent(onlineGameView.transform, false);
        RectTransform vpRect = boardVp.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = new Vector2(12f, 380f);
        vpRect.offsetMax = new Vector2(-12f, -280f);
        boardVp.AddComponent<RectMask2D>();

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(boardVp.transform, false);
        onlineBoardContent = contentObj.AddComponent<RectTransform>();
        onlineBoardContent.anchoredPosition = Vector2.zero;

        GameObject tilesObj = new GameObject("Tiles");
        tilesObj.transform.SetParent(onlineBoardContent, false);
        onlineTilesLayer = tilesObj.AddComponent<RectTransform>();

        GameObject slotsObj = new GameObject("Slots");
        slotsObj.transform.SetParent(onlineBoardContent, false);
        onlineSlotsLayer = slotsObj.AddComponent<RectTransform>();

        // Status + Draw button
        GameObject statusCard = CreateCard(onlineGameView.transform, "OnlineStatusCard", new Color(0.05f, 0.16f, 0.22f, 0.92f), Vector2.zero, new Vector2(940f, 64f));
        RectTransform sRect = statusCard.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.5f, 0f);
        sRect.anchorMax = new Vector2(0.5f, 0f);
        sRect.pivot = new Vector2(0.5f, 0f);
        sRect.anchoredPosition = new Vector2(0f, 355f);
        onlineMatchStatus = CreateText(statusCard.transform, "Text", "", 24, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(900f, 52f));

        onlineDrawBtn = CreatePillButton(onlineGameView.transform, "BtnOnlineDraw", "Draw / Pass", RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(360f, 82f), 28, () =>
        {
            if (subscribedClient?.User == null || onlineActiveUserId != subscribedClient.User.Id || onlineTurnActionDispatched) return;
            onlineTurnTimerActive = false;
            onlineTurnActionDispatched = true;
            if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);

            if (!onlineIsAutoPassTurn)
            {
                onlineConsecutiveAutoPlays = 0;
                onlineIsBotControlled = false;
                RefreshOnlineTakeoverIndicators();
            }

            subscribedClient.AddToSendQueue(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
            {
                IdUserPlayed = subscribedClient.User.Id,
                PlayActionType = onlineIsAutoPassTurn ? PlayActionType.Pass : PlayActionType.Draw
            }));
        });
        RectTransform dRect = onlineDrawBtn.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.5f, 0f);
        dRect.anchorMax = new Vector2(0.5f, 0f);
        dRect.pivot = new Vector2(0.5f, 0f);
        dRect.anchoredPosition = new Vector2(0f, 430f);
        onlineDrawBtnLabel = onlineDrawBtn.GetComponentInChildren<TMP_Text>();
        onlineDrawBtnLabel.color = Color.black;
        onlineDrawBtn.gameObject.SetActive(false);

        // Online 10s Turn Timer / 5s Auto-Pass Pill
        onlineTimerBadgeObj = CreateCard(onlineGameView.transform, "OnlineTurnTimerBadge", new Color(0.08f, 0.34f, 0.38f, 0.96f), Vector2.zero, new Vector2(275f, 64f));
        RectTransform otRect = onlineTimerBadgeObj.GetComponent<RectTransform>();
        otRect.anchorMin = new Vector2(0f, 0f);
        otRect.anchorMax = new Vector2(0f, 0f);
        otRect.pivot = new Vector2(0f, 0f);
        otRect.anchoredPosition = new Vector2(24f, 430f);
        onlineTimerBadgeBg = onlineTimerBadgeObj.GetComponent<Image>();
        onlineTimerLabel = CreateText(onlineTimerBadgeObj.transform, "TimerText", "TURN TIMER: 10s", 22, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(255f, 52f));
        onlineTimerBadgeObj.SetActive(false);

        // Online "Bot Auto-Playing" Indicator + Instant "Resume Control" Banner
        onlineBotIndicatorObj = CreateCard(onlineGameView.transform, "OnlineBotIndicator", new Color(0.58f, 0.22f, 0.14f, 0.96f), Vector2.zero, new Vector2(290f, 58f));
        RectTransform obiRect = onlineBotIndicatorObj.GetComponent<RectTransform>();
        obiRect.anchorMin = new Vector2(0f, 0f);
        obiRect.anchorMax = new Vector2(0f, 0f);
        obiRect.pivot = new Vector2(0f, 0f);
        obiRect.anchoredPosition = new Vector2(24f, 502f);
        CreateText(onlineBotIndicatorObj.transform, "BotText", "Bot Auto-Playing", 22, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(270f, 46f));
        onlineBotIndicatorObj.SetActive(false);

        onlineResumeControlBtn = CreatePillButton(onlineGameView.transform, "BtnOnlineResumeControl", "Resume Control (Tap Here or Any Tile)", RoatanVisualAssets.PalmGreen, Vector2.zero, new Vector2(610f, 76f), 25, ReclaimOnlineManualControl);
        RectTransform orcRect = onlineResumeControlBtn.GetComponent<RectTransform>();
        orcRect.anchorMin = new Vector2(0.5f, 0f);
        orcRect.anchorMax = new Vector2(0.5f, 0f);
        orcRect.pivot = new Vector2(0.5f, 0f);
        orcRect.anchoredPosition = new Vector2(0f, 430f);
        onlineResumeControlBtn.gameObject.SetActive(false);

        // Online Post-Hand / Post-Break Overlay
        onlineEndOverlay = CreateStretchPanel(onlineGameView.transform, "OnlineEndOverlay", new Color(0f, 0f, 0f, 0.74f));
        GameObject endCard = CreateCard(onlineEndOverlay.transform, "EndCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(880f, 700f));
        onlineEndTitleLabel = CreateText(endCard.transform, "EndTitle", "MATCH COMPLETE", 42, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 255f), new Vector2(800f, 64f));
        onlineEndBodyLabel = CreateText(endCard.transform, "EndBody", "", 28, FontStyles.Normal, Color.white, new Vector2(0f, 110f), new Vector2(780f, 160f));

        onlineEndPrimaryBtn = CreatePillButton(endCard.transform, "BtnOnlinePrimary", "Next Round", RoatanVisualAssets.PalmGreen, new Vector2(0f, -60f), new Vector2(520f, 92f), 32, () => { });
        onlineEndPrimaryBtnLabel = onlineEndPrimaryBtn.GetComponentInChildren<TMP_Text>();

        onlineEndNewMatchBtn = CreatePillButton(endCard.transform, "BtnOnlineNewMatch", "New Match", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -172f), new Vector2(460f, 82f), 28, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            onlineEndOverlay.SetActive(false);
            ShowSubView(hubView);
            RequestLobbiesList();
        });
        onlineEndNewMatchBtn.gameObject.SetActive(false);

        CreatePillButton(endCard.transform, "BtnOnlineLeave", "Leave", new Color(0.24f, 0.20f, 0.24f, 1f), new Vector2(0f, -276f), new Vector2(380f, 76f), 28, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            RoatanMatchSessionStore.ClearSession();
            pendingSavedSession = null;
            onlineEndOverlay.SetActive(false);
            subscribedClient?.AddToSendQueue(MessageFrameBuilder.LeaveGameFrame());
            ShowSubView(hubView);
            RequestLobbiesList();
        });
        onlineEndOverlay.SetActive(false);

        // Wood Hand Dock
        GameObject handDock = new GameObject("OnlineHandDock");
        handDock.transform.SetParent(onlineGameView.transform, false);
        RectTransform dockRect = handDock.AddComponent<RectTransform>();
        dockRect.anchorMin = new Vector2(0f, 0f);
        dockRect.anchorMax = new Vector2(1f, 0f);
        dockRect.pivot = new Vector2(0.5f, 0f);
        dockRect.sizeDelta = new Vector2(0f, 240f);
        dockRect.anchoredPosition = new Vector2(0f, 105f);
        handDock.AddComponent<Image>().sprite = RoatanVisualAssets.WoodFrameSprite;

        GameObject scrollObj = new GameObject("HandScroll");
        scrollObj.transform.SetParent(handDock.transform, false);
        RectTransform srRect = scrollObj.AddComponent<RectTransform>();
        srRect.anchorMin = Vector2.zero;
        srRect.anchorMax = Vector2.one;
        srRect.offsetMin = new Vector2(14f, 10f);
        srRect.offsetMax = new Vector2(-14f, -10f);
        scrollObj.AddComponent<RectMask2D>();
        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = true;
        sr.vertical = false;

        GameObject hcObj = new GameObject("HandContent");
        hcObj.transform.SetParent(scrollObj.transform, false);
        onlineHandContent = hcObj.AddComponent<RectTransform>();
        HorizontalLayoutGroup hlg = hcObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 12f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hcObj.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = onlineHandContent;
        sr.viewport = srRect;
    }

    private void BuildRejoinModal(Transform safeRoot)
    {
        rejoinModal = CreateStretchPanel(safeRoot, "MatchRejoinModal", new Color(0f, 0f, 0f, 0.78f));

        GameObject card = CreateCard(rejoinModal.transform, "RejoinCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(880f, 680f));
        CreateText(card.transform, "RejoinTitle", "Match in Progress — Rejoin?", 40, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 245f), new Vector2(800f, 64f));
        rejoinModalBodyLabel = CreateText(card.transform, "RejoinBody", "", 27, FontStyles.Normal, Color.white, new Vector2(0f, 92f), new Vector2(780f, 200f));
        rejoinModalTimerLabel = CreateText(card.transform, "RejoinTimer", "Time remaining to rejoin: 40s", 30, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, -35f), new Vector2(780f, 52f));

        rejoinConfirmBtn = CreatePillButton(card.transform, "BtnRejoinMatch", "Rejoin Match", RoatanVisualAssets.PalmGreen, new Vector2(0f, -145f), new Vector2(540f, 96f), 34, RequestRejoinMatch);
        CreatePillButton(card.transform, "BtnAbandonMatch", "Abandon", RoatanVisualAssets.SunsetCoral, new Vector2(0f, -258f), new Vector2(400f, 80f), 28, RequestAbandonMatch);

        rejoinModal.SetActive(false);
    }

    // ==================== HELPERS ====================

    private void ShowSubView(GameObject target)
    {
        authView.SetActive(target == authView);
        hubView.SetActive(target == hubView);
        lobbyView.SetActive(target == lobbyView);
        onlineGameView.SetActive(target == onlineGameView);
    }

    private void SelectAuthTab(bool isSignIn)
    {
        signInPanel.SetActive(isSignIn);
        registerPanel.SetActive(!isSignIn);
        signInTabImg.color = isSignIn ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f);
        registerTabImg.color = !isSignIn ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f);
    }

    private void RefreshConnectionBadge()
    {
        if (connectionBadgeText == null) return;
        bool connected = Networking.Instance != null && Networking.Instance.IsConnected && !Networking.Instance.IsOfflineMode;
        if (isConnecting)
        {
            connectionBadgeText.text = RoatanLocalization.T("connecting");
            connectionBadgeBg.color = new Color(0.55f, 0.42f, 0.10f, 0.95f);
        }
        else if (connected)
        {
            connectionBadgeText.text = $"{RoatanLocalization.T("server_connected")} ({Networking.Instance.CurrentHost}:{Networking.Instance.CurrentPort})";
            connectionBadgeBg.color = new Color(0.11f, 0.45f, 0.28f, 0.95f);
        }
        else
        {
            connectionBadgeText.text = RoatanLocalization.T("server_offline");
            connectionBadgeBg.color = new Color(0.58f, 0.19f, 0.16f, 0.95f);
        }
    }

    private void RefreshCreateLobbySelections()
    {
        foreach (KeyValuePair<TableStyle, Image> kv in createTableStyleBtns)
        {
            kv.Value.color = kv.Key == createTableStyle ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f);
        }

        foreach (KeyValuePair<MatchFormat, Image> kv in createMatchFormatBtns)
        {
            bool active = kv.Key == createMatchFormat;
            kv.Value.color = active ? RoatanVisualAssets.IslandGold : new Color(0.14f, 0.22f, 0.28f, 1f);
            TMP_Text lbl = kv.Value.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.color = active ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
            }
        }

        bool isSixLov = createMatchFormat == MatchFormat.SIX_LOV;
        if (createOneOnePlayoffBtn != null)
        {
            createOneOnePlayoffBtn.gameObject.SetActive(isSixLov);
            if (createOneOnePlayoffImg != null)
            {
                createOneOnePlayoffImg.color = createOneOnePlayoff ? RoatanVisualAssets.PalmGreen : new Color(0.14f, 0.22f, 0.28f, 1f);
            }
            if (createOneOnePlayoffLabel != null)
            {
                createOneOnePlayoffLabel.text = createOneOnePlayoff
                    ? "4P (2v2 Partners)  •  1-1 Playoff: ON"
                    : "4P (2v2 Partners)  •  1-1 Playoff: OFF";
            }
        }

        if (isSixLov)
        {
            createLobbyMaxPlayers = 4;
            foreach (KeyValuePair<int, GameObject> kv in createMaxPlayerObjs) kv.Value.SetActive(false);
        }
        else if (createMatchFormat == MatchFormat.TWO_BEST)
        {
            if (createLobbyMaxPlayers == 3) createLobbyMaxPlayers = 2;
            if (createMaxPlayerObjs.ContainsKey(2)) createMaxPlayerObjs[2].SetActive(true);
            if (createMaxPlayerObjs.ContainsKey(3)) createMaxPlayerObjs[3].SetActive(false);
            if (createMaxPlayerObjs.ContainsKey(4)) createMaxPlayerObjs[4].SetActive(true);
        }
        else
        {
            foreach (KeyValuePair<int, GameObject> kv in createMaxPlayerObjs) kv.Value.SetActive(true);
        }

        foreach (KeyValuePair<int, Image> kv in createMaxPlayerBtns)
        {
            kv.Value.color = kv.Key == createLobbyMaxPlayers ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f);
        }
    }

    private static string FormatLobbyRulesBadge(MatchFormat? format, TableStyle? style, bool? oneOnePlayoff)
    {
        string fmtStr = !format.HasValue ? "Classic"
            : (format.Value == MatchFormat.SIX_LOV ? "Six-Lov (2v2)"
            : (format.Value == MatchFormat.TWO_BEST ? "Two-Best" : "Cut-Throat (FFA)"));
        string styleStr = !style.HasValue ? "Draw"
            : (style.Value == TableStyle.BLOCK ? "Block (Cerrado)" : "Classic Draw");
        string playoffStr = (format == MatchFormat.SIX_LOV && oneOnePlayoff == true) ? "  •  1-1 Playoff" : "";
        return $"{fmtStr}  •  {styleStr}{playoffStr}";
    }

    private void RefreshLobbyView()
    {
        if (currentLobby == null) return;
        lobbyTitleLabel.text = currentLobby.Name;
        string codePart = !string.IsNullOrEmpty(currentLobby.Code) ? $"  •  Code: {currentLobby.Code}" : "";
        string rulesBadge = FormatLobbyRulesBadge(currentLobby.MatchFormat, currentLobby.TableStyle, currentLobby.OneOnePlayoff);
        lobbyInfoLabel.text = $"{rulesBadge}\nPlayers: {currentLobby.Players.Count}/{currentLobby.MaxNumberOfPlayer}{codePart}";

        foreach (GameObject go in spawnedLobbyPlayerRows) Destroy(go);
        spawnedLobbyPlayerRows.Clear();

        foreach (PlayerDto p in currentLobby.Players)
        {
            GameObject row = CreateCard(lobbyPlayersContent, $"P_{p.Id}", new Color(0.10f, 0.26f, 0.32f, 0.95f), Vector2.zero, new Vector2(840f, 90f));
            row.AddComponent<LayoutElement>().preferredHeight = 90f;
            CreateText(row.transform, "PName", $"{p.Name}   •   Elo {p.Elo}", 28, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(780f, 64f));
            spawnedLobbyPlayerRows.Add(row);
        }

        RefreshLobbyReadyButton();
    }

    private void RefreshLobbyReadyButton()
    {
        if (readyBtnLabel == null) return;
        readyBtnLabel.text = localReady ? RoatanLocalization.T("not_ready") : RoatanLocalization.T("ready");
        readyBtnImg.color = localReady ? RoatanVisualAssets.IslandGold : RoatanVisualAssets.PalmGreen;
    }

    private void RefreshLocalizedTexts()
    {
        if (titleLabel != null) titleLabel.text = RoatanLocalization.T("online_title");
        if (backBtnLabel != null) backBtnLabel.text = RoatanLocalization.T("home_back");
        if (connectBtnLabel != null) connectBtnLabel.text = RoatanLocalization.T("connect_retry");
        if (signInTabLabel != null) signInTabLabel.text = RoatanLocalization.T("sign_in");
        if (signInSubmitLabel != null) signInSubmitLabel.text = RoatanLocalization.T("sign_in");
        if (registerTabLabel != null) registerTabLabel.text = RoatanLocalization.T("create_account");
        if (registerSubmitLabel != null) registerSubmitLabel.text = RoatanLocalization.T("create_account");
        if (signOutBtnLabel != null) signOutBtnLabel.text = RoatanLocalization.T("sign_out");
        if (quickRankedLabel != null) quickRankedLabel.text = RoatanLocalization.T("quick_ranked");
        if (createLobbyBtnLabel != null) createLobbyBtnLabel.text = RoatanLocalization.T("create_lobby");
        if (joinCodeBtnLabel != null) joinCodeBtnLabel.text = RoatanLocalization.T("join_code");
        if (refreshLobbiesBtnLabel != null) refreshLobbiesBtnLabel.text = RoatanLocalization.T("refresh_lobbies");
        if (leaveLobbyBtnLabel != null) leaveLobbyBtnLabel.text = RoatanLocalization.T("leave_lobby");
        RefreshLobbyReadyButton();
        RefreshConnectionBadge();
    }

    private void SetFeedback(string msg, Color color)
    {
        if (feedbackToast == null) return;
        feedbackToast.text = msg;
        feedbackToast.color = color;
    }

    private static string GetDefaultHost()
    {
        return Application.platform == RuntimePlatform.Android ? "10.0.2.2" : "127.0.0.1";
    }

    private GameObject CreateStretchPanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        if (color.a > 0.001f)
        {
            obj.AddComponent<Image>().color = color;
        }
        return obj;
    }

    private GameObject CreateCard(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image img = obj.AddComponent<Image>();
        img.sprite = RoatanVisualAssets.RoundedCardSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        return obj;
    }

    private Button CreatePillButton(Transform parent, string name, string label, Color color, Vector2 pos, Vector2 size, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        GameObject obj = CreateCard(parent, name, color, pos, size);
        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = obj.GetComponent<Image>();
        btn.onClick.AddListener(onClick);
        TMP_Text txt = CreateText(obj.transform, "Label", label, fontSize, FontStyles.Bold, Color.white, Vector2.zero, size - new Vector2(18f, 10f));
        txt.raycastTarget = false;
        return btn;
    }

    private TMP_InputField CreateInputField(Transform parent, string name, string placeholder, string initialValue, Vector2 pos, Vector2 size, bool isPassword = false)
    {
        GameObject obj = CreateCard(parent, name, new Color(0.04f, 0.11f, 0.16f, 1f), pos, size);

        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(obj.transform, false);
        RectTransform taRect = textArea.AddComponent<RectTransform>();
        taRect.anchorMin = Vector2.zero;
        taRect.anchorMax = Vector2.one;
        taRect.offsetMin = new Vector2(22f, 8f);
        taRect.offsetMax = new Vector2(-22f, -8f);
        textArea.AddComponent<RectMask2D>();

        TMP_Text ph = CreateText(textArea.transform, "Placeholder", placeholder, 26, FontStyles.Italic, new Color(0.55f, 0.68f, 0.74f, 0.85f), Vector2.zero, Vector2.zero);
        RectTransform phRect = ph.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        ph.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text txt = CreateText(textArea.transform, "Text", "", 27, FontStyles.Normal, Color.white, Vector2.zero, Vector2.zero);
        RectTransform tRect = txt.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;
        txt.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_InputField input = obj.AddComponent<TMP_InputField>();
        input.textViewport = taRect;
        input.textComponent = txt;
        input.placeholder = ph;
        input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        input.text = initialValue;

        return input;
    }

    private TMP_Text CreateText(Transform parent, string name, string content, int fontSize, FontStyles style, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) tmp.font = uiFont;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        return tmp;
    }
}
