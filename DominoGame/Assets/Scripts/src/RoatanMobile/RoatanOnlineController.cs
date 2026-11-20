using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

    private GameObject onlineRoot;
    private GameObject authView;
    private GameObject hubView;
    private GameObject lobbyView;
    private GameObject onlineGameView;

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
    private readonly Dictionary<int, Image> createMaxPlayerBtns = new Dictionary<int, Image>();

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
    private readonly List<GameObject> onlineBoardObjs = new List<GameObject>();
    private readonly List<GameObject> onlineSlotObjs = new List<GameObject>();
    private readonly List<GameObject> onlineHandObjs = new List<GameObject>();
    private readonly List<DominoDto> onlineHand = new List<DominoDto>();
    private PossibleMovesDto onlinePossibleMoves;
    private DominoDto onlineSelectedDomino;
    private long onlineActiveUserId = -1;
    private readonly GameGridDto onlineGridValue = new GameGridDto(60, 60);

    private IClient subscribedClient;
    private bool isConnecting;
    private bool isSignedIn;

    public void Initialize(RectTransform safeRoot, TMP_FontAsset font, Action returnHomeCallback)
    {
        uiFont = font;
        onReturnHome = returnHomeCallback;

        BuildOnlineScreen(safeRoot);
        RoatanLocalization.LanguageChanged += RefreshLocalizedTexts;
        RefreshLocalizedTexts();
        onlineRoot.SetActive(false);
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
    }

    public void HideOnline()
    {
        onlineRoot.SetActive(false);
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
        }
        else
        {
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

        subscribedClient.AddToSendQueue(MessageFrameBuilder.CreateLobbyFrame(new CreateLobbyDto
        {
            Name = lobbyName,
            MaxNumberOfPlayer = createLobbyMaxPlayers,
            Public = true
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
            GameObject row = CreateCard(lobbiesListContent, $"Lobby_{lobby.Id}", new Color(0.08f, 0.23f, 0.29f, 0.95f), Vector2.zero, new Vector2(860f, 92f));
            row.AddComponent<LayoutElement>().preferredHeight = 92f;

            CreateText(row.transform, "LName", $"{lobby.Name}  ({lobby.NumberOfPlayer}/{lobby.MaxNumberOfPlayer})", 26, FontStyles.Bold, Color.white, new Vector2(-130f, 0f), new Vector2(540f, 64f)).alignment = TextAlignmentOptions.MidlineLeft;
            CreatePillButton(row.transform, "BtnJoin", "Join", RoatanVisualAssets.PalmGreen, new Vector2(315f, 0f), new Vector2(180f, 66f), 25, () =>
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
        ClearOnlineBoardVisuals();
        onlineHand.Clear();
        onlinePossibleMoves = null;
        onlineSelectedDomino = null;
        onlineActiveUserId = -1;

        for (int r = 0; r < 60; r++)
        {
            for (int c = 0; c < 60; c++)
            {
                onlineGridValue[r][c] = GameGridDto.EmptySpotValue;
            }
        }

        ShowSubView(onlineGameView);
        onlineMatchHeader.text = currentLobby != null ? $"ONLINE  •  {currentLobby.Name}" : "ONLINE MATCH";
        onlineMatchStatus.text = "Waiting for server to deal dominoes...";
        subscribedClient?.AddToSendQueue(MessageFrameBuilder.ReadyInGameFrame());
    }

    private void OnOnlineHandDistributed(HandDto dto)
    {
        onlineHand.Clear();
        if (dto?.Dominoes != null)
        {
            onlineHand.AddRange(dto.Dominoes);
        }
        RebuildOnlineHandUI();
    }

    private void OnOnlinePlayerPlays(PlayerPlayDto dto)
    {
        if (dto == null) return;

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
            RoatanSoundFX.Instance?.PlayPlace();
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
        onlineActiveUserId = dto.IdUserPlaying;
        bool isMyTurn = subscribedClient?.User != null && dto.IdUserPlaying == subscribedClient.User.Id;
        onlineDrawBtn.gameObject.SetActive(isMyTurn);

        string playerName = currentLobby?.Players.FirstOrDefault(p => p.Id == dto.IdUserPlaying)?.Name ?? $"Player {dto.IdUserPlaying}";
        onlineMatchStatus.text = isMyTurn ? "Your turn! Tap a glowing domino to place." : $"{playerName}'s turn...";
    }

    private void OnOnlinePossibleMoves(PossibleMovesDto dto)
    {
        onlinePossibleMoves = dto;
        RebuildOnlineHandUI();
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
        RoatanSoundFX.Instance?.PlayWin();
        string winnerName = "Match Complete";
        if (dto?.Rankings != null && dto.Rankings.Count > 0 && currentLobby != null)
        {
            PlayerDto w = currentLobby.Players.FirstOrDefault(p => p.Id == dto.Rankings[0].Id);
            if (w != null) winnerName = $"Winner: {w.Name}!";
        }
        onlineMatchStatus.text = winnerName;
        onlineDrawBtn.gameObject.SetActive(false);
    }

    private void OnOnlineLeaveGameResponse(ResponseDto dto)
    {
        ShowSubView(hubView);
        RequestLobbiesList();
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

        GameObject profileBar = CreateCard(hubView.transform, "HubProfileCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, 575f), new Vector2(940f, 106f));
        hubWelcomeLabel = CreateText(profileBar.transform, "HubWelcome", "Online Player", 28, FontStyles.Bold, Color.white, new Vector2(-140f, 0f), new Vector2(580f, 64f));
        hubWelcomeLabel.alignment = TextAlignmentOptions.MidlineLeft;
        Button outBtn = CreatePillButton(profileBar.transform, "BtnSignOut", "Sign Out", RoatanVisualAssets.SunsetCoral, new Vector2(335f, 0f), new Vector2(210f, 68f), 24, SubmitLogout);
        signOutBtnLabel = outBtn.GetComponentInChildren<TMP_Text>();

        Button rankedBtn = CreatePillButton(hubView.transform, "BtnQuickRanked", "Quick Ranked Match", RoatanVisualAssets.IslandGold, new Vector2(0f, 445f), new Vector2(940f, 102f), 34, RequestQuickRanked);
        quickRankedLabel = rankedBtn.GetComponentInChildren<TMP_Text>();
        quickRankedLabel.color = new Color(0.10f, 0.10f, 0.12f, 1f);

        // Create Lobby Card
        GameObject createCard = CreateCard(hubView.transform, "CreateLobbyCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, 240f), new Vector2(940f, 250f));
        createLobbyNameInput = CreateInputField(createCard.transform, "LobbyNameInput", "New Lobby Name", "Roatan Table", new Vector2(0f, 65f), new Vector2(860f, 70f));

        int[] maxChoices = { 2, 3, 4 };
        float[] xs = { -290f, -110f, 70f };
        for (int i = 0; i < maxChoices.Length; i++)
        {
            int count = maxChoices[i];
            Button b = CreatePillButton(createCard.transform, $"MaxP_{count}", $"{count}P", count == createLobbyMaxPlayers ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(xs[i], -45f), new Vector2(150f, 68f), 25, () =>
            {
                createLobbyMaxPlayers = count;
                foreach (KeyValuePair<int, Image> kv in createMaxPlayerBtns)
                {
                    kv.Value.color = kv.Key == createLobbyMaxPlayers ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f);
                }
            });
            createMaxPlayerBtns[count] = b.GetComponent<Image>();
        }

        Button createBtn = CreatePillButton(createCard.transform, "BtnCreateLobby", "Create Lobby", RoatanVisualAssets.PalmGreen, new Vector2(300f, -45f), new Vector2(270f, 68f), 25, RequestCreateLobby);
        createLobbyBtnLabel = createBtn.GetComponentInChildren<TMP_Text>();

        // Join by Code Row
        GameObject codeCard = CreateCard(hubView.transform, "CodeCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, 55f), new Vector2(940f, 100f));
        joinCodeInput = CreateInputField(codeCard.transform, "JoinCodeInput", "Private Lobby Code", "", new Vector2(-150f, 0f), new Vector2(560f, 68f));
        Button codeBtn = CreatePillButton(codeCard.transform, "BtnJoinCode", "Join Code", RoatanVisualAssets.BrightLagoon, new Vector2(290f, 0f), new Vector2(280f, 68f), 25, RequestJoinByCode);
        joinCodeBtnLabel = codeBtn.GetComponentInChildren<TMP_Text>();

        // Public Lobbies Scroll List
        GameObject listCard = CreateCard(hubView.transform, "LobbiesCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, -390f), new Vector2(940f, 730f));
        Button refreshBtn = CreatePillButton(listCard.transform, "BtnRefreshLobbies", "Refresh Lobbies", RoatanVisualAssets.BrightLagoon, new Vector2(0f, 305f), new Vector2(380f, 64f), 24, RequestLobbiesList);
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
        lobbyInfoLabel = CreateText(card.transform, "LInfo", "", 25, FontStyles.Normal, new Color(0.84f, 0.95f, 0.98f), new Vector2(0f, 445f), new Vector2(860f, 46f));
        lobbyTimerLabel = CreateText(card.transform, "LTimer", "Waiting for players...", 26, FontStyles.Italic, Color.white, new Vector2(0f, 385f), new Vector2(860f, 46f));

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
            if (subscribedClient?.User == null || onlineActiveUserId != subscribedClient.User.Id) return;
            subscribedClient.AddToSendQueue(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
            {
                IdUserPlayed = subscribedClient.User.Id,
                PlayActionType = PlayActionType.Draw
            }));
        });
        RectTransform dRect = onlineDrawBtn.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.5f, 0f);
        dRect.anchorMax = new Vector2(0.5f, 0f);
        dRect.pivot = new Vector2(0.5f, 0f);
        dRect.anchoredPosition = new Vector2(0f, 430f);
        onlineDrawBtn.GetComponentInChildren<TMP_Text>().color = Color.black;
        onlineDrawBtn.gameObject.SetActive(false);

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

    private void RefreshLobbyView()
    {
        if (currentLobby == null) return;
        lobbyTitleLabel.text = currentLobby.Name;
        string codePart = !string.IsNullOrEmpty(currentLobby.Code) ? $"  •  Code: {currentLobby.Code}" : "";
        lobbyInfoLabel.text = $"Players: {currentLobby.Players.Count}/{currentLobby.MaxNumberOfPlayer}{codePart}";

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
