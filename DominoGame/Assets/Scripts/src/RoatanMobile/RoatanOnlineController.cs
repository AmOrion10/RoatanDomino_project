using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Newtonsoft.Json.Linq;

using TMPro;

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public enum RoatanServerPreset
{
    Production = 0,
    CustomTunnel = 1,
    Localhost = 2
}

public class RoatanOnlineController : MonoBehaviour
{
    private const string PrefHostKey = "RoatanDomino_ServerHost";
    private const string PrefPortKey = "RoatanDomino_ServerPort";
    private const string PrefServerUrlKey = "RoatanDomino_ServerUrl";
    private const string PrefServerPresetKey = "RoatanDomino_ServerPreset";
    private const string PrefCustomServerUrlKey = "RoatanDomino_CustomServerUrl";
    private const string PrefSocialDeviceIdKey = "RoatanDomino_SocialDeviceId";

    private static readonly Regex EmailValidationRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex UsernameValidationRegex = new Regex(@"^[A-Za-z0-9_]{3,16}$", RegexOptions.Compiled);
    private static readonly Regex HasCapitalRegex = new Regex(@"[A-Z]", RegexOptions.Compiled);
    private static readonly Regex HasNumberOrSymbolRegex = new Regex(@"[\d\W_]", RegexOptions.Compiled);

    public const string ProductionServerUrl = "https://domino.roatandomino.com";
    public static event Action ServerEndpointChanged;

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
    private TMP_Text editServerPillBtnLabel;
    private GameObject serverSettingsDrawer;
    private bool serverSettingsExpanded;
    private TMP_Text feedbackToast;

    // Auth view
    private TMP_InputField serverUrlInput;
    private TMP_Text serverEndpointPreviewLabel;
    private readonly Dictionary<RoatanServerPreset, Image> serverPresetBtns = new Dictionary<RoatanServerPreset, Image>();
    private RoatanServerPreset activeServerPreset = RoatanServerPreset.Localhost;
    private bool suppressServerInputEvents;
    private TMP_Text connectBtnLabel;
    private Image signInTabImg;
    private Image registerTabImg;
    private TMP_Text signInTabLabel;
    private TMP_Text registerTabLabel;
    private GameObject authErrorBannerObj;
    private TMP_Text authErrorBannerText;
    private GameObject signInPanel;
    private GameObject registerPanel;
    private TMP_InputField loginUserInput;
    private TMP_InputField loginPassInput;
    private TMP_Text loginShowPassLabel;
    private bool loginPassVisible;
    private TMP_InputField regEmailInput;
    private TMP_InputField regUserInput;
    private TMP_InputField regPassInput;
    private TMP_Text regShowPassLabel;
    private bool regPassVisible;
    private TMP_Text passRuleLenLabel;
    private TMP_Text passRuleCapLabel;
    private TMP_Text passRuleNumSymLabel;
    private TMP_InputField regBirthInput;
    private TMP_Text signInSubmitLabel;
    private TMP_Text registerSubmitLabel;

    // Hub view (Tables + Players Roster)
    private Image hubAvatarImg;
    private TMP_Text hubAvatarInitialsLabel;
    private TMP_Text hubWelcomeLabel;
    private TMP_Text signOutBtnLabel;
    private TMP_Text quickRankedLabel;

    // OAuth Account Chooser & Browser Wait Modal
    private Coroutine oauthFlowCoroutine;
    private string pendingOAuthProvider;
    private string pendingOAuthState;
    private bool oauthCodeSubmitted;
    private GameObject oauthWaitingModal;
    private TMP_Text oauthWaitingLabel;

    // Player Profile & Delete Account Modals
    private GameObject profileModal;
    private Image profileModalAvatarImg;
    private TMP_Text profileModalAvatarInitials;
    private TMP_Text profileModalNameLabel;
    private TMP_Text profileModalEmailLabel;
    private TMP_Text profileModalProviderBadge;
    private TMP_Text profileModalStatsLabel;
    private TMP_Text profileModalStatusLabel;
    private Button profileAdoptSocialBtn;

    private GameObject deleteAccountModal;
    private TMP_InputField deleteConfirmInput;
    private TMP_Text deleteConfirmBtnLabel;
    private TMP_Text deleteConfirmErrorLabel;
    private bool deleteConfirmSecondTapArmed;
    private TMP_Text createLobbyBtnLabel;
    private TMP_Text joinCodeBtnLabel;
    private TMP_Text refreshLobbiesBtnLabel;
    private TMP_InputField createLobbyNameInput;
    private TMP_InputField joinCodeInput;
    private enum HubBrowserTab
    {
        Tables = 0,
        Players = 1,
        Leaderboard = 2,
        Teams = 3,
        Chat = 4
    }

    private sealed class PlayerActionProfileData
    {
        public long UserId;
        public string Username;
        public string AvatarBase64;
        public int Elo;
        public int Wins;
        public int Losses;
        public int WinAssists;
        public int TotalMoves;
        public int OptimalMoves;
        public double AverageTurnSeconds;
        public bool IsOnline;
        public long? TeamId;
        public string TeamTag;
    }

    private static readonly int[] TeamMinEloOptions = { 100, 500, 900, 1200, 1500, 1800, 2100 };
    private static readonly int[] OpenChallengeMinEloOptions = { 100, 900, 1200, 1500 };

    private OpenTeamChallengesDto latestOpenChallenges;
    private MatchFormat openChSelectedMode = MatchFormat.SIX_LOV;
    private TableStyle openChSelectedVariant = TableStyle.DRAW;
    private int openChMinEloIndex = 0;
    private bool openChRequireHumansOnly = false;

    private Image hubTablesTabImg;
    private Image hubPlayersTabImg;
    private Image hubLeaderboardTabImg;
    private Image hubTeamsTabImg;
    private Image hubChatTabImg;
    private TMP_Text hubTablesTabLabel;
    private TMP_Text hubPlayersTabLabel;
    private TMP_Text hubLeaderboardTabLabel;
    private TMP_Text hubTeamsTabLabel;
    private TMP_Text hubChatTabLabel;
    private GameObject hubTablesScrollObj;
    private GameObject hubPlayersScrollObj;
    private GameObject hubLeaderboardViewObj;
    private GameObject hubTeamsScrollObj;
    private GameObject hubChatViewObj;
    private HubBrowserTab activeHubTab = HubBrowserTab.Tables;
    private RectTransform lobbiesListContent;
    private RectTransform playersRosterContent;
    private RectTransform leaderboardListContent;
    private RectTransform teamsContentRoot;
    private GameObject leaderboardSelfBannerCard;
    private Image leaderboardSelfAvatarImg;
    private TMP_Text leaderboardSelfAvatarInitials;
    private TMP_Text leaderboardSelfRankLabel;
    private TMP_Text leaderboardSelfNameTierLabel;
    private TMP_Text leaderboardSelfStatsLabel;
    private bool leaderboardShowingTeams;
    private Image lbPlayersSubTabImg;
    private Image lbTeamsSubTabImg;
    private TMP_Text lbPlayersSubTabLabel;
    private TMP_Text lbTeamsSubTabLabel;
    private GameObject lbPlayersSubViewObj;
    private GameObject lbTeamsSubViewObj;
    private GameObject teamLeaderboardSelfCard;
    private TMP_Text teamLeaderboardSelfRankLabel;
    private TMP_Text teamLeaderboardSelfNameTierLabel;
    private TMP_Text teamLeaderboardSelfStatsLabel;
    private RectTransform teamLeaderboardListContent;
    private readonly List<GameObject> spawnedLobbyRows = new List<GameObject>();
    private readonly List<GameObject> spawnedRosterRows = new List<GameObject>();
    private readonly List<GameObject> spawnedLeaderboardRows = new List<GameObject>();
    private readonly List<GameObject> spawnedTeamLeaderboardRows = new List<GameObject>();
    private readonly List<GameObject> spawnedTeamSectionObjs = new List<GameObject>();
    private readonly List<PlayerPresenceDto> latestRoster = new List<PlayerPresenceDto>();
    private LeaderboardDto latestLeaderboard;
    private TeamLeaderboardDto latestTeamLeaderboard;
    private MyTeamDetailsDto myTeamDetails;
    private TeamsListDto latestTeamsList;
    private string createTeamNameDraft = string.Empty;
    private string createTeamTagDraft = string.Empty;
    private TeamJoinPolicy createTeamJoinPolicy = TeamJoinPolicy.Open;
    private int createTeamMinEloIndex = 0;
    private TeamActivityRequirement createTeamActivityReq = TeamActivityRequirement.Any;
    private string teamSearchQuery = string.Empty;
    private string editTeamNameDraft = string.Empty;
    private string editTeamTagDraft = string.Empty;
    private TeamJoinPolicy editTeamJoinPolicy = TeamJoinPolicy.Open;
    private int editTeamMinEloIndex = 0;
    private TeamActivityRequirement editTeamActivityReq = TeamActivityRequirement.Any;
    private long editTeamDraftTeamId = -1;
    private string teamInviteUsernameDraft = string.Empty;
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

    // In-Table Waiting Room (Visual 4-Seat / 2-Seat Table Diagram, Host Bot Controls & Invites)
    private TMP_Text lobbyTitleLabel;
    private TMP_Text lobbyInfoLabel;
    private TMP_Text lobbyTimerLabel;
    private RectTransform tableDiagramRoot;
    private TMP_Text tableFeltLabel;
    private readonly List<GameObject> spawnedSeatCards = new List<GameObject>();
    private GameObject hostBotDifficultyRow;
    private readonly Dictionary<string, Image> hostBotDiffBtns = new Dictionary<string, Image>();
    private string selectedHostBotDifficulty = "Pro";
    private Button startMatchBtn;
    private Image startMatchBtnImg;
    private TMP_Text startMatchBtnLabel;
    private Button readyBtnObj;
    private TMP_Text readyBtnLabel;
    private Image readyBtnImg;
    private TMP_Text leaveLobbyBtnLabel;
    private bool localReady;
    private LobbyDto currentLobby;

    // Table Invite Modal (Incoming + Send Invite Popover)
    private GameObject incomingInviteModal;
    private TMP_Text incomingInviteBodyLabel;
    private TableInviteDto pendingIncomingInvite;
    private GameObject invitePlayersModal;
    private RectTransform invitePlayersListContent;
    private readonly List<GameObject> spawnedInviteModalRows = new List<GameObject>();

    // Team Challenge Modals (Send Challenge + Incoming Crew Challenge)
    private GameObject challengeTeamModal;
    private TMP_Text challengeTeamModalTitle;
    private TeamLeaderboardEntryDto challengeTargetTeam;
    private MatchFormat challengeSelectedMode = MatchFormat.SIX_LOV;
    private TableStyle challengeSelectedVariant = TableStyle.DRAW;
    private Image challengeModeSixLovImg;
    private Image challengeModeTwoBestImg;
    private Image challengeVariantDrawImg;
    private Image challengeVariantBlockImg;
    private GameObject incomingTeamChallengeModal;
    private TMP_Text incomingTeamChallengeTitle;
    private TMP_Text incomingTeamChallengeBody;
    private Button incomingTeamChallengeAcceptBtn;
    private TMP_Text incomingTeamChallengeAcceptLabel;
    private Button incomingTeamChallengeDeclineBtn;
    private TMP_Text incomingTeamChallengeDeclineLabel;
    private TeamChallengeDto pendingTeamChallenge;

    // Live Match Spectating State & HUD
    private bool isSpectatingMatch;
    private int spectatingGameId;
    private int liveSpectatorCount;
    private string spectatingMatchTitle = string.Empty;
    private readonly List<PlayerDto> spectatingPlayers = new List<PlayerDto>();
    private GameObject onlineSpectatorBadgeObj;
    private TMP_Text onlineSpectatorBadgeLabel;
    private Button onlineLeaveSpectateBtn;

    // Player Action Card & Direct Player Challenge Modals
    private GameObject playerActionModal;
    private Image playerActionAvatarImg;
    private TMP_Text playerActionAvatarInitials;
    private TMP_Text playerActionNameTierLabel;
    private TMP_Text playerActionStatsLabel;
    private Button playerActionChallengeBtn;
    private Button playerActionMessageBtn;
    private Button playerActionInviteTeamBtn;
    private PlayerActionProfileData selectedActionPlayer;

    private GameObject directChallengeModal;
    private TMP_Text directChallengeModalTitle;
    private MatchFormat directChallengeMode = MatchFormat.TWO_BEST;
    private TableStyle directChallengeVariant = TableStyle.DRAW;
    private Image directChallengeTwoBestImg;
    private Image directChallengeCutThroatImg;
    private Image directChallengeDrawImg;
    private Image directChallengeBlockImg;

    private GameObject incomingDirectChallengeModal;
    private TMP_Text incomingDirectChallengeBody;
    private DirectChallengeDto pendingDirectChallenge;

    // Team Chat & Direct Messages UI & State
    private bool chatShowingDirectMessages;
    private long activeDmPartnerUserId;
    private string activeDmPartnerUsername = string.Empty;
    private Image chatTeamSubTabImg;
    private Image chatDmSubTabImg;
    private TMP_Text chatTeamSubTabLabel;
    private TMP_Text chatDmSubTabLabel;
    private TMP_Text chatActiveChannelHeader;
    private RectTransform chatDmPartnersContent;
    private GameObject chatDmPartnersStripObj;
    private RectTransform chatMessagesListContent;
    private TMP_InputField chatComposeInput;
    private readonly List<GameObject> spawnedChatPartnerChips = new List<GameObject>();
    private readonly List<GameObject> spawnedChatMessageRows = new List<GameObject>();
    private readonly List<ChatMessageDto> teamChatMessages = new List<ChatMessageDto>();
    private readonly Dictionary<long, List<ChatMessageDto>> dmThreadsByUserId = new Dictionary<long, List<ChatMessageDto>>();
    private readonly Dictionary<long, string> dmPartnerNamesByUserId = new Dictionary<long, string>();
    private int unreadTeamChatCount;
    private readonly Dictionary<long, int> unreadDmCountByUserId = new Dictionary<long, int>();

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
    private GameObject onlineEndEloBreakdownCard;
    private TMP_Text onlineEndEloBreakdownTitle;
    private TMP_Text onlineEndEloBreakdownBody;
    private MatchEloBreakdownDto latestMatchEloBreakdown;
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
        BuildIncomingInviteModal(safeRoot);
        BuildInvitePlayersModal(safeRoot);
        BuildChallengeTeamModal(safeRoot);
        BuildIncomingTeamChallengeModal(safeRoot);
        BuildPlayerActionModal(safeRoot);
        BuildDirectChallengeModal(safeRoot);
        BuildIncomingDirectChallengeModal(safeRoot);
        BuildOAuthWaitingModal(safeRoot);
        BuildProfileModal(safeRoot);
        BuildDeleteAccountModal(safeRoot);
        RoatanLocalization.LanguageChanged += RefreshLocalizedTexts;
        ServerEndpointChanged += SyncServerEndpointFieldsFromPrefs;
        Application.deepLinkActivated += OnDeepLinkActivated;
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
            if (!string.IsNullOrWhiteSpace(saved.ServerUrl))
            {
                string savedUrl = saved.ServerUrl;
                Task.Run(() => Networking.Instance.ConnectOnlineUrl(savedUrl));
            }
            else
            {
                string host = saved.ServerHost;
                int port = saved.ServerPort;
                Task.Run(() => Networking.Instance.ConnectOnline(host, port, false));
            }
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
        ServerEndpointChanged -= SyncServerEndpointFieldsFromPrefs;
        Application.deepLinkActivated -= OnDeepLinkActivated;
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
            RefreshHubProfileHeader();
            ShowSubView(hubView);
            RequestLobbiesList();
            RequestPlayerRoster();
            RequestLeaderboard();
            RequestTeamLeaderboard();
            RequestMyTeam();
            RequestTeamsList();
            RequestOpenTeamChallenges();
        }
        else
        {
            ShowSubView(authView);
            ClearAuthError();
            SetServerSettingsExpanded(false);
            if (Networking.Instance != null && !Networking.Instance.IsConnected && !isConnecting)
            {
                AttemptServerConnect();
            }
            if (!string.IsNullOrWhiteSpace(Application.absoluteURL))
            {
                OnDeepLinkActivated(Application.absoluteURL);
            }
        }

        RefreshConnectionBadge();
        SyncServerEndpointFieldsFromPrefs();
        RefreshLocalizedTexts();
        CheckPendingRejoinOnStartup();
    }

    public void ConnectConfiguredServer()
    {
        SyncServerEndpointFieldsFromPrefs();
        AttemptServerConnect();
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
        eh.MatchEloBreakdownEvent += OnMatchEloBreakdown;
        eh.LeaveGameResponseEvent += OnOnlineLeaveGameResponse;
        eh.CheckActiveMatchResponseEvent += OnCheckActiveMatchResponse;
        eh.RejoinMatchResponseEvent += OnRejoinMatchResponse;
        eh.AbandonMatchResponseEvent += OnAbandonMatchResponse;
        eh.SocialAuthResponseEvent += OnSocialAuthResponse;
        eh.GetPlayerRosterResponseEvent += OnGetPlayerRosterResponse;
        eh.PlayerRosterUpdatedEvent += OnPlayerRosterUpdated;
        eh.TableInviteReceivedEvent += OnTableInviteReceived;
        eh.SendTableInviteResponseEvent += OnSendTableInviteResponse;
        eh.SwitchTableSeatResponseEvent += OnTableActionLobbyUpdatedResponse;
        eh.AddTableBotResponseEvent += OnTableActionLobbyUpdatedResponse;
        eh.RemoveTableBotResponseEvent += OnTableActionLobbyUpdatedResponse;
        eh.StartTableMatchResponseEvent += OnStartTableMatchResponse;
        eh.TableStateUpdatedEvent += OnTableStateUpdated;
        eh.GetInfoProfileResponseEvent += OnGetInfoProfileResponse;
        eh.UpdateProfileAvatarResponseEvent += OnUpdateProfileAvatarResponse;
        eh.DeleteAccountResponseEvent += OnDeleteAccountResponse;
        eh.GetLeaderboardResponseEvent += OnGetLeaderboardResponse;
        eh.GetTeamsResponseEvent += OnGetTeamsResponse;
        eh.GetMyTeamResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.CreateTeamResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.UpdateTeamSettingsResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.JoinOrRequestTeamResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.RespondTeamJoinRequestResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.InviteToTeamResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.RespondTeamInviteResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.PromoteOrDemoteMemberResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.KickTeamMemberResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.LeaveTeamResponseEvent += OnTeamMutationOrDetailsResponse;
        eh.GetTeamLeaderboardResponseEvent += OnGetTeamLeaderboardResponse;
        eh.SendTeamChallengeResponseEvent += OnSendTeamChallengeResponse;
        eh.RespondTeamChallengeResponseEvent += OnRespondTeamChallengeResponse;
        eh.TeamChallengeNotificationEvent += OnTeamChallengeNotification;
        eh.JoinAsSpectatorResponseEvent += OnJoinAsSpectatorResponse;
        eh.LeaveSpectatorResponseEvent += OnLeaveSpectatorResponse;
        eh.SpectatorCountUpdatedEvent += OnSpectatorCountUpdated;
        eh.SendDirectChallengeResponseEvent += OnSendDirectChallengeResponse;
        eh.RespondDirectChallengeResponseEvent += OnRespondDirectChallengeResponse;
        eh.DirectChallengeNotificationEvent += OnDirectChallengeNotification;
        eh.SendSocialChatMessageResponseEvent += OnSendSocialChatMessageResponse;
        eh.GetChatHistoryResponseEvent += OnGetChatHistoryResponse;
        eh.ChatMessageReceivedEvent += OnChatMessageReceived;
        eh.GetOpenTeamChallengesResponseEvent += OnGetOpenTeamChallengesResponse;
        eh.PostOpenTeamChallengeResponseEvent += OnPostOpenTeamChallengeResponse;
        eh.CancelOpenTeamChallengeResponseEvent += OnCancelOpenTeamChallengeResponse;
        eh.RequestAcceptTeamChallengeResponseEvent += OnRequestAcceptTeamChallengeResponse;
        eh.AcceptOpenTeamChallengeResponseEvent += OnAcceptOpenTeamChallengeResponse;
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
        eh.MatchEloBreakdownEvent -= OnMatchEloBreakdown;
        eh.LeaveGameResponseEvent -= OnOnlineLeaveGameResponse;
        eh.CheckActiveMatchResponseEvent -= OnCheckActiveMatchResponse;
        eh.RejoinMatchResponseEvent -= OnRejoinMatchResponse;
        eh.AbandonMatchResponseEvent -= OnAbandonMatchResponse;
        eh.SocialAuthResponseEvent -= OnSocialAuthResponse;
        eh.GetPlayerRosterResponseEvent -= OnGetPlayerRosterResponse;
        eh.PlayerRosterUpdatedEvent -= OnPlayerRosterUpdated;
        eh.TableInviteReceivedEvent -= OnTableInviteReceived;
        eh.SendTableInviteResponseEvent -= OnSendTableInviteResponse;
        eh.SwitchTableSeatResponseEvent -= OnTableActionLobbyUpdatedResponse;
        eh.AddTableBotResponseEvent -= OnTableActionLobbyUpdatedResponse;
        eh.RemoveTableBotResponseEvent -= OnTableActionLobbyUpdatedResponse;
        eh.StartTableMatchResponseEvent -= OnStartTableMatchResponse;
        eh.TableStateUpdatedEvent -= OnTableStateUpdated;
        eh.GetInfoProfileResponseEvent -= OnGetInfoProfileResponse;
        eh.UpdateProfileAvatarResponseEvent -= OnUpdateProfileAvatarResponse;
        eh.DeleteAccountResponseEvent -= OnDeleteAccountResponse;
        eh.GetLeaderboardResponseEvent -= OnGetLeaderboardResponse;
        eh.GetTeamsResponseEvent -= OnGetTeamsResponse;
        eh.GetMyTeamResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.CreateTeamResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.UpdateTeamSettingsResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.JoinOrRequestTeamResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.RespondTeamJoinRequestResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.InviteToTeamResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.RespondTeamInviteResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.PromoteOrDemoteMemberResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.KickTeamMemberResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.LeaveTeamResponseEvent -= OnTeamMutationOrDetailsResponse;
        eh.GetTeamLeaderboardResponseEvent -= OnGetTeamLeaderboardResponse;
        eh.SendTeamChallengeResponseEvent -= OnSendTeamChallengeResponse;
        eh.RespondTeamChallengeResponseEvent -= OnRespondTeamChallengeResponse;
        eh.TeamChallengeNotificationEvent -= OnTeamChallengeNotification;
        eh.JoinAsSpectatorResponseEvent -= OnJoinAsSpectatorResponse;
        eh.LeaveSpectatorResponseEvent -= OnLeaveSpectatorResponse;
        eh.SpectatorCountUpdatedEvent -= OnSpectatorCountUpdated;
        eh.SendDirectChallengeResponseEvent -= OnSendDirectChallengeResponse;
        eh.RespondDirectChallengeResponseEvent -= OnRespondDirectChallengeResponse;
        eh.DirectChallengeNotificationEvent -= OnDirectChallengeNotification;
        eh.SendSocialChatMessageResponseEvent -= OnSendSocialChatMessageResponse;
        eh.GetChatHistoryResponseEvent -= OnGetChatHistoryResponse;
        eh.ChatMessageReceivedEvent -= OnChatMessageReceived;
        eh.GetOpenTeamChallengesResponseEvent -= OnGetOpenTeamChallengesResponse;
        eh.PostOpenTeamChallengeResponseEvent -= OnPostOpenTeamChallengeResponse;
        eh.CancelOpenTeamChallengeResponseEvent -= OnCancelOpenTeamChallengeResponse;
        eh.RequestAcceptTeamChallengeResponseEvent -= OnRequestAcceptTeamChallengeResponse;
        eh.AcceptOpenTeamChallengeResponseEvent -= OnAcceptOpenTeamChallengeResponse;
        subscribedClient = null;
    }

    private void AttemptServerConnect()
    {
        if (isConnecting || Networking.Instance == null) return;
        RoatanSoundFX.Instance?.PlayTap();

        string rawUrl = serverUrlInput != null && !string.IsNullOrWhiteSpace(serverUrlInput.text)
            ? serverUrlInput.text.Trim()
            : GetSavedServerUrl();

        if (!TryResolveServerEndpoint(
            rawUrl,
            out _,
            out string normalizedDisplayUrl,
            out _,
            out _,
            out _,
            out _,
            out string errorMessage))
        {
            SetFeedback($"Invalid server URL: {errorMessage}", RoatanVisualAssets.SunsetCoral);
            return;
        }

        RoatanServerPreset resolvedPreset = DetectPresetForUrl(normalizedDisplayUrl, activeServerPreset);
        SaveServerEndpoint(resolvedPreset, normalizedDisplayUrl);

        isConnecting = true;
        SetFeedback($"{RoatanLocalization.T("connecting")} ({normalizedDisplayUrl})", RoatanVisualAssets.IslandGold);
        RefreshConnectionBadge();

        Task.Run(() => Networking.Instance.ConnectOnlineUrl(normalizedDisplayUrl));
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
        ClearAuthError();

        string login = loginUserInput != null ? loginUserInput.text.Trim() : string.Empty;
        string pass = loginPassInput != null ? loginPassInput.text : string.Empty;

        if (string.IsNullOrWhiteSpace(login))
        {
            ShowAuthError("Please enter your username or email address.");
            return;
        }
        if (string.IsNullOrEmpty(pass))
        {
            ShowAuthError("Please enter your password.");
            return;
        }

        if (!EnsureConnectedForRequest())
        {
            ShowAuthError("Unable to reach server. Tap [Edit] above to check your Server URL.");
            return;
        }

        subscribedClient.AddToSendQueue(MessageFrameBuilder.ConnectionFrame(new LoggingDto
        {
            Login = login,
            Password = pass
        }));
        SetFeedback("Signing in...", RoatanVisualAssets.IslandGold);
    }

    private void SubmitRegister()
    {
        RoatanSoundFX.Instance?.PlayTap();
        ClearAuthError();

        string email = regEmailInput != null ? regEmailInput.text.Trim() : string.Empty;
        string username = regUserInput != null ? regUserInput.text.Trim() : string.Empty;
        string pass = regPassInput != null ? regPassInput.text : string.Empty;
        string birth = regBirthInput != null ? regBirthInput.text.Trim() : string.Empty;

        if (!ValidateRegistrationInputs(email, username, pass, birth, out string validationError))
        {
            ShowAuthError(validationError);
            return;
        }

        if (!EnsureConnectedForRequest())
        {
            ShowAuthError("Unable to reach server. Tap [Edit] above to check your Server URL.");
            return;
        }

        subscribedClient.AddToSendQueue(MessageFrameBuilder.RegistrationFrame(new RegistrationDto
        {
            Email = email,
            Login = username,
            Password = pass,
            Birthdate = birth
        }));
        SetFeedback("Creating account...", RoatanVisualAssets.IslandGold);
    }

    private void SubmitSocialAuth(string provider)
    {
        RoatanSoundFX.Instance?.PlayTap();
        ClearAuthError();

        if (!EnsureConnectedForRequest())
        {
            ShowAuthError("Unable to reach server. Tap [Edit] above to check your Server URL.");
            return;
        }

        CancelPendingOAuthFlow();
        oauthFlowCoroutine = StartCoroutine(BeginSocialOAuthCoroutine(provider));
    }

    private void CancelPendingOAuthFlow()
    {
        if (oauthFlowCoroutine != null)
        {
            StopCoroutine(oauthFlowCoroutine);
            oauthFlowCoroutine = null;
        }
        pendingOAuthProvider = null;
        pendingOAuthState = null;
        oauthCodeSubmitted = false;
        if (oauthWaitingModal != null) oauthWaitingModal.SetActive(false);
    }

    private string GetHttpBaseUrl()
    {
        string raw = Networking.Instance != null && !string.IsNullOrWhiteSpace(Networking.Instance.CurrentServerUrl)
            ? Networking.Instance.CurrentServerUrl
            : GetSavedServerUrl();

        string normalized = NormalizeDisplayUrl(raw);
        if (normalized.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "https://" + normalized.Substring(6);
        }
        else if (normalized.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "http://" + normalized.Substring(5);
        }

        if (normalized.EndsWith("/ws", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - 3);
        }

        return normalized.TrimEnd('/');
    }

    private IEnumerator BeginSocialOAuthCoroutine(string provider)
    {
        string httpBase = GetHttpBaseUrl();
        string startUrl = $"{httpBase}/auth/oauth/{provider.ToLowerInvariant()}/start?mode=json";
        SetFeedback($"Opening {provider} sign-in...", RoatanVisualAssets.IslandGold);

        using (UnityWebRequest req = UnityWebRequest.Get(startUrl))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                oauthFlowCoroutine = null;
                string unreachableMsg = $"Unable to reach server for {provider} Sign-In. Check your Server URL and try again.";
                ShowAuthError(unreachableMsg);
                SetFeedback(unreachableMsg, RoatanVisualAssets.SunsetCoral);
                yield break;
            }

            bool configured = false;
            bool idTokenConfigured = false;
            string clientId = string.Empty;
            string authorizeUrl = string.Empty;
            string state = string.Empty;
            string serverMsg = string.Empty;
            try
            {
                JObject json = JObject.Parse(req.downloadHandler.text);
                configured = json["configured"] != null && json["configured"].Value<bool>();
                idTokenConfigured = json["idTokenConfigured"] != null && json["idTokenConfigured"].Value<bool>();
                clientId = json["clientId"]?.ToString() ?? string.Empty;
                authorizeUrl = json["authorizeUrl"]?.ToString() ?? string.Empty;
                state = json["state"]?.ToString() ?? string.Empty;
                serverMsg = json["message"]?.ToString() ?? string.Empty;
            }
            catch
            {
                configured = false;
            }

            string defaultSetupMsg = string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase)
                ? "Google Sign-In is not configured on this server yet — add GOOGLE_CLIENT_ID to .env"
                : "Facebook Sign-In is not configured on this server yet — add FACEBOOK_APP_ID to .env";
            string effectiveSetupMsg = string.IsNullOrWhiteSpace(serverMsg) ? defaultSetupMsg : serverMsg;

            bool isGoogle = string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase);

            // Native Android Google Account Chooser (Credential Manager ID Token or AccountManager -> Chrome Custom Tab)
            if (isGoogle && RoatanGoogleSignIn.IsNativeAndroidChooserAvailable)
            {
                if (!configured && !idTokenConfigured)
                {
                    oauthFlowCoroutine = null;
                    ShowAuthError(effectiveSetupMsg);
                    SetFeedback(effectiveSetupMsg, RoatanVisualAssets.SunsetCoral);
                    yield break;
                }

                oauthFlowCoroutine = null;
                SetFeedback("Choose your Google account...", RoatanVisualAssets.IslandGold);

                RoatanGoogleSignIn.LaunchGoogleAccountChooser(
                    clientId,
                    onIdToken: idToken =>
                    {
                        oauthFlowCoroutine = StartCoroutine(SubmitGoogleIdTokenCoroutine(httpBase, idToken));
                    },
                    onAccountEmailSelected: selectedEmail =>
                    {
                        if (!configured || string.IsNullOrWhiteSpace(authorizeUrl) || string.IsNullOrWhiteSpace(state))
                        {
                            ShowAuthError(effectiveSetupMsg);
                            SetFeedback(effectiveSetupMsg, RoatanVisualAssets.SunsetCoral);
                            return;
                        }

                        string hintedUrl = authorizeUrl.IndexOf("login_hint=", StringComparison.OrdinalIgnoreCase) >= 0
                            ? authorizeUrl
                            : $"{authorizeUrl}&login_hint={Uri.EscapeDataString(selectedEmail)}";
                        LaunchOAuthBrowserAndPoll("Google", state, httpBase, hintedUrl);
                    },
                    onFallbackBrowser: () =>
                    {
                        if (!configured || string.IsNullOrWhiteSpace(authorizeUrl) || string.IsNullOrWhiteSpace(state))
                        {
                            ShowAuthError(effectiveSetupMsg);
                            SetFeedback(effectiveSetupMsg, RoatanVisualAssets.SunsetCoral);
                            return;
                        }

                        LaunchOAuthBrowserAndPoll("Google", state, httpBase, authorizeUrl);
                    },
                    onCancelOrError: reason =>
                    {
                        CancelPendingOAuthFlow();
                        if (string.Equals(reason, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                        {
                            SetFeedback("Google sign-in cancelled.", RoatanVisualAssets.IslandGold);
                        }
                        else
                        {
                            ShowAuthError(reason);
                            SetFeedback(reason, RoatanVisualAssets.SunsetCoral);
                        }
                    });
                yield break;
            }

            if (!configured || string.IsNullOrWhiteSpace(authorizeUrl) || string.IsNullOrWhiteSpace(state))
            {
                oauthFlowCoroutine = null;
                ShowAuthError(effectiveSetupMsg);
                SetFeedback(effectiveSetupMsg, RoatanVisualAssets.SunsetCoral);
                yield break;
            }

            LaunchOAuthBrowserAndPoll(provider, state, httpBase, authorizeUrl);
        }
    }

    private void LaunchOAuthBrowserAndPoll(string provider, string state, string httpBase, string authorizeUrl)
    {
        pendingOAuthProvider = provider;
        pendingOAuthState = state;
        oauthCodeSubmitted = false;

        if (oauthWaitingModal != null && oauthWaitingLabel != null)
        {
            oauthWaitingLabel.text = $"Choose your {provider} account in the browser window.\nYou will return to Roatan Domino automatically once signed in.";
            oauthWaitingModal.transform.SetAsLastSibling();
            oauthWaitingModal.SetActive(true);
        }

        RoatanGoogleSignIn.OpenAuthBrowser(authorizeUrl);
        oauthFlowCoroutine = StartCoroutine(PollOAuthSessionCoroutine(provider, state, httpBase));
    }

    private IEnumerator SubmitGoogleIdTokenCoroutine(string httpBase, string idToken)
    {
        string verifyUrl = $"{httpBase}/auth/oauth/google/id-token";
        SetFeedback("Verifying Google account...", RoatanVisualAssets.IslandGold);

        JObject payload = new JObject
        {
            ["idToken"] = idToken
        };
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(payload.ToString());

        using (UnityWebRequest req = new UnityWebRequest(verifyUrl, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 12;

            yield return req.SendWebRequest();
            oauthFlowCoroutine = null;

            string responseText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            if (req.result != UnityWebRequest.Result.Success)
            {
                string errMsg = "Google sign-in verification failed. Please try again.";
                try
                {
                    JObject errJson = JObject.Parse(responseText);
                    string serverErr = errJson["error"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(serverErr)) errMsg = serverErr;
                }
                catch
                {
                }
                ShowAuthError(errMsg);
                SetFeedback(errMsg, RoatanVisualAssets.SunsetCoral);
                yield break;
            }

            try
            {
                JObject json = JObject.Parse(responseText);
                bool ok = json["ok"] != null && json["ok"].Value<bool>();
                string oneTimeCode = json["oneTimeCode"]?.ToString() ?? string.Empty;
                if (ok && !string.IsNullOrWhiteSpace(oneTimeCode))
                {
                    RedeemOAuthOneTimeCode("Google", oneTimeCode);
                    yield break;
                }

                string err = json["error"]?.ToString() ?? "Google sign-in could not be completed.";
                ShowAuthError(err);
                SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            }
            catch
            {
                ShowAuthError("Unexpected response from server during Google sign-in.");
                SetFeedback("Unexpected response from server during Google sign-in.", RoatanVisualAssets.SunsetCoral);
            }
        }
    }

    private IEnumerator PollOAuthSessionCoroutine(string provider, string state, string httpBase)
    {
        string pollUrl = $"{httpBase}/auth/oauth/poll/{Uri.EscapeDataString(state)}";
        float elapsed = 0f;
        const float maxWaitSeconds = 180f;

        while (elapsed < maxWaitSeconds && !oauthCodeSubmitted && string.Equals(pendingOAuthState, state, StringComparison.Ordinal))
        {
            yield return new WaitForSeconds(1.5f);
            elapsed += 1.5f;

            if (oauthCodeSubmitted || !string.Equals(pendingOAuthState, state, StringComparison.Ordinal))
            {
                yield break;
            }

            using (UnityWebRequest req = UnityWebRequest.Get(pollUrl))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (req.responseCode == 404)
                    {
                        CancelPendingOAuthFlow();
                        ShowAuthError($"{provider} sign-in session expired. Please try again.");
                        yield break;
                    }
                    continue;
                }

                try
                {
                    JObject json = JObject.Parse(req.downloadHandler.text);
                    string status = json["status"]?.ToString() ?? "pending";
                    if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                    {
                        string oneTimeCode = json["oneTimeCode"]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(oneTimeCode))
                        {
                            RedeemOAuthOneTimeCode(provider, oneTimeCode);
                            yield break;
                        }
                    }
                    else if (string.Equals(status, "error", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                    {
                        string err = json["error"]?.ToString() ?? $"{provider} sign-in was cancelled.";
                        CancelPendingOAuthFlow();
                        ShowAuthError(err);
                        yield break;
                    }
                }
                catch
                {
                    // Ignore transient JSON parse error and continue polling
                }
            }
        }

        if (!oauthCodeSubmitted)
        {
            CancelPendingOAuthFlow();
            ShowAuthError($"{provider} sign-in timed out. Please try again.");
        }
    }

    private void OnDeepLinkActivated(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.StartsWith("roatandomino://auth", StringComparison.OrdinalIgnoreCase)) return;

        string code = ExtractQueryParam(url, "code");
        string provider = ExtractQueryParam(url, "provider");
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = pendingOAuthProvider ?? "Google";
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            RedeemOAuthOneTimeCode(provider, code);
        }
    }

    private static string ExtractQueryParam(string url, string key)
    {
        int qIdx = url.IndexOf('?');
        if (qIdx < 0 || qIdx >= url.Length - 1) return string.Empty;
        string query = url.Substring(qIdx + 1);
        string[] pairs = query.Split('&');
        foreach (string pair in pairs)
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            string k = Uri.UnescapeDataString(pair.Substring(0, eq));
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
        }
        return string.Empty;
    }

    private void RedeemOAuthOneTimeCode(string provider, string oneTimeCode)
    {
        if (oauthCodeSubmitted || string.IsNullOrWhiteSpace(oneTimeCode)) return;
        oauthCodeSubmitted = true;
        if (oauthFlowCoroutine != null)
        {
            StopCoroutine(oauthFlowCoroutine);
            oauthFlowCoroutine = null;
        }
        if (oauthWaitingModal != null) oauthWaitingModal.SetActive(false);

        if (!EnsureConnectedForRequest())
        {
            oauthCodeSubmitted = false;
            ShowAuthError("Disconnected from server. Please reconnect and try again.");
            return;
        }

        subscribedClient.AddToSendQueue(MessageFrameBuilder.SocialAuthFrame(new SocialAuthDto
        {
            Provider = provider,
            OneTimeCode = oneTimeCode
        }));
        SetFeedback($"Finishing {provider} sign-in...", RoatanVisualAssets.IslandGold);
    }

    public static bool ValidateRegistrationInputs(string email, string username, string password, string birthdate, out string error)
    {
        if (string.IsNullOrWhiteSpace(email) || !EmailValidationRegex.IsMatch(email))
        {
            error = "Please enter a valid email address.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(username) || !UsernameValidationRegex.IsMatch(username))
        {
            error = "Username must be 3–16 letters or numbers.";
            return false;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 8 || !HasCapitalRegex.IsMatch(password) || !HasNumberOrSymbolRegex.IsMatch(password))
        {
            error = "Password must be at least 8 characters and include at least one capital letter (A-Z) and one number or special character (!@#$%).";
            return false;
        }

        string[] dateFormats = { "MM/dd/yyyy", "M/d/yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
        if (string.IsNullOrWhiteSpace(birthdate) || !DateTime.TryParseExact(birthdate.Trim(), dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            error = "Please enter a valid date of birth (MM/DD/YYYY).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void SubmitLogout()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (subscribedClient != null)
        {
            subscribedClient.AddToSendQueue(MessageFrameBuilder.DisconnectionFrame());
        }
        isSignedIn = false;
        currentLobby = null;
        myTeamDetails = null;
        latestTeamsList = null;
        latestTeamLeaderboard = null;
        latestOpenChallenges = null;
        pendingTeamChallenge = null;
        pendingDirectChallenge = null;
        selectedActionPlayer = null;
        isSpectatingMatch = false;
        spectatingGameId = 0;
        liveSpectatorCount = 0;
        spectatingPlayers.Clear();
        teamChatMessages.Clear();
        dmThreadsByUserId.Clear();
        dmPartnerNamesByUserId.Clear();
        unreadTeamChatCount = 0;
        unreadDmCountByUserId.Clear();
        activeDmPartnerUserId = 0;
        activeDmPartnerUsername = string.Empty;
        editTeamDraftTeamId = -1;
        if (challengeTeamModal != null) challengeTeamModal.SetActive(false);
        if (incomingTeamChallengeModal != null) incomingTeamChallengeModal.SetActive(false);
        if (playerActionModal != null) playerActionModal.SetActive(false);
        if (directChallengeModal != null) directChallengeModal.SetActive(false);
        if (incomingDirectChallengeModal != null) incomingDirectChallengeModal.SetActive(false);
        ShowSubView(authView);
    }

    private void RequestChatHistories()
    {
        if (!EnsureConnectedForRequest()) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetChatHistoryFrame(new GetChatHistoryDto
        {
            ChannelType = SocialChatChannelType.Team,
            Limit = 60
        }));
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetChatHistoryFrame(new GetChatHistoryDto
        {
            ChannelType = SocialChatChannelType.Direct,
            OtherUserId = null,
            Limit = 100
        }));
    }

    private void RequestLobbiesList()
    {
        if (!EnsureConnectedForRequest()) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetAllLobbyFrame());
    }

    private void RequestPlayerRoster()
    {
        if (!EnsureConnectedForRequest()) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetPlayerRosterFrame());
    }

    private void RequestLeaderboard()
    {
        if (!EnsureConnectedForRequest()) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetLeaderboardFrame());
    }

    private void RequestTeamLeaderboard()
    {
        if (!EnsureConnectedForRequest()) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetTeamLeaderboardFrame());
    }

    private void RequestMyTeam()
    {
        if (!EnsureConnectedForRequest()) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetMyTeamFrame());
    }

    private void RequestTeamsList(string query = null)
    {
        if (!EnsureConnectedForRequest()) return;
        string q = query ?? teamSearchQuery;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetTeamsFrame(new GetTeamsRequestDto { SearchQuery = q }));
    }

    private void RequestOpenTeamChallenges()
    {
        if (!EnsureConnectedForRequest()) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.GetOpenTeamChallengesFrame());
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

        string tableName = string.IsNullOrWhiteSpace(createLobbyNameInput.text)
            ? "Roatan Table"
            : createLobbyNameInput.text.Trim();

        int effectiveMaxPlayers = createMatchFormat == MatchFormat.SIX_LOV
            ? 4
            : (createMatchFormat == MatchFormat.TWO_BEST && createLobbyMaxPlayers == 3 ? 2 : createLobbyMaxPlayers);

        subscribedClient.AddToSendQueue(MessageFrameBuilder.CreateLobbyFrame(new CreateLobbyDto
        {
            Name = tableName,
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
        if (string.IsNullOrWhiteSpace(joinCodeInput.text))
        {
            SetFeedback("Enter a private table code first.", RoatanVisualAssets.SunsetCoral);
            return;
        }

        subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyWithCodeFrame(new LobbyCodeDto
        {
            Code = joinCodeInput.text.Trim()
        }));
    }

    private void RequestSwitchSeat(int targetSeatIndex)
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest() || currentLobby == null) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.SwitchTableSeatFrame(new SwitchTableSeatDto
        {
            TargetSeatIndex = targetSeatIndex
        }));
    }

    private void RequestAddBot(int seatIndex)
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest() || currentLobby == null) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.AddTableBotFrame(new AddTableBotDto
        {
            SeatIndex = seatIndex,
            Difficulty = selectedHostBotDifficulty
        }));
    }

    private void RequestRemoveBot(int seatIndex)
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest() || currentLobby == null) return;
        subscribedClient.AddToSendQueue(MessageFrameBuilder.RemoveTableBotFrame(new RemoveTableBotDto
        {
            SeatIndex = seatIndex
        }));
    }

    private void RequestStartTableMatch()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest() || currentLobby == null) return;

        int humanCount = currentLobby.Players != null ? currentLobby.Players.Count(p => !p.IsBot) : 0;
        if (humanCount < 2)
        {
            SetFeedback($"Need at least 2 real players to start online ({humanCount}/2 humans seated). Use Offline Mode to play solo vs bots.", RoatanVisualAssets.SunsetCoral);
            return;
        }

        subscribedClient.AddToSendQueue(MessageFrameBuilder.StartTableMatchFrame());
        SetFeedback("Starting match...", RoatanVisualAssets.IslandGold);
    }

    private void RequestInvitePlayer(PlayerPresenceDto target)
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (!EnsureConnectedForRequest() || target == null) return;
        if (currentLobby == null)
        {
            SetFeedback("Join or create a table first to invite players.", RoatanVisualAssets.IslandGold);
            return;
        }
        subscribedClient.AddToSendQueue(MessageFrameBuilder.SendTableInviteFrame(new TableInviteDto
        {
            TargetUserId = target.UserId,
            TargetUsername = target.Username
        }));
        SetFeedback($"Invite sent to {target.Username}!", RoatanVisualAssets.PalmGreen);
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
        if (invitePlayersModal != null) invitePlayersModal.SetActive(false);
        ShowSubView(hubView);
        RequestLobbiesList();
        RequestPlayerRoster();
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
            CompleteSignInSuccess(loginUserInput != null ? loginUserInput.text : string.Empty);
        }
        else
        {
            string err = FormatFriendlyError(dto, "Incorrect username or password.");
            ShowAuthError(err);
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
        }
    }

    private void OnSocialAuthResponse(ResponseDto dto)
    {
        CancelPendingOAuthFlow();
        if (dto.OkResult)
        {
            CompleteSignInSuccess("Player");
        }
        else
        {
            string err = FormatFriendlyError(dto, "Social sign-in failed. Please try again.");
            ShowAuthError(err);
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
        }
    }

    private void CompleteSignInSuccess(string fallbackName)
    {
        isSignedIn = true;
        ClearAuthError();
        SetServerSettingsExpanded(false);
        if (oauthWaitingModal != null) oauthWaitingModal.SetActive(false);
        SetFeedback("Welcome back!", RoatanVisualAssets.PalmGreen);
        subscribedClient.AddToSendQueue(new List<MessageFrame>
        {
            MessageFrameBuilder.ReadyConnectedFrame(),
            MessageFrameBuilder.GetAllLobbyFrame(),
            MessageFrameBuilder.GetPlayerRosterFrame(),
            MessageFrameBuilder.GetLeaderboardFrame(),
            MessageFrameBuilder.GetTeamLeaderboardFrame(),
            MessageFrameBuilder.GetMyTeamFrame(),
            MessageFrameBuilder.GetTeamsFrame(new GetTeamsRequestDto()),
            MessageFrameBuilder.GetOpenTeamChallengesFrame(),
            MessageFrameBuilder.GetChatHistoryFrame(new GetChatHistoryDto { ChannelType = SocialChatChannelType.Team, Limit = 60 }),
            MessageFrameBuilder.GetChatHistoryFrame(new GetChatHistoryDto { ChannelType = SocialChatChannelType.Direct, Limit = 100 })
        });
        RefreshHubProfileHeader(fallbackName);
        ShowSubView(hubView);

        // Automatically download & cache social profile photo (Google/Facebook) if we have an AvatarUrl without cached bytes yet
        UserDto user = subscribedClient?.User;
        if (user != null && !string.IsNullOrWhiteSpace(user.AvatarUrl) && string.IsNullOrWhiteSpace(user.AvatarBase64))
        {
            StartCoroutine(AdoptSocialPhotoCoroutine(user.AvatarUrl, silent: true));
        }
    }

    private static int ResolveDisplayElo(UserDto user)
    {
        if (user == null) return RoatanRankTierHelper.DefaultElo;
        return user.Elo >= RoatanRankTierHelper.MinElo ? user.Elo : RoatanRankTierHelper.DefaultElo;
    }

    private static int ResolveDisplayElo(int rawElo)
    {
        return rawElo >= RoatanRankTierHelper.MinElo ? rawElo : RoatanRankTierHelper.DefaultElo;
    }

    private void RefreshHubProfileHeader(string fallbackName = "Online Player")
    {
        UserDto user = subscribedClient?.User;
        string loginName = user != null && !string.IsNullOrWhiteSpace(user.Login) ? user.Login : fallbackName;
        int elo = ResolveDisplayElo(user);
        string rankBadge = RoatanRankTierHelper.FormatRichBadge(elo);
        if (hubWelcomeLabel != null)
        {
            hubWelcomeLabel.text = $"{loginName}\n<size=19>{rankBadge}</size>";
        }
        ApplyCircularAvatar(
            hubAvatarImg,
            hubAvatarInitialsLabel,
            user != null ? $"u_{user.Id}" : "local",
            user?.AvatarBase64,
            loginName);
    }

    private void OnGetInfoProfileResponse(ResponseObjectDto<InfoProfileDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null) return;
        RefreshHubProfileHeader();
        if (profileModal != null && profileModal.activeSelf)
        {
            RefreshProfileModalUI(dto.Object.Statistic);
        }
    }

    private void OnUpdateProfileAvatarResponse(ResponseObjectDto<UserDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null)
        {
            string err = FormatFriendlyError(dto, "Could not update profile photo.");
            if (profileModalStatusLabel != null) profileModalStatusLabel.text = err;
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            return;
        }

        RefreshHubProfileHeader();
        if (profileModal != null && profileModal.activeSelf)
        {
            RefreshProfileModalUI(null);
            if (profileModalStatusLabel != null)
            {
                profileModalStatusLabel.text = "Profile photo saved!";
                profileModalStatusLabel.color = RoatanVisualAssets.PalmGreen;
            }
        }
        if (lobbyView != null && lobbyView.activeSelf && currentLobby != null)
        {
            RefreshLobbyView();
        }
        SetFeedback("Profile photo updated!", RoatanVisualAssets.PalmGreen);
    }

    private void OnDeleteAccountResponse(ResponseDto dto)
    {
        if (dto == null || !dto.OkResult)
        {
            string err = FormatFriendlyError(dto, "Could not delete account. Please try again.");
            if (deleteConfirmErrorLabel != null) deleteConfirmErrorLabel.text = err;
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            return;
        }

        // Clear all local credentials, cached avatars, and active match sessions
        PlayerPrefs.DeleteKey(PrefSocialDeviceIdKey);
        PlayerPrefs.Save();
        RoatanMatchSessionStore.ClearSession();
        RoatanAndroidPhotoPicker.ClearCache();
        pendingSavedSession = null;
        currentLobby = null;
        localReady = false;
        isSignedIn = false;

        if (loginUserInput != null) loginUserInput.text = string.Empty;
        if (loginPassInput != null) loginPassInput.text = string.Empty;
        if (regEmailInput != null) regEmailInput.text = string.Empty;
        if (regUserInput != null) regUserInput.text = string.Empty;
        if (regPassInput != null) regPassInput.text = string.Empty;
        if (profileModal != null) profileModal.SetActive(false);
        if (deleteAccountModal != null) deleteAccountModal.SetActive(false);

        ShowSubView(authView);
        SelectAuthTab(isSignIn: true);
        SetFeedback("Your account has been permanently deleted.", RoatanVisualAssets.IslandGold);
    }

    private IEnumerator AdoptSocialPhotoCoroutine(string avatarUrl, bool silent)
    {
        if (!silent && profileModalStatusLabel != null)
        {
            profileModalStatusLabel.text = "Downloading social photo...";
            profileModalStatusLabel.color = RoatanVisualAssets.IslandGold;
        }

        yield return RoatanAndroidPhotoPicker.DownloadAndCompressUrlCoroutine(
            avatarUrl,
            base64 =>
            {
                if (EnsureConnectedForRequest())
                {
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.UpdateProfileAvatarFrame(new UpdateProfileAvatarDto
                    {
                        AvatarBase64 = base64,
                        AvatarUrl = avatarUrl
                    }));
                }
            },
            err =>
            {
                if (!silent)
                {
                    if (profileModalStatusLabel != null)
                    {
                        profileModalStatusLabel.text = err;
                        profileModalStatusLabel.color = RoatanVisualAssets.SunsetCoral;
                    }
                    SetFeedback(err, RoatanVisualAssets.SunsetCoral);
                }
            });
    }

    private void OpenProfileModal()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (subscribedClient?.User == null) return;
        RefreshProfileModalUI(null);
        if (profileModalStatusLabel != null) profileModalStatusLabel.text = string.Empty;
        if (profileModal != null)
        {
            profileModal.transform.SetAsLastSibling();
            profileModal.SetActive(true);
        }
        if (EnsureConnectedForRequest())
        {
            subscribedClient.AddToSendQueue(MessageFrameBuilder.GetInfoProfileFrame());
        }
    }

    private void RefreshProfileModalUI(StatisticDto stats)
    {
        UserDto user = subscribedClient?.User;
        if (user == null) return;

        ApplyCircularAvatar(profileModalAvatarImg, profileModalAvatarInitials, $"u_{user.Id}", user.AvatarBase64, user.Login);
        if (profileModalNameLabel != null) profileModalNameLabel.text = user.Login;
        if (profileModalEmailLabel != null) profileModalEmailLabel.text = string.IsNullOrWhiteSpace(user.Email) ? "No email on file" : user.Email;

        string provider = string.IsNullOrWhiteSpace(user.AuthProvider) ? "Email" : user.AuthProvider;
        if (profileModalProviderBadge != null)
        {
            profileModalProviderBadge.text = $"Signed in with {provider}";
        }

        if (profileModalStatsLabel != null)
        {
            int elo = ResolveDisplayElo(user);
            string rankBadge = RoatanRankTierHelper.FormatRichBadge(elo);
            int wins = user.Wins > 0 || user.Losses > 0 ? user.Wins : (stats != null ? stats.NumberOfGameWon : 0);
            int losses = user.Wins > 0 || user.Losses > 0 ? user.Losses : (stats != null ? stats.NumberOfGameLost : 0);
            string accStr = user.TotalMoves > 0 ? $"{user.AccuracyPercent:0.#}%" : "—";
            string speedStr = user.AverageTurnSeconds > 0.01 ? $"{user.AverageTurnSeconds:0.0}s" : "—";
            profileModalStatsLabel.text = $"{rankBadge} Elo\n<size=19>W/L: {wins}-{losses}  •  Acc: {accStr}  •  Assists: {user.WinAssists}  •  Avg: {speedStr}</size>";
        }

        if (profileAdoptSocialBtn != null)
        {
            bool hasSocialPhoto = !string.IsNullOrWhiteSpace(user.AvatarUrl);
            profileAdoptSocialBtn.gameObject.SetActive(hasSocialPhoto);
        }
    }

    private void OpenDeleteAccountModal()
    {
        RoatanSoundFX.Instance?.PlayTap();
        deleteConfirmSecondTapArmed = false;
        if (deleteConfirmInput != null) deleteConfirmInput.text = string.Empty;
        if (deleteConfirmErrorLabel != null) deleteConfirmErrorLabel.text = string.Empty;
        if (deleteConfirmBtnLabel != null) deleteConfirmBtnLabel.text = "Confirm Delete";
        if (deleteAccountModal != null)
        {
            deleteAccountModal.transform.SetAsLastSibling();
            deleteAccountModal.SetActive(true);
        }
    }

    private void SubmitDeleteAccountConfirmed()
    {
        RoatanSoundFX.Instance?.PlayTap();
        string typed = deleteConfirmInput != null ? deleteConfirmInput.text.Trim() : string.Empty;
        bool typedDelete = string.Equals(typed, "DELETE", StringComparison.OrdinalIgnoreCase);

        if (!typedDelete && !deleteConfirmSecondTapArmed)
        {
            deleteConfirmSecondTapArmed = true;
            if (deleteConfirmBtnLabel != null) deleteConfirmBtnLabel.text = "Tap Again to Permanently Delete";
            if (deleteConfirmErrorLabel != null)
            {
                deleteConfirmErrorLabel.text = "Type DELETE or tap once more to permanently delete your account.";
                deleteConfirmErrorLabel.color = RoatanVisualAssets.IslandGold;
            }
            return;
        }

        if (!EnsureConnectedForRequest())
        {
            if (deleteConfirmErrorLabel != null)
            {
                deleteConfirmErrorLabel.text = "Not connected to server.";
                deleteConfirmErrorLabel.color = RoatanVisualAssets.SunsetCoral;
            }
            return;
        }

        subscribedClient.AddToSendQueue(MessageFrameBuilder.DeleteAccountFrame());
        if (deleteConfirmErrorLabel != null)
        {
            deleteConfirmErrorLabel.text = "Deleting account...";
            deleteConfirmErrorLabel.color = RoatanVisualAssets.IslandGold;
        }
    }

    private static void ApplyCircularAvatar(Image avatarImg, TMP_Text initialsLbl, string cacheKey, string base64, string displayName)
    {
        if (avatarImg == null) return;
        Color bg = RoatanAndroidPhotoPicker.GetFallbackAvatarColor(displayName);
        Sprite sprite = RoatanAndroidPhotoPicker.GetOrCreateCircularAvatarSprite(cacheKey, base64, bg, out bool hasPhoto);
        avatarImg.sprite = sprite;
        avatarImg.color = Color.white;
        if (initialsLbl != null)
        {
            initialsLbl.gameObject.SetActive(!hasPhoto);
            if (!hasPhoto)
            {
                initialsLbl.text = RoatanAndroidPhotoPicker.GetInitials(displayName);
            }
        }
    }

    private void OnRegisterResponse(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            ClearAuthError();
            SetFeedback("Account created! You can now Sign In.", RoatanVisualAssets.PalmGreen);
            SelectAuthTab(isSignIn: true);
            loginUserInput.text = regUserInput.text;
        }
        else
        {
            string err = FormatFriendlyError(dto, "Could not create account. Please check your details.");
            ShowAuthError(err);
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
        }
    }

    private void OnLogoutResponse(ResponseDto dto)
    {
        isSignedIn = false;
        currentLobby = null;
        myTeamDetails = null;
        latestTeamsList = null;
        editTeamDraftTeamId = -1;
        ShowSubView(authView);
    }

    private void OnGetAllLobbiesResponse(ResponseObjectDto<LobbiesInfoDto> dto)
    {
        foreach (GameObject row in spawnedLobbyRows) Destroy(row);
        spawnedLobbyRows.Clear();

        if (!dto.OkResult || dto.Object == null || dto.Object.Lobbies == null || dto.Object.Lobbies.Count == 0)
        {
            GameObject emptyRow = CreateCard(lobbiesListContent, "EmptyRow", new Color(0.08f, 0.21f, 0.27f, 0.7f), Vector2.zero, new Vector2(860f, 88f));
            emptyRow.AddComponent<LayoutElement>().preferredHeight = 88f;
            CreateText(emptyRow.transform, "EmptyLbl", "No public tables open yet — create one above!", 24, FontStyles.Italic, new Color(0.82f, 0.92f, 0.95f), Vector2.zero, new Vector2(820f, 64f));
            spawnedLobbyRows.Add(emptyRow);
            return;
        }

        foreach (LobbyInfoDto table in dto.Object.Lobbies)
        {
            LobbyInfoDto captured = table;
            bool isLive = table.IsInProgress;
            bool isFull = table.NumberOfPlayer >= table.MaxNumberOfPlayer;
            Color cardColor = isLive
                ? new Color(0.11f, 0.21f, 0.30f, 0.97f)
                : (table.IsHouseTable
                    ? new Color(0.09f, 0.27f, 0.30f, 0.97f)
                    : new Color(0.08f, 0.23f, 0.29f, 0.95f));

            GameObject row = CreateCard(lobbiesListContent, $"Table_{table.Id}_{(isLive ? "Live" : "Open")}", cardColor, Vector2.zero, new Vector2(860f, 146f));
            row.AddComponent<LayoutElement>().preferredHeight = 146f;

            string liveTag = isLive
                ? $"  <color=#FF6B6B>[LIVE • {table.SpectatorCount} Watching]</color>"
                : (table.IsHouseTable ? "  <color=#F7C948>[HOUSE]</color>" : string.Empty);
            string hostName = string.IsNullOrWhiteSpace(table.Creator) ? "House" : table.Creator;
            string modeBadge = FormatLobbyRulesBadge(table.MatchFormat, table.TableStyle, table.OneOnePlayoff);
            string seatedSummary = (table.SeatedPlayers != null && table.SeatedPlayers.Count > 0)
                ? string.Join(", ", table.SeatedPlayers)
                : "Open — tap Join to sit!";

            CreateText(row.transform, "TTitle", $"{table.Name}{liveTag}  ({table.NumberOfPlayer}/{table.MaxNumberOfPlayer})", 23, FontStyles.Bold, Color.white, new Vector2(-125f, 42f), new Vector2(580f, 36f)).alignment = TextAlignmentOptions.MidlineLeft;
            CreateText(row.transform, "TMeta", isLive ? $"In Progress  •  {modeBadge}" : $"Host: {hostName}  •  {modeBadge}", 19, FontStyles.Normal, RoatanVisualAssets.IslandGold, new Vector2(-125f, 4f), new Vector2(580f, 32f)).alignment = TextAlignmentOptions.MidlineLeft;
            CreateText(row.transform, "TSeated", $"Players: {seatedSummary}", 18, FontStyles.Italic, new Color(0.80f, 0.93f, 0.96f), new Vector2(-125f, -36f), new Vector2(580f, 34f)).alignment = TextAlignmentOptions.MidlineLeft;

            if (isLive)
            {
                CreatePillButton(row.transform, "BtnWatchLive", "Watch Live", RoatanVisualAssets.BrightLagoon, new Vector2(315f, 0f), new Vector2(190f, 74f), 22, () =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    if (!EnsureConnectedForRequest()) return;
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinAsSpectatorFrame(new JoinSpectatorDto { GameId = captured.Id }));
                    SetFeedback($"Joining {captured.Name} as spectator...", RoatanVisualAssets.IslandGold);
                });
            }
            else
            {
                string joinLabel = isFull ? $"FULL ({table.NumberOfPlayer}/{table.MaxNumberOfPlayer})" : "Join";
                Color joinColor = isFull ? new Color(0.38f, 0.24f, 0.25f, 0.95f) : RoatanVisualAssets.PalmGreen;
                Button joinBtn = CreatePillButton(row.transform, "BtnJoinTable", joinLabel, joinColor, new Vector2(315f, 0f), new Vector2(190f, 74f), isFull ? 21 : 26, () =>
                {
                    if (captured.NumberOfPlayer >= captured.MaxNumberOfPlayer)
                    {
                        SetFeedback($"This table is full ({captured.NumberOfPlayer}/{captured.MaxNumberOfPlayer} players).", RoatanVisualAssets.SunsetCoral);
                        return;
                    }
                    RoatanSoundFX.Instance?.PlayTap();
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyFrame(new JoinLobbyDto { LobbyIdToJoin = captured.Id }));
                });
                joinBtn.interactable = !isFull;
            }

            spawnedLobbyRows.Add(row);
        }
    }

    private void OnLobbyEnteredResponse(ResponseObjectDto<LobbyDto> dto)
    {
        if (!dto.OkResult || dto.Object == null)
        {
            string err = FormatFriendlyError(dto, "Unable to enter table.");
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            return;
        }

        currentLobby = dto.Object;
        localReady = false;
        InLobby.SetOfflineLobby(currentLobby);
        ShowSubView(lobbyView);
        RefreshLobbyView();
        RequestPlayerRoster();
    }

    private void OnTableActionLobbyUpdatedResponse(ResponseObjectDto<LobbyDto> dto)
    {
        if (!dto.OkResult || dto.Object == null)
        {
            string err = FormatFriendlyError(dto, "Table action failed.");
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            return;
        }

        currentLobby = dto.Object;
        InLobby.SetOfflineLobby(currentLobby);
        RefreshLobbyView();
    }

    private void OnTableStateUpdated(LobbyDto updatedTable)
    {
        if (updatedTable == null) return;
        currentLobby = updatedTable;
        InLobby.SetOfflineLobby(currentLobby);
        if (lobbyView != null && lobbyView.activeSelf)
        {
            RefreshLobbyView();
        }
    }

    private void OnStartTableMatchResponse(ResponseDto dto)
    {
        if (!dto.OkResult)
        {
            string err = FormatFriendlyError(dto, "Cannot start match yet.");
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
        }
    }

    private void OnSendTableInviteResponse(ResponseDto dto)
    {
        if (!dto.OkResult)
        {
            string err = FormatFriendlyError(dto, "Could not send table invite.");
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
        }
    }

    private void OnGetPlayerRosterResponse(ResponseObjectDto<PlayerRosterDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null) return;
        ApplyRosterUpdate(dto.Object);
    }

    private void OnPlayerRosterUpdated(PlayerRosterDto roster)
    {
        if (roster == null) return;
        ApplyRosterUpdate(roster);
    }

    private void ApplyRosterUpdate(PlayerRosterDto roster)
    {
        latestRoster.Clear();
        if (roster.Players != null)
        {
            latestRoster.AddRange(roster.Players);
        }

        int onlineCount = latestRoster.Count(p => p.IsOnline);
        if (hubPlayersTabLabel != null)
        {
            hubPlayersTabLabel.text = $"Players ({onlineCount})";
        }

        RefreshPlayersRosterUI();
        if (invitePlayersModal != null && invitePlayersModal.activeSelf)
        {
            RefreshInvitePlayersModalUI();
        }
    }

    private void OnTableInviteReceived(TableInviteDto invite)
    {
        if (invite == null) return;
        pendingIncomingInvite = invite;
        if (incomingInviteModal != null && incomingInviteBodyLabel != null)
        {
            incomingInviteBodyLabel.text = $"<b>{invite.FromUsername}</b> invited you to join:\n\n<color=#F7C948><b>{invite.TableName}</b></color>\nCode: {invite.TableCode}";
            incomingInviteModal.transform.SetAsLastSibling();
            incomingInviteModal.SetActive(true);
            RoatanSoundFX.Instance?.PlayWin();
        }
    }

    private void OnJoinCodeResponse(ResponseDto dto)
    {
        if (!dto.OkResult)
        {
            string err = FormatFriendlyError(dto, "Invalid table code.");
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
            lobbyTimerLabel.text = dto.HasStarted ? $"Match starting in {dto.Duration}s..." : "Tap an open seat to switch, add bots, or Ready up!";
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
        latestMatchEloBreakdown = null;
        if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);
        if (onlineEndOverlay != null) onlineEndOverlay.SetActive(false);
        if (onlineEndEloBreakdownCard != null) onlineEndEloBreakdownCard.SetActive(false);
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
        isSpectatingMatch = false;
        spectatingGameId = 0;
        liveSpectatorCount = 0;
        spectatingPlayers.Clear();
        RefreshOnlineSpectatorHUD();
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
            int port = Networking.Instance != null ? Networking.Instance.CurrentPort : 5000;
            string serverUrl = Networking.Instance != null ? Networking.Instance.CurrentServerUrl : GetSavedServerUrl();
            RoatanMatchSessionStore.SaveSession(matchId, lobbyId, subscribedClient.User.Id, dto.ReconnectToken, host, port, 1800, serverUrl);
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

            string playerName = (isSpectatingMatch ? spectatingPlayers : currentLobby?.Players)?
                .FirstOrDefault(p => p.Id == dto.IdUserPlaying)?.Name ?? $"Player {dto.IdUserPlaying}";
            string botSuffix = dto.IsBotControlled == true ? " [Bot Auto-Playing]" : "";
            onlineMatchStatus.text = isSpectatingMatch
                ? $"SPECTATING  •  {playerName}{botSuffix}'s turn..."
                : $"{playerName}{botSuffix}'s turn...";
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
        List<PlayerDto> roster = isSpectatingMatch ? spectatingPlayers : currentLobby?.Players;
        if (dto?.NumberOfDomino == null || roster == null) return;
        List<string> parts = new List<string>();
        foreach (PlayerDto p in roster)
        {
            if (dto.NumberOfDomino.TryGetValue(p.Id, out int count))
            {
                parts.Add($"{p.Name}: {count} tiles");
            }
        }
        onlineMatchHeader.text = isSpectatingMatch
            ? $"[LIVE] {spectatingMatchTitle}  •  {string.Join("  |  ", parts)}"
            : string.Join("  •  ", parts);
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
        bool isKeyWin = dto != null && dto.IsKeyWin == true;
        int ptsAwarded = dto != null && dto.PointsAwarded.HasValue ? dto.PointsAwarded.Value : (isKeyWin ? 2 : 1);
        bool isSixLov = currentLobby != null && currentLobby.MatchFormat == MatchFormat.SIX_LOV;

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
        else if (isKeyWin)
        {
            RoatanSoundFX.Instance?.PlayKeySting();
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
        onlineMatchStatus.text = breakTriggered
            ? "GAME BRUK! (BREAK)"
            : (isKeyWin
                ? (matchOver && isSixLov ? "SIX-LOV BY KEY! (CAPICÚ!)" : $"KEY! / CAPICÚ! (+{ptsAwarded} WINS) — {winnerName}")
                : winnerName);
        onlineDrawBtn.gameObject.SetActive(false);

        if (onlineEndOverlay != null)
        {
            if (breakTriggered)
            {
                onlineEndTitleLabel.text = "GAME BRUK! (BREAK)";
                onlineEndBodyLabel.text = $"{winnerName}\nScores reset to 0 — [6|6] poses next on Rematch.";
            }
            else if (isKeyWin && matchOver && isSixLov)
            {
                onlineEndTitleLabel.text = "SIX-LOV BY KEY! (CAPICÚ!)";
                onlineEndBodyLabel.text = $"{winnerName}\nWon by KEY / CAPICÚ (+{ptsAwarded}) — 6–0 Clean Sweep!";
            }
            else if (isKeyWin)
            {
                onlineEndTitleLabel.text = "KEY! / CAPICÚ! (+2 WINS)";
                onlineEndBodyLabel.text = $"{winnerName}\nWon by KEY / CAPICÚ (+{ptsAwarded})";
            }
            else
            {
                onlineEndTitleLabel.text = matchOver ? "MATCH COMPLETE" : "HAND COMPLETE";
                onlineEndBodyLabel.text = winnerName;
            }

            bool showRematchLayout = breakTriggered || matchOver;
            onlineEndPrimaryBtn.onClick.RemoveAllListeners();
            if (isSpectatingMatch)
            {
                onlineEndPrimaryBtnLabel.text = "Leave Spectating";
                onlineEndPrimaryBtn.onClick.AddListener(() =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    onlineEndOverlay.SetActive(false);
                    LeaveSpectatingMatch();
                });
                onlineEndNewMatchBtn.gameObject.SetActive(false);
            }
            else if (showRematchLayout)
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

        if (dto?.EloBreakdown != null)
        {
            OnMatchEloBreakdown(dto.EloBreakdown);
        }
        else if (latestMatchEloBreakdown != null)
        {
            RefreshOnlineEndEloBreakdownUI(latestMatchEloBreakdown);
        }
    }

    private void OnMatchEloBreakdown(MatchEloBreakdownDto breakdown)
    {
        if (breakdown == null) return;
        latestMatchEloBreakdown = breakdown;

        if (subscribedClient?.User != null && breakdown.Players != null)
        {
            PlayerEloBreakdownDto local = breakdown.Players.FirstOrDefault(p => p.PlayerId == subscribedClient.User.Id);
            if (local != null && local.IsRankedApplied)
            {
                subscribedClient.User.Elo = local.NewElo;
                RefreshHubProfileHeader();
            }
        }

        RefreshOnlineEndEloBreakdownUI(breakdown);
        if (EnsureConnectedForRequest())
        {
            RequestLeaderboard();
            RequestTeamLeaderboard();
            RequestMyTeam();
        }
    }

    private void RefreshOnlineEndEloBreakdownUI(MatchEloBreakdownDto breakdown)
    {
        if (onlineEndEloBreakdownCard == null || onlineEndEloBreakdownTitle == null || onlineEndEloBreakdownBody == null)
        {
            return;
        }

        long localUserId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;
        PlayerEloBreakdownDto local = breakdown?.Players?.FirstOrDefault(p => p.PlayerId == localUserId)
            ?? breakdown?.Players?.FirstOrDefault();

        if (local == null)
        {
            onlineEndEloBreakdownCard.SetActive(false);
            return;
        }

        string tierHex = string.IsNullOrWhiteSpace(local.TierColorHex)
            ? local.NewTier.GetBadgeColorHex()
            : local.TierColorHex;
        string tierName = string.IsNullOrWhiteSpace(local.TierName)
            ? local.NewTier.GetDisplayName()
            : local.TierName;

        string teamLine = FormatTeamBreakdownLine(breakdown?.TeamBreakdown);

        if (!local.IsRankedApplied)
        {
            onlineEndEloBreakdownTitle.text = $"<color={tierHex}>[{tierName}]</color>  •  {local.NewElo} Elo (Unranked)";
            onlineEndEloBreakdownBody.text = string.IsNullOrEmpty(teamLine)
                ? "Online matches with 2+ real players update Elo ratings."
                : $"Online matches with 2+ real players update Elo ratings.\n{teamLine}";
            onlineEndEloBreakdownCard.SetActive(true);
            return;
        }

        string deltaColor = local.Delta > 0 ? "#4BE38D" : (local.Delta < 0 ? "#F26D5B" : "#B8D8E0");
        string signedDelta = local.Delta >= 0 ? $"+{local.Delta}" : local.Delta.ToString();
        string promoTag = local.NewTier > local.PreviousTier ? "  <color=#F7C948>▲ RANK UP!</color>" : string.Empty;

        onlineEndEloBreakdownTitle.text = $"<color={tierHex}>[{tierName}]</color>  •  {local.PreviousElo} → <b>{local.NewElo}</b>  (<color={deltaColor}><b>{signedDelta}</b></color>){promoTag}";

        string speedStr = local.AverageTurnSeconds > 0.01 ? $"{local.AverageTurnSeconds:0.0}s" : "—";
        string bonusPart = local.SpecialWinBonus > 0 ? $"  |  Bonus +{local.SpecialWinBonus}" : string.Empty;
        string perfSign = local.PerformanceModifier >= 0 ? $"+{local.PerformanceModifier}" : local.PerformanceModifier.ToString();
        string baseSign = local.BaseEloDelta >= 0 ? $"+{local.BaseEloDelta}" : local.BaseEloDelta.ToString();

        string bodyText =
            $"Result: <b>{local.ResultLabel}</b>  •  Assists: <b>{local.WinAssists}</b>  •  Accuracy: <b>{local.AccuracyPercent:0.#}%</b>  •  Avg Speed: <b>{speedStr}</b>\n" +
            $"<size=17><color=#9FC5D0>Base {baseSign}{bonusPart}  |  Performance {perfSign}</color></size>";
        if (!string.IsNullOrEmpty(teamLine))
        {
            bodyText += $"\n{teamLine}";
        }

        onlineEndEloBreakdownBody.text = bodyText;
        onlineEndEloBreakdownCard.SetActive(true);
    }

    private static string FormatTeamBreakdownLine(TeamEloBreakdownDto teamBreakdown)
    {
        if (teamBreakdown?.TeamA == null || teamBreakdown.TeamB == null) return string.Empty;
        TeamEloChangeDto a = teamBreakdown.TeamA;
        TeamEloChangeDto b = teamBreakdown.TeamB;
        string aDelta = a.Delta >= 0 ? $"+{a.Delta}" : a.Delta.ToString();
        string bDelta = b.Delta >= 0 ? $"+{b.Delta}" : b.Delta.ToString();
        string aColor = a.Delta >= 0 ? "#4BE38D" : "#F26D5B";
        string bColor = b.Delta >= 0 ? "#4BE38D" : "#F26D5B";
        return $"<size=17><color=#F7C948>Crew Elo:</color> [{a.TeamTag}] {a.NewElo} (<color={aColor}>{aDelta}</color>)  vs  [{b.TeamTag}] {b.NewElo} (<color={bColor}>{bDelta}</color>)</size>";
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

        // Compact Server Status Pill (with [Edit] toggle for collapsed Server URL & Presets)
        GameObject badgeCard = CreateCard(onlineRoot.transform, "ConnBadge", new Color(0.08f, 0.21f, 0.27f, 0.94f), Vector2.zero, new Vector2(940f, 64f));
        RectTransform badgeRect = badgeCard.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 1f);
        badgeRect.anchorMax = new Vector2(0.5f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, -124f);
        connectionBadgeBg = badgeCard.GetComponent<Image>();
        connectionBadgeText = CreateText(badgeCard.transform, "ConnText", "", 22, FontStyles.Bold, Color.white, new Vector2(-78f, 0f), new Vector2(730f, 52f));
        connectionBadgeText.alignment = TextAlignmentOptions.MidlineLeft;
        Button editServerBtn = CreatePillButton(badgeCard.transform, "BtnEditServer", "Edit", new Color(0.10f, 0.24f, 0.30f, 0.96f), new Vector2(382f, 0f), new Vector2(138f, 46f), 21, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            SetServerSettingsExpanded(!serverSettingsExpanded);
        });
        editServerPillBtnLabel = editServerBtn.GetComponentInChildren<TMP_Text>();

        // Feedback Toast at bottom
        GameObject toastCard = CreateCard(onlineRoot.transform, "OnlineToast", new Color(0.05f, 0.15f, 0.20f, 0.92f), Vector2.zero, new Vector2(940f, 72f));
        RectTransform toastRect = toastCard.GetComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0.5f, 0f);
        toastRect.anchorMax = new Vector2(0.5f, 0f);
        toastRect.pivot = new Vector2(0.5f, 0f);
        toastRect.anchoredPosition = new Vector2(0f, 22f);
        feedbackToast = CreateText(toastCard.transform, "ToastText", "Sign in to join Roatan House Table or create your own table.", 22, FontStyles.Normal, new Color(0.88f, 0.96f, 0.98f), Vector2.zero, new Vector2(900f, 60f));

        BuildAuthView(onlineRoot.transform);
        BuildHubView(onlineRoot.transform);
        BuildLobbyView(onlineRoot.transform);
        BuildOnlineGameView(onlineRoot.transform);
        BuildCollapsibleServerDrawer(onlineRoot.transform);
    }

    private void BuildCollapsibleServerDrawer(Transform parent)
    {
        serverSettingsDrawer = CreateCard(parent, "ServerCardDrawer", new Color(0.05f, 0.15f, 0.21f, 0.99f), Vector2.zero, new Vector2(940f, 326f));
        RectTransform dRect = serverSettingsDrawer.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.5f, 1f);
        dRect.anchorMax = new Vector2(0.5f, 1f);
        dRect.pivot = new Vector2(0.5f, 1f);
        dRect.anchoredPosition = new Vector2(0f, -196f);

        CreateText(serverSettingsDrawer.transform, "ServerHdr", "SERVER URL & PRESETS", 22, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 130f), new Vector2(880f, 34f));

        RoatanServerPreset[] presets = { RoatanServerPreset.Production, RoatanServerPreset.CustomTunnel, RoatanServerPreset.Localhost };
        string[] presetLabels = { "Production", "Custom / Tunnel", "Localhost" };
        float[] presetXs = { -292f, 0f, 292f };
        for (int i = 0; i < presets.Length; i++)
        {
            RoatanServerPreset capturedPreset = presets[i];
            Button pBtn = CreatePillButton(serverSettingsDrawer.transform, $"Preset_{capturedPreset}", presetLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(presetXs[i], 70f), new Vector2(276f, 52f), 22, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                SelectServerPresetInAuthView(capturedPreset);
            });
            serverPresetBtns[capturedPreset] = pBtn.GetComponent<Image>();
        }

        string savedUrl = GetSavedServerUrl();
        activeServerPreset = GetSavedPreset();

        serverUrlInput = CreateInputField(serverSettingsDrawer.transform, "ServerUrlInput", "https://your-tunnel.trycloudflare.com", savedUrl, new Vector2(0f, 2f), new Vector2(876f, 62f));
        serverUrlInput.onValueChanged.AddListener(OnAuthServerUrlChanged);

        serverEndpointPreviewLabel = CreateText(serverSettingsDrawer.transform, "EndpointPreview", "", 18, FontStyles.Italic, new Color(0.75f, 0.92f, 0.96f), new Vector2(0f, -48f), new Vector2(870f, 28f));

        Button connBtn = CreatePillButton(serverSettingsDrawer.transform, "BtnConnect", "Connect / Retry", RoatanVisualAssets.BrightLagoon, new Vector2(-120f, -108f), new Vector2(440f, 60f), 24, AttemptServerConnect);
        connectBtnLabel = connBtn.GetComponentInChildren<TMP_Text>();

        CreatePillButton(serverSettingsDrawer.transform, "BtnCloseServerDrawer", "Done", new Color(0.20f, 0.24f, 0.28f, 1f), new Vector2(250f, -108f), new Vector2(200f, 60f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            SetServerSettingsExpanded(false);
        });

        SyncServerEndpointFieldsFromPrefs();
        SetServerSettingsExpanded(false);
    }

    private void SetServerSettingsExpanded(bool expanded)
    {
        serverSettingsExpanded = expanded;
        if (serverSettingsDrawer != null)
        {
            serverSettingsDrawer.SetActive(expanded);
            if (expanded) serverSettingsDrawer.transform.SetAsLastSibling();
        }
        if (editServerPillBtnLabel != null)
        {
            editServerPillBtnLabel.text = expanded ? "Close" : "Edit";
        }
    }

    private void BuildAuthView(Transform parent)
    {
        authView = CreateStretchPanel(parent, "AuthView", Color.clear);

        // Front-and-center Sign In / Create Account Card (Server panel collapsed by default!)
        GameObject formCard = CreateCard(authView.transform, "AuthFormCard", new Color(0.06f, 0.18f, 0.24f, 0.96f), new Vector2(0f, -25f), new Vector2(940f, 1260f));

        Button tabIn = CreatePillButton(formCard.transform, "TabSignIn", "Sign In", RoatanVisualAssets.BrightLagoon, new Vector2(-215f, 566f), new Vector2(410f, 70f), 28, () => SelectAuthTab(true));
        signInTabImg = tabIn.GetComponent<Image>();
        signInTabLabel = tabIn.GetComponentInChildren<TMP_Text>();

        Button tabReg = CreatePillButton(formCard.transform, "TabRegister", "Create Account", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(215f, 566f), new Vector2(410f, 70f), 28, () => SelectAuthTab(false));
        registerTabImg = tabReg.GetComponent<Image>();
        registerTabLabel = tabReg.GetComponentInChildren<TMP_Text>();

        // Dedicated Inline Error Banner (replaces cryptic server errors)
        authErrorBannerObj = CreateCard(formCard.transform, "AuthErrorBanner", new Color(0.62f, 0.16f, 0.16f, 0.96f), new Vector2(0f, 476f), new Vector2(840f, 84f));
        authErrorBannerText = CreateText(authErrorBannerObj.transform, "ErrText", "", 20, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(800f, 74f));
        authErrorBannerObj.SetActive(false);

        // Sign In Panel
        signInPanel = CreateStretchPanel(formCard.transform, "SignInPanel", Color.clear);
        CreateFieldTitle(signInPanel.transform, "SignInUserTitle", "Username or Email", new Vector2(0f, 396f));
        loginUserInput = CreateInputField(signInPanel.transform, "LoginUser", "Enter username or email", "", new Vector2(0f, 336f), new Vector2(820f, 78f));
        loginUserInput.onValueChanged.AddListener(_ => ClearAuthError());

        CreateFieldTitle(signInPanel.transform, "SignInPassTitle", "Password", new Vector2(0f, 256f));
        loginPassInput = CreateInputField(signInPanel.transform, "LoginPass", "Enter password", "", new Vector2(-74f, 196f), new Vector2(672f, 78f), isPassword: true);
        loginPassInput.onValueChanged.AddListener(_ => ClearAuthError());
        Button loginShowBtn = CreatePillButton(signInPanel.transform, "BtnLoginShowPass", "Show", new Color(0.12f, 0.28f, 0.35f, 1f), new Vector2(342f, 196f), new Vector2(136f, 78f), 22, ToggleLoginPasswordVisibility);
        loginShowPassLabel = loginShowBtn.GetComponentInChildren<TMP_Text>();

        Button loginSubmit = CreatePillButton(signInPanel.transform, "BtnSubmitLogin", "Sign In", RoatanVisualAssets.PalmGreen, new Vector2(0f, 66f), new Vector2(680f, 92f), 34, SubmitLogin);
        signInSubmitLabel = loginSubmit.GetComponentInChildren<TMP_Text>();

        // Register Panel (with clear field titles, Show/Hide toggle, and live password checklist)
        registerPanel = CreateStretchPanel(formCard.transform, "RegisterPanel", Color.clear);
        CreateFieldTitle(registerPanel.transform, "RegEmailTitle", "Email Address", new Vector2(0f, 404f));
        regEmailInput = CreateInputField(registerPanel.transform, "RegEmail", "you@example.com", "", new Vector2(0f, 354f), new Vector2(820f, 64f));
        regEmailInput.onValueChanged.AddListener(_ => ClearAuthError());

        CreateFieldTitle(registerPanel.transform, "RegUserTitle", "Username", new Vector2(0f, 294f));
        regUserInput = CreateInputField(registerPanel.transform, "RegUser", "3–16 letters or numbers", "", new Vector2(0f, 244f), new Vector2(820f, 64f));
        regUserInput.onValueChanged.AddListener(_ => ClearAuthError());

        CreateFieldTitle(registerPanel.transform, "RegPassTitle", "Password", new Vector2(0f, 184f));
        regPassInput = CreateInputField(registerPanel.transform, "RegPass", "Create a strong password", "", new Vector2(-74f, 134f), new Vector2(672f, 64f), isPassword: true);
        regPassInput.onValueChanged.AddListener(val =>
        {
            ClearAuthError();
            UpdatePasswordChecklist(val);
        });
        Button regShowBtn = CreatePillButton(registerPanel.transform, "BtnRegShowPass", "Show", new Color(0.12f, 0.28f, 0.35f, 1f), new Vector2(342f, 134f), new Vector2(136f, 64f), 21, ToggleRegisterPasswordVisibility);
        regShowPassLabel = regShowBtn.GetComponentInChildren<TMP_Text>();

        // Compact Live Password Requirement Checklist (turns green as each rule is satisfied)
        GameObject checkCard = CreateCard(registerPanel.transform, "PasswordChecklist", new Color(0.04f, 0.13f, 0.18f, 0.92f), new Vector2(0f, 58f), new Vector2(820f, 66f));
        passRuleLenLabel = CreateText(checkCard.transform, "RuleLen", "○ 8+ chars", 18, FontStyles.Bold, new Color(0.70f, 0.80f, 0.84f), new Vector2(-275f, 0f), new Vector2(230f, 44f));
        passRuleCapLabel = CreateText(checkCard.transform, "RuleCap", "○ 1 Capital (A-Z)", 18, FontStyles.Bold, new Color(0.70f, 0.80f, 0.84f), new Vector2(-30f, 0f), new Vector2(240f, 44f));
        passRuleNumSymLabel = CreateText(checkCard.transform, "RuleNumSym", "○ 1 Number / !@#$%", 18, FontStyles.Bold, new Color(0.70f, 0.80f, 0.84f), new Vector2(250f, 0f), new Vector2(280f, 44f));

        CreateFieldTitle(registerPanel.transform, "RegBirthTitle", "Date of Birth (MM/DD/YYYY)", new Vector2(0f, -6f));
        regBirthInput = CreateInputField(registerPanel.transform, "RegBirth", "MM/DD/YYYY", "", new Vector2(0f, -56f), new Vector2(820f, 64f));
        regBirthInput.onValueChanged.AddListener(_ => ClearAuthError());

        Button regSubmit = CreatePillButton(registerPanel.transform, "BtnSubmitReg", "Create Account", RoatanVisualAssets.PalmGreen, new Vector2(0f, -148f), new Vector2(680f, 84f), 32, SubmitRegister);
        registerSubmitLabel = regSubmit.GetComponentInChildren<TMP_Text>();

        // Social Sign-In Buttons (Continue with Google & Continue with Facebook)
        CreateText(formCard.transform, "SocialDivider", "────────   OR CONTINUE WITH   ────────", 20, FontStyles.Bold, new Color(0.72f, 0.85f, 0.90f), new Vector2(0f, -270f), new Vector2(820f, 34f));

        Button googleBtn = CreatePillButton(formCard.transform, "BtnGoogleAuth", "G   Continue with Google", new Color(0.96f, 0.97f, 0.99f, 1f), new Vector2(0f, -365f), new Vector2(760f, 84f), 27, () => SubmitSocialAuth("Google"));
        TMP_Text googleLbl = googleBtn.GetComponentInChildren<TMP_Text>();
        googleLbl.color = new Color(0.11f, 0.14f, 0.18f, 1f);

        CreatePillButton(formCard.transform, "BtnFacebookAuth", "f   Continue with Facebook", new Color(0.094f, 0.467f, 0.949f, 1f), new Vector2(0f, -472f), new Vector2(760f, 84f), 27, () => SubmitSocialAuth("Facebook"));

        UpdatePasswordChecklist(string.Empty);
        SelectAuthTab(isSignIn: true);
    }

    private void BuildHubView(Transform parent)
    {
        hubView = CreateStretchPanel(parent, "HubView", Color.clear);

        GameObject profileBar = CreateCard(hubView.transform, "HubProfileCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, 640f), new Vector2(940f, 88f));
        GameObject hubAvObj = new GameObject("HubAvatar");
        hubAvObj.transform.SetParent(profileBar.transform, false);
        RectTransform hubAvRect = hubAvObj.AddComponent<RectTransform>();
        hubAvRect.anchoredPosition = new Vector2(-418f, 0f);
        hubAvRect.sizeDelta = new Vector2(64f, 64f);
        hubAvatarImg = hubAvObj.AddComponent<Image>();
        hubAvatarImg.raycastTarget = true;
        Button hubAvBtn = hubAvObj.AddComponent<Button>();
        hubAvBtn.targetGraphic = hubAvatarImg;
        hubAvBtn.onClick.AddListener(OpenProfileModal);
        hubAvatarInitialsLabel = CreateText(hubAvObj.transform, "Init", "?", 24, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(56f, 44f));
        hubAvatarInitialsLabel.raycastTarget = false;

        hubWelcomeLabel = CreateText(profileBar.transform, "HubWelcome", "Online Player", 22, FontStyles.Bold, Color.white, new Vector2(-150f, 0f), new Vector2(440f, 70f));
        hubWelcomeLabel.alignment = TextAlignmentOptions.MidlineLeft;
        CreatePillButton(profileBar.transform, "BtnOpenProfile", "Profile", RoatanVisualAssets.BrightLagoon, new Vector2(168f, 0f), new Vector2(160f, 58f), 22, OpenProfileModal);
        Button outBtn = CreatePillButton(profileBar.transform, "BtnSignOut", "Sign Out", RoatanVisualAssets.SunsetCoral, new Vector2(356f, 0f), new Vector2(184f, 58f), 22, SubmitLogout);
        signOutBtnLabel = outBtn.GetComponentInChildren<TMP_Text>();

        Button rankedBtn = CreatePillButton(hubView.transform, "BtnQuickRanked", "Quick Ranked Match", RoatanVisualAssets.IslandGold, new Vector2(0f, 548f), new Vector2(940f, 72f), 28, RequestQuickRanked);
        quickRankedLabel = rankedBtn.GetComponentInChildren<TMP_Text>();
        quickRankedLabel.color = new Color(0.10f, 0.10f, 0.12f, 1f);

        // Create Table Card (with Table Style, Match Format, 1-1 Playoff & Player Count)
        GameObject createCard = CreateCard(hubView.transform, "CreateTableCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, 282f), new Vector2(940f, 430f));
        createLobbyNameInput = CreateInputField(createCard.transform, "TableNameInput", "New Table Name", "Roatan Table", new Vector2(0f, 162f), new Vector2(860f, 60f));

        // Table Style Row (Block | Draw)
        TableStyle[] styles = { TableStyle.BLOCK, TableStyle.DRAW };
        string[] styleLabels = { "Block (Cerrado)", "Classic Draw" };
        float[] styleXs = { -218f, 218f };
        for (int i = 0; i < styles.Length; i++)
        {
            TableStyle st = styles[i];
            Button sb = CreatePillButton(createCard.transform, $"Style_{st}", styleLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(styleXs[i], 88f), new Vector2(416f, 58f), 23, () =>
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
            Button fb = CreatePillButton(createCard.transform, $"Fmt_{mf}", formatLabels[i], new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(fmtXs[i], 14f), new Vector2(272f, 60f), 22, () =>
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
            Button b = CreatePillButton(createCard.transform, $"MaxP_{count}", $"{count} Players", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(xs[i], -60f), new Vector2(272f, 58f), 22, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                createLobbyMaxPlayers = count;
                RefreshCreateLobbySelections();
            });
            createMaxPlayerBtns[count] = b.GetComponent<Image>();
            createMaxPlayerObjs[count] = b.gameObject;
        }

        createOneOnePlayoffBtn = CreatePillButton(createCard.transform, "BtnOnlineOneOne", "4P (2v2)  •  1-1 Playoff: OFF", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(0f, -60f), new Vector2(856f, 58f), 23, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            createOneOnePlayoff = !createOneOnePlayoff;
            RefreshCreateLobbySelections();
        });
        createOneOnePlayoffImg = createOneOnePlayoffBtn.GetComponent<Image>();
        createOneOnePlayoffLabel = createOneOnePlayoffBtn.GetComponentInChildren<TMP_Text>();

        Button createBtn = CreatePillButton(createCard.transform, "BtnCreateTable", "Create Table", RoatanVisualAssets.PalmGreen, new Vector2(0f, -148f), new Vector2(640f, 70f), 27, RequestCreateLobby);
        createLobbyBtnLabel = createBtn.GetComponentInChildren<TMP_Text>();

        RefreshCreateLobbySelections();

        // Join by Private Table Code Row
        GameObject codeCard = CreateCard(hubView.transform, "CodeCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, 12f), new Vector2(940f, 84f));
        joinCodeInput = CreateInputField(codeCard.transform, "JoinCodeInput", "Private Table Code", "", new Vector2(-150f, 0f), new Vector2(560f, 60f));
        Button codeBtn = CreatePillButton(codeCard.transform, "BtnJoinCode", "Join Code", RoatanVisualAssets.BrightLagoon, new Vector2(290f, 0f), new Vector2(280f, 60f), 24, RequestJoinByCode);
        joinCodeBtnLabel = codeBtn.GetComponentInChildren<TMP_Text>();

        // Public Tables + Online/Offline Players + Leaderboard + Teams + Chat Browser Card
        GameObject listCard = CreateCard(hubView.transform, "TablesBrowserCard", new Color(0.06f, 0.18f, 0.24f, 0.94f), new Vector2(0f, -412f), new Vector2(940f, 730f));

        Button tablesTabBtn = CreatePillButton(listCard.transform, "TabPublicTables", "Tables", RoatanVisualAssets.BrightLagoon, new Vector2(-384f, 314f), new Vector2(126f, 56f), 17, () => SelectHubBrowserTab(HubBrowserTab.Tables));
        hubTablesTabImg = tablesTabBtn.GetComponent<Image>();
        hubTablesTabLabel = tablesTabBtn.GetComponentInChildren<TMP_Text>();

        Button playersTabBtn = CreatePillButton(listCard.transform, "TabPlayersRoster", "Players (0)", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(-232f, 314f), new Vector2(162f, 56f), 16, () => SelectHubBrowserTab(HubBrowserTab.Players));
        hubPlayersTabImg = playersTabBtn.GetComponent<Image>();
        hubPlayersTabLabel = playersTabBtn.GetComponentInChildren<TMP_Text>();

        Button leaderboardTabBtn = CreatePillButton(listCard.transform, "TabLeaderboard", "Ranks", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(-78f, 314f), new Vector2(130f, 56f), 17, () => SelectHubBrowserTab(HubBrowserTab.Leaderboard));
        hubLeaderboardTabImg = leaderboardTabBtn.GetComponent<Image>();
        hubLeaderboardTabLabel = leaderboardTabBtn.GetComponentInChildren<TMP_Text>();

        Button teamsTabBtn = CreatePillButton(listCard.transform, "TabTeams", "Teams", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(72f, 314f), new Vector2(154f, 56f), 17, () => SelectHubBrowserTab(HubBrowserTab.Teams));
        hubTeamsTabImg = teamsTabBtn.GetComponent<Image>();
        hubTeamsTabLabel = teamsTabBtn.GetComponentInChildren<TMP_Text>();

        Button chatTabBtn = CreatePillButton(listCard.transform, "TabSocialChat", "Chat", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(228f, 314f), new Vector2(142f, 56f), 17, () => SelectHubBrowserTab(HubBrowserTab.Chat));
        hubChatTabImg = chatTabBtn.GetComponent<Image>();
        hubChatTabLabel = chatTabBtn.GetComponentInChildren<TMP_Text>();

        Button refreshBtn = CreatePillButton(listCard.transform, "BtnRefreshTables", "Refresh", new Color(0.10f, 0.34f, 0.42f, 1f), new Vector2(382f, 314f), new Vector2(124f, 56f), 16, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            RequestLobbiesList();
            RequestPlayerRoster();
            RequestLeaderboard();
            RequestTeamLeaderboard();
            RequestMyTeam();
            RequestTeamsList();
            RequestOpenTeamChallenges();
            RequestChatHistories();
        });
        refreshLobbiesBtnLabel = refreshBtn.GetComponentInChildren<TMP_Text>();

        hubTablesScrollObj = CreateVerticalScrollList(listCard.transform, "TablesScroll", new Vector2(24f, 20f), new Vector2(-24f, -104f), out lobbiesListContent);
        hubPlayersScrollObj = CreateVerticalScrollList(listCard.transform, "PlayersScroll", new Vector2(24f, 20f), new Vector2(-24f, -104f), out playersRosterContent);
        hubTeamsScrollObj = CreateVerticalScrollList(listCard.transform, "TeamsScroll", new Vector2(24f, 20f), new Vector2(-24f, -104f), out teamsContentRoot);

        // Chat Tab View: Team Chat | Direct Messages + Thread Chips + Scrollable Messages + Compose Bar
        hubChatViewObj = new GameObject("SocialChatView");
        hubChatViewObj.transform.SetParent(listCard.transform, false);
        RectTransform chatRootRect = hubChatViewObj.AddComponent<RectTransform>();
        chatRootRect.anchorMin = Vector2.zero;
        chatRootRect.anchorMax = Vector2.one;
        chatRootRect.offsetMin = new Vector2(24f, 20f);
        chatRootRect.offsetMax = new Vector2(-24f, -104f);

        Button chatTeamBtn = CreatePillButton(hubChatViewObj.transform, "BtnChatTeamChannel", "Team Chat", RoatanVisualAssets.PalmGreen, new Vector2(-224f, 276f), new Vector2(436f, 44f), 19, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            SelectChatChannel(showDirectMessages: false);
        });
        chatTeamSubTabImg = chatTeamBtn.GetComponent<Image>();
        chatTeamSubTabLabel = chatTeamBtn.GetComponentInChildren<TMP_Text>();

        Button chatDmBtn = CreatePillButton(hubChatViewObj.transform, "BtnChatDmChannel", "Direct Messages", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(224f, 276f), new Vector2(436f, 44f), 19, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            SelectChatChannel(showDirectMessages: true);
        });
        chatDmSubTabImg = chatDmBtn.GetComponent<Image>();
        chatDmSubTabLabel = chatDmBtn.GetComponentInChildren<TMP_Text>();

        GameObject chatHdrBar = CreateCard(hubChatViewObj.transform, "ChatChannelHeader", new Color(0.04f, 0.13f, 0.18f, 0.92f), new Vector2(0f, 224f), new Vector2(892f, 40f));
        chatActiveChannelHeader = CreateText(chatHdrBar.transform, "HeaderTxt", "TEAM CHAT", 18, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(856f, 32f));
        chatActiveChannelHeader.alignment = TextAlignmentOptions.MidlineLeft;

        // Horizontal DM Partners Strip (visible only in Direct Messages mode)
        chatDmPartnersStripObj = CreateCard(hubChatViewObj.transform, "ChatDmPartnersStrip", new Color(0.05f, 0.15f, 0.20f, 0.92f), new Vector2(0f, 168f), new Vector2(892f, 58f));
        GameObject dmStripContentObj = new GameObject("DmPartnersContent");
        dmStripContentObj.transform.SetParent(chatDmPartnersStripObj.transform, false);
        chatDmPartnersContent = dmStripContentObj.AddComponent<RectTransform>();
        chatDmPartnersContent.anchorMin = Vector2.zero;
        chatDmPartnersContent.anchorMax = Vector2.one;
        chatDmPartnersContent.offsetMin = new Vector2(10f, 6f);
        chatDmPartnersContent.offsetMax = new Vector2(-10f, -6f);
        HorizontalLayoutGroup dmHlg = dmStripContentObj.AddComponent<HorizontalLayoutGroup>();
        dmHlg.spacing = 10f;
        dmHlg.childAlignment = TextAnchor.MiddleLeft;
        dmHlg.childControlWidth = false;
        dmHlg.childControlHeight = false;
        dmHlg.childForceExpandWidth = false;
        dmHlg.childForceExpandHeight = false;
        chatDmPartnersStripObj.SetActive(false);

        CreateVerticalScrollList(hubChatViewObj.transform, "ChatMessagesScroll", new Vector2(0f, 76f), new Vector2(0f, -158f), out chatMessagesListContent);

        GameObject composeBar = CreateCard(hubChatViewObj.transform, "ChatComposeBar", new Color(0.04f, 0.13f, 0.18f, 0.96f), new Vector2(0f, -268f), new Vector2(892f, 68f));
        chatComposeInput = CreateInputField(composeBar.transform, "ChatComposeInput", "Type a message...", "", new Vector2(-92f, 0f), new Vector2(680f, 52f));
        CreatePillButton(composeBar.transform, "BtnSendSocialChat", "Send", RoatanVisualAssets.PalmGreen, new Vector2(348f, 0f), new Vector2(172f, 52f), 20, SubmitSocialChatMessage);

        // Leaderboard Tab View: Players | Teams sub-toggle + Pinned Banner + Column Headers + Scrollable List
        hubLeaderboardViewObj = new GameObject("LeaderboardView");
        hubLeaderboardViewObj.transform.SetParent(listCard.transform, false);
        RectTransform lbRootRect = hubLeaderboardViewObj.AddComponent<RectTransform>();
        lbRootRect.anchorMin = Vector2.zero;
        lbRootRect.anchorMax = Vector2.one;
        lbRootRect.offsetMin = new Vector2(24f, 20f);
        lbRootRect.offsetMax = new Vector2(-24f, -104f);

        GameObject lbToggleRow = new GameObject("LeaderboardSubToggle");
        lbToggleRow.transform.SetParent(hubLeaderboardViewObj.transform, false);
        RectTransform lbToggleRect = lbToggleRow.AddComponent<RectTransform>();
        lbToggleRect.anchorMin = new Vector2(0.5f, 1f);
        lbToggleRect.anchorMax = new Vector2(0.5f, 1f);
        lbToggleRect.pivot = new Vector2(0.5f, 1f);
        lbToggleRect.anchoredPosition = new Vector2(0f, 0f);
        lbToggleRect.sizeDelta = new Vector2(892f, 48f);

        Button lbPlayersBtn = CreatePillButton(lbToggleRow.transform, "BtnLbPlayers", "Players", RoatanVisualAssets.IslandGold, new Vector2(-224f, -24f), new Vector2(436f, 44f), 20, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            SelectLeaderboardSubTab(showTeams: false);
        });
        lbPlayersSubTabImg = lbPlayersBtn.GetComponent<Image>();
        lbPlayersSubTabLabel = lbPlayersBtn.GetComponentInChildren<TMP_Text>();

        Button lbTeamsBtn = CreatePillButton(lbToggleRow.transform, "BtnLbTeams", "Teams", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(224f, -24f), new Vector2(436f, 44f), 20, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            SelectLeaderboardSubTab(showTeams: true);
        });
        lbTeamsSubTabImg = lbTeamsBtn.GetComponent<Image>();
        lbTeamsSubTabLabel = lbTeamsBtn.GetComponentInChildren<TMP_Text>();

        // --- Players Sub-View ---
        lbPlayersSubViewObj = new GameObject("PlayersLeaderboardSubView");
        lbPlayersSubViewObj.transform.SetParent(hubLeaderboardViewObj.transform, false);
        RectTransform lbpRect = lbPlayersSubViewObj.AddComponent<RectTransform>();
        lbpRect.anchorMin = Vector2.zero;
        lbpRect.anchorMax = Vector2.one;
        lbpRect.offsetMin = new Vector2(0f, 0f);
        lbpRect.offsetMax = new Vector2(0f, -54f);

        leaderboardSelfBannerCard = CreateCard(lbPlayersSubViewObj.transform, "LeaderboardSelfBanner", new Color(0.09f, 0.31f, 0.36f, 0.98f), Vector2.zero, new Vector2(892f, 92f));
        RectTransform selfRect = leaderboardSelfBannerCard.GetComponent<RectTransform>();
        selfRect.anchorMin = new Vector2(0.5f, 1f);
        selfRect.anchorMax = new Vector2(0.5f, 1f);
        selfRect.pivot = new Vector2(0.5f, 1f);
        selfRect.anchoredPosition = new Vector2(0f, 0f);

        leaderboardSelfRankLabel = CreateText(leaderboardSelfBannerCard.transform, "SelfRank", "#—", 24, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-404f, 0f), new Vector2(72f, 54f));
        GameObject selfAvObj = new GameObject("SelfAvatar");
        selfAvObj.transform.SetParent(leaderboardSelfBannerCard.transform, false);
        RectTransform selfAvRect = selfAvObj.AddComponent<RectTransform>();
        selfAvRect.anchoredPosition = new Vector2(-334f, 0f);
        selfAvRect.sizeDelta = new Vector2(58f, 58f);
        leaderboardSelfAvatarImg = selfAvObj.AddComponent<Image>();
        leaderboardSelfAvatarImg.raycastTarget = false;
        leaderboardSelfAvatarInitials = CreateText(selfAvObj.transform, "Init", "?", 21, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(50f, 38f));
        leaderboardSelfAvatarInitials.raycastTarget = false;

        leaderboardSelfNameTierLabel = CreateText(leaderboardSelfBannerCard.transform, "SelfNameTier", "Your Rank", 20, FontStyles.Bold, Color.white, new Vector2(-115f, 0f), new Vector2(360f, 74f));
        leaderboardSelfNameTierLabel.alignment = TextAlignmentOptions.MidlineLeft;
        leaderboardSelfStatsLabel = CreateText(leaderboardSelfBannerCard.transform, "SelfStats", "1000  •  0/0  •  —  •  0 Ast", 18, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(248f, 0f), new Vector2(360f, 64f));
        leaderboardSelfStatsLabel.alignment = TextAlignmentOptions.MidlineRight;

        GameObject colHdr = CreateCard(lbPlayersSubViewObj.transform, "LeaderboardColumns", new Color(0.04f, 0.13f, 0.18f, 0.90f), Vector2.zero, new Vector2(892f, 34f));
        RectTransform chRect = colHdr.GetComponent<RectTransform>();
        chRect.anchorMin = new Vector2(0.5f, 1f);
        chRect.anchorMax = new Vector2(0.5f, 1f);
        chRect.pivot = new Vector2(0.5f, 1f);
        chRect.anchoredPosition = new Vector2(0f, -100f);
        CreateText(colHdr.transform, "ColRank", "#", 16, FontStyles.Bold, new Color(0.70f, 0.85f, 0.90f), new Vector2(-404f, 0f), new Vector2(64f, 28f));
        TMP_Text colPlayer = CreateText(colHdr.transform, "ColPlayer", "PLAYER / TIER", 16, FontStyles.Bold, new Color(0.70f, 0.85f, 0.90f), new Vector2(-150f, 0f), new Vector2(320f, 28f));
        colPlayer.alignment = TextAlignmentOptions.MidlineLeft;
        CreateText(colHdr.transform, "ColElo", "ELO", 16, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(105f, 0f), new Vector2(96f, 28f));
        CreateText(colHdr.transform, "ColWL", "W/L", 16, FontStyles.Bold, new Color(0.70f, 0.85f, 0.90f), new Vector2(210f, 0f), new Vector2(94f, 28f));
        CreateText(colHdr.transform, "ColAcc", "ACC %", 16, FontStyles.Bold, new Color(0.70f, 0.85f, 0.90f), new Vector2(310f, 0f), new Vector2(96f, 28f));
        CreateText(colHdr.transform, "ColAst", "AST", 16, FontStyles.Bold, new Color(0.70f, 0.85f, 0.90f), new Vector2(400f, 0f), new Vector2(74f, 28f));

        CreateVerticalScrollList(lbPlayersSubViewObj.transform, "LeaderboardScroll", new Vector2(0f, 0f), new Vector2(0f, -142f), out leaderboardListContent);

        // --- Teams Sub-View ---
        lbTeamsSubViewObj = new GameObject("TeamsLeaderboardSubView");
        lbTeamsSubViewObj.transform.SetParent(hubLeaderboardViewObj.transform, false);
        RectTransform lbtRect = lbTeamsSubViewObj.AddComponent<RectTransform>();
        lbtRect.anchorMin = Vector2.zero;
        lbtRect.anchorMax = Vector2.one;
        lbtRect.offsetMin = new Vector2(0f, 0f);
        lbtRect.offsetMax = new Vector2(0f, -54f);

        teamLeaderboardSelfCard = CreateCard(lbTeamsSubViewObj.transform, "TeamLeaderboardSelfBanner", new Color(0.08f, 0.29f, 0.30f, 0.98f), Vector2.zero, new Vector2(892f, 88f));
        RectTransform tSelfRect = teamLeaderboardSelfCard.GetComponent<RectTransform>();
        tSelfRect.anchorMin = new Vector2(0.5f, 1f);
        tSelfRect.anchorMax = new Vector2(0.5f, 1f);
        tSelfRect.pivot = new Vector2(0.5f, 1f);
        tSelfRect.anchoredPosition = new Vector2(0f, 0f);

        teamLeaderboardSelfRankLabel = CreateText(teamLeaderboardSelfCard.transform, "MyTeamRank", "#—", 24, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-404f, 0f), new Vector2(72f, 52f));
        teamLeaderboardSelfNameTierLabel = CreateText(teamLeaderboardSelfCard.transform, "MyTeamNameTier", "Join or create a team to climb the Crew Leaderboard!", 19, FontStyles.Bold, Color.white, new Vector2(-112f, 0f), new Vector2(480f, 70f));
        teamLeaderboardSelfNameTierLabel.alignment = TextAlignmentOptions.MidlineLeft;
        teamLeaderboardSelfStatsLabel = CreateText(teamLeaderboardSelfCard.transform, "MyTeamStats", "—", 18, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(280f, 0f), new Vector2(290f, 64f));
        teamLeaderboardSelfStatsLabel.alignment = TextAlignmentOptions.MidlineRight;

        GameObject tColHdr = CreateCard(lbTeamsSubViewObj.transform, "TeamLeaderboardColumns", new Color(0.04f, 0.13f, 0.18f, 0.90f), Vector2.zero, new Vector2(892f, 34f));
        RectTransform tchRect = tColHdr.GetComponent<RectTransform>();
        tchRect.anchorMin = new Vector2(0.5f, 1f);
        tchRect.anchorMax = new Vector2(0.5f, 1f);
        tchRect.pivot = new Vector2(0.5f, 1f);
        tchRect.anchoredPosition = new Vector2(0f, -96f);
        CreateText(tColHdr.transform, "TColRank", "#", 16, FontStyles.Bold, new Color(0.70f, 0.85f, 0.90f), new Vector2(-404f, 0f), new Vector2(60f, 28f));
        TMP_Text tColTeam = CreateText(tColHdr.transform, "TColTeam", "TEAM / TIER / CAPTAIN", 16, FontStyles.Bold, new Color(0.70f, 0.85f, 0.90f), new Vector2(-180f, 0f), new Vector2(360f, 28f));
        tColTeam.alignment = TextAlignmentOptions.MidlineLeft;
        CreateText(tColHdr.transform, "TColElo", "ELO", 16, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(60f, 0f), new Vector2(88f, 28f));
        CreateText(tColHdr.transform, "TColWL", "W/L", 16, FontStyles.Bold, new Color(0.70f, 0.85f, 0.90f), new Vector2(150f, 0f), new Vector2(84f, 28f));
        CreateText(tColHdr.transform, "TColCrew", "CREW", 16, FontStyles.Bold, new Color(0.70f, 0.85f, 0.90f), new Vector2(236f, 0f), new Vector2(78f, 28f));
        CreateText(tColHdr.transform, "TColAction", "CHALLENGE", 15, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(358f, 0f), new Vector2(148f, 28f));

        CreateVerticalScrollList(lbTeamsSubViewObj.transform, "TeamLeaderboardScroll", new Vector2(0f, 0f), new Vector2(0f, -138f), out teamLeaderboardListContent);

        SelectLeaderboardSubTab(showTeams: false);
        SelectHubBrowserTab(HubBrowserTab.Tables);
    }

    private void BuildLobbyView(Transform parent)
    {
        lobbyView = CreateStretchPanel(parent, "TableWaitingRoomView", Color.clear);

        GameObject card = CreateCard(lobbyView.transform, "InTableCard", new Color(0.06f, 0.18f, 0.24f, 0.96f), new Vector2(0f, -10f), new Vector2(940f, 1340f));
        lobbyTitleLabel = CreateText(card.transform, "TTitle", "Table", 38, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 608f), new Vector2(860f, 56f));
        lobbyInfoLabel = CreateText(card.transform, "TInfo", "", 22, FontStyles.Normal, new Color(0.84f, 0.95f, 0.98f), new Vector2(0f, 542f), new Vector2(860f, 66f));
        lobbyTimerLabel = CreateText(card.transform, "TTimer", "Tap an open seat to switch, or add bots to start!", 22, FontStyles.Italic, Color.white, new Vector2(0f, 480f), new Vector2(860f, 38f));

        // Visual 4-Seat / 2-Seat Domino Table Diagram
        GameObject diagramObj = new GameObject("DominoTableDiagram");
        diagramObj.transform.SetParent(card.transform, false);
        tableDiagramRoot = diagramObj.AddComponent<RectTransform>();
        tableDiagramRoot.anchoredPosition = new Vector2(0f, 150f);
        tableDiagramRoot.sizeDelta = new Vector2(890f, 580f);

        GameObject felt = CreateCard(tableDiagramRoot, "TableFelt", new Color(0.055f, 0.32f, 0.24f, 0.96f), Vector2.zero, new Vector2(360f, 210f));
        GameObject feltOutline = new GameObject("FeltBorder");
        feltOutline.transform.SetParent(felt.transform, false);
        RectTransform foRect = feltOutline.AddComponent<RectTransform>();
        foRect.anchorMin = Vector2.zero;
        foRect.anchorMax = Vector2.one;
        foRect.offsetMin = Vector2.zero;
        foRect.offsetMax = Vector2.zero;
        Image foImg = feltOutline.AddComponent<Image>();
        foImg.sprite = RoatanVisualAssets.RoundedOutlineSprite;
        foImg.type = Image.Type.Sliced;
        foImg.color = new Color(0.95f, 0.74f, 0.24f, 0.80f);
        foImg.raycastTarget = false;
        tableFeltLabel = CreateText(felt.transform, "FeltLabel", "ROATAN TABLE\n2v2 Partners", 22, FontStyles.Bold, new Color(0.96f, 0.92f, 0.76f), Vector2.zero, new Vector2(320f, 140f));

        // Host Bot Difficulty Selector Row (Novice | Pro | Master)
        hostBotDifficultyRow = CreateCard(card.transform, "HostBotDiffRow", new Color(0.04f, 0.13f, 0.18f, 0.92f), new Vector2(0f, -195f), new Vector2(860f, 78f));
        CreateText(hostBotDifficultyRow.transform, "BotDiffTitle", "Bot Level:", 21, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-315f, 0f), new Vector2(180f, 44f)).alignment = TextAlignmentOptions.MidlineLeft;
        string[] botDiffs = { "Novice", "Pro", "Master" };
        float[] diffXs = { -115f, 95f, 305f };
        for (int i = 0; i < botDiffs.Length; i++)
        {
            string diffName = botDiffs[i];
            Button db = CreatePillButton(hostBotDifficultyRow.transform, $"HostBotDiff_{diffName}", diffName, new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(diffXs[i], 0f), new Vector2(190f, 54f), 22, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                selectedHostBotDifficulty = diffName;
                RefreshHostBotDifficultyButtons();
            });
            hostBotDiffBtns[diffName] = db.GetComponent<Image>();
        }
        RefreshHostBotDifficultyButtons();

        // Quick Invite Online Players Button
        CreatePillButton(card.transform, "BtnInviteOnlinePlayers", "Invite Online Players", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -290f), new Vector2(560f, 68f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            RequestPlayerRoster();
            if (invitePlayersModal != null)
            {
                RefreshInvitePlayersModalUI();
                invitePlayersModal.transform.SetAsLastSibling();
                invitePlayersModal.SetActive(true);
            }
        });

        // Primary Action Row: Host Start Match + Ready Toggle
        startMatchBtn = CreatePillButton(card.transform, "BtnStartMatch", "Start Match", RoatanVisualAssets.PalmGreen, new Vector2(-145f, -402f), new Vector2(470f, 94f), 30, RequestStartTableMatch);
        startMatchBtnImg = startMatchBtn.GetComponent<Image>();
        startMatchBtnLabel = startMatchBtn.GetComponentInChildren<TMP_Text>();

        readyBtnObj = CreatePillButton(card.transform, "BtnTableReady", "Ready", RoatanVisualAssets.PalmGreen, new Vector2(265f, -402f), new Vector2(310f, 94f), 28, ToggleLobbyReady);
        readyBtnImg = readyBtnObj.GetComponent<Image>();
        readyBtnLabel = readyBtnObj.GetComponentInChildren<TMP_Text>();

        Button leaveBtn = CreatePillButton(card.transform, "BtnTableLeave", "Leave Table", RoatanVisualAssets.SunsetCoral, new Vector2(0f, -525f), new Vector2(440f, 78f), 27, LeaveCurrentLobby);
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

        // Live Spectator Viewer Count Badge + Leave Spectating Button
        onlineSpectatorBadgeObj = CreateCard(onlineGameView.transform, "OnlineSpectatorBadge", new Color(0.10f, 0.25f, 0.35f, 0.96f), Vector2.zero, new Vector2(236f, 48f));
        RectTransform spBadgeRect = onlineSpectatorBadgeObj.GetComponent<RectTransform>();
        spBadgeRect.anchorMin = new Vector2(1f, 1f);
        spBadgeRect.anchorMax = new Vector2(1f, 1f);
        spBadgeRect.pivot = new Vector2(1f, 1f);
        spBadgeRect.anchoredPosition = new Vector2(-24f, -276f);
        onlineSpectatorBadgeLabel = CreateText(onlineSpectatorBadgeObj.transform, "SpectatorCountTxt", "Watching: 0", 19, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(220f, 38f));
        onlineSpectatorBadgeObj.SetActive(false);

        onlineLeaveSpectateBtn = CreatePillButton(onlineGameView.transform, "BtnLeaveSpectating", "Leave Spectating", RoatanVisualAssets.SunsetCoral, Vector2.zero, new Vector2(360f, 76f), 25, LeaveSpectatingMatch);
        RectTransform lsRect = onlineLeaveSpectateBtn.GetComponent<RectTransform>();
        lsRect.anchorMin = new Vector2(0.5f, 0f);
        lsRect.anchorMax = new Vector2(0.5f, 0f);
        lsRect.pivot = new Vector2(0.5f, 0f);
        lsRect.anchoredPosition = new Vector2(0f, 110f);
        onlineLeaveSpectateBtn.gameObject.SetActive(false);

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

        // Online 10s Turn Timer / 5s Auto-Pass Pill (clean breathing room above status card)
        onlineTimerBadgeObj = CreateCard(onlineGameView.transform, "OnlineTurnTimerBadge", new Color(0.08f, 0.34f, 0.38f, 0.96f), Vector2.zero, new Vector2(275f, 64f));
        RectTransform otRect = onlineTimerBadgeObj.GetComponent<RectTransform>();
        otRect.anchorMin = new Vector2(0f, 0f);
        otRect.anchorMax = new Vector2(0f, 0f);
        otRect.pivot = new Vector2(0f, 0f);
        otRect.anchoredPosition = new Vector2(24f, 452f);
        onlineTimerBadgeBg = onlineTimerBadgeObj.GetComponent<Image>();
        onlineTimerLabel = CreateText(onlineTimerBadgeObj.transform, "TimerText", "TURN TIMER: 10s", 22, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(255f, 52f));
        onlineTimerBadgeObj.SetActive(false);

        // Online "Bot Auto-Playing" Indicator + Instant "Resume Control" Banner
        onlineBotIndicatorObj = CreateCard(onlineGameView.transform, "OnlineBotIndicator", new Color(0.58f, 0.22f, 0.14f, 0.96f), Vector2.zero, new Vector2(290f, 58f));
        RectTransform obiRect = onlineBotIndicatorObj.GetComponent<RectTransform>();
        obiRect.anchorMin = new Vector2(0f, 0f);
        obiRect.anchorMax = new Vector2(0f, 0f);
        obiRect.pivot = new Vector2(0f, 0f);
        obiRect.anchoredPosition = new Vector2(24f, 524f);
        CreateText(onlineBotIndicatorObj.transform, "BotText", "Bot Auto-Playing", 22, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(270f, 46f));
        onlineBotIndicatorObj.SetActive(false);

        onlineResumeControlBtn = CreatePillButton(onlineGameView.transform, "BtnOnlineResumeControl", "Resume Control (Tap Here or Any Tile)", RoatanVisualAssets.PalmGreen, Vector2.zero, new Vector2(610f, 76f), 25, ReclaimOnlineManualControl);
        RectTransform orcRect = onlineResumeControlBtn.GetComponent<RectTransform>();
        orcRect.anchorMin = new Vector2(0.5f, 0f);
        orcRect.anchorMax = new Vector2(0.5f, 0f);
        orcRect.pivot = new Vector2(0.5f, 0f);
        orcRect.anchoredPosition = new Vector2(0f, 430f);
        onlineResumeControlBtn.gameObject.SetActive(false);

        // Online Post-Hand / Post-Break Overlay (with Post-Match Elo & Rank Breakdown)
        onlineEndOverlay = CreateStretchPanel(onlineGameView.transform, "OnlineEndOverlay", new Color(0f, 0f, 0f, 0.74f));
        GameObject endCard = CreateCard(onlineEndOverlay.transform, "EndCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(880f, 840f));
        onlineEndTitleLabel = CreateText(endCard.transform, "EndTitle", "MATCH COMPLETE", 40, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 338f), new Vector2(800f, 60f));
        onlineEndBodyLabel = CreateText(endCard.transform, "EndBody", "", 25, FontStyles.Normal, Color.white, new Vector2(0f, 232f), new Vector2(780f, 124f));

        onlineEndEloBreakdownCard = CreateCard(endCard.transform, "EloBreakdownCard", new Color(0.05f, 0.17f, 0.23f, 0.96f), new Vector2(0f, 84f), new Vector2(800f, 148f));
        onlineEndEloBreakdownTitle = CreateText(onlineEndEloBreakdownCard.transform, "EloTitle", "", 24, FontStyles.Bold, Color.white, new Vector2(0f, 34f), new Vector2(760f, 44f));
        onlineEndEloBreakdownBody = CreateText(onlineEndEloBreakdownCard.transform, "EloBody", "", 19, FontStyles.Normal, new Color(0.86f, 0.95f, 0.98f), new Vector2(0f, -24f), new Vector2(760f, 72f));
        onlineEndEloBreakdownCard.SetActive(false);

        onlineEndPrimaryBtn = CreatePillButton(endCard.transform, "BtnOnlinePrimary", "Next Round", RoatanVisualAssets.PalmGreen, new Vector2(0f, -92f), new Vector2(520f, 88f), 31, () => { });
        onlineEndPrimaryBtnLabel = onlineEndPrimaryBtn.GetComponentInChildren<TMP_Text>();

        onlineEndNewMatchBtn = CreatePillButton(endCard.transform, "BtnOnlineNewMatch", "New Match", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -200f), new Vector2(460f, 80f), 27, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            onlineEndOverlay.SetActive(false);
            ShowSubView(hubView);
            RequestLobbiesList();
            RequestLeaderboard();
        });
        onlineEndNewMatchBtn.gameObject.SetActive(false);

        CreatePillButton(endCard.transform, "BtnOnlineLeave", "Leave", new Color(0.24f, 0.20f, 0.24f, 1f), new Vector2(0f, -306f), new Vector2(380f, 74f), 27, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            RoatanMatchSessionStore.ClearSession();
            pendingSavedSession = null;
            onlineEndOverlay.SetActive(false);
            subscribedClient?.AddToSendQueue(MessageFrameBuilder.LeaveGameFrame());
            ShowSubView(hubView);
            RequestLobbiesList();
            RequestLeaderboard();
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

    private void BuildIncomingInviteModal(Transform safeRoot)
    {
        incomingInviteModal = CreateStretchPanel(safeRoot, "IncomingInviteModal", new Color(0f, 0f, 0f, 0.76f));
        GameObject card = CreateCard(incomingInviteModal.transform, "InviteCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(840f, 540f));
        CreateText(card.transform, "InvTitle", "TABLE INVITE", 38, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 195f), new Vector2(760f, 58f));
        incomingInviteBodyLabel = CreateText(card.transform, "InvBody", "", 27, FontStyles.Normal, Color.white, new Vector2(0f, 45f), new Vector2(760f, 180f));

        CreatePillButton(card.transform, "BtnAcceptInvite", "Join Table", RoatanVisualAssets.PalmGreen, new Vector2(0f, -105f), new Vector2(520f, 88f), 32, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            incomingInviteModal.SetActive(false);
            if (pendingIncomingInvite != null && EnsureConnectedForRequest())
            {
                if (currentLobby != null)
                {
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.LeaveLobbyFrame());
                    currentLobby = null;
                }
                if (!string.IsNullOrWhiteSpace(pendingIncomingInvite.TableCode))
                {
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyWithCodeFrame(new LobbyCodeDto { Code = pendingIncomingInvite.TableCode }));
                }
                else
                {
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyFrame(new JoinLobbyDto { LobbyIdToJoin = pendingIncomingInvite.TableId }));
                }
            }
            pendingIncomingInvite = null;
        });

        CreatePillButton(card.transform, "BtnDeclineInvite", "Dismiss", new Color(0.24f, 0.22f, 0.26f, 1f), new Vector2(0f, -205f), new Vector2(360f, 70f), 26, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            pendingIncomingInvite = null;
            incomingInviteModal.SetActive(false);
        });

        incomingInviteModal.SetActive(false);
    }

    private void BuildInvitePlayersModal(Transform safeRoot)
    {
        invitePlayersModal = CreateStretchPanel(safeRoot, "InvitePlayersModal", new Color(0f, 0f, 0f, 0.76f));
        GameObject card = CreateCard(invitePlayersModal.transform, "InviteRosterCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(880f, 960f));
        CreateText(card.transform, "Title", "INVITE ONLINE PLAYERS", 34, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-40f, 422f), new Vector2(680f, 54f));
        CreatePillButton(card.transform, "BtnCloseInviteModal", "X", new Color(0.24f, 0.22f, 0.26f, 1f), new Vector2(375f, 422f), new Vector2(68f, 60f), 28, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            invitePlayersModal.SetActive(false);
        });

        CreateVerticalScrollList(card.transform, "InviteRosterScroll", new Vector2(28f, 28f), new Vector2(-28f, -110f), out invitePlayersListContent);
        invitePlayersModal.SetActive(false);
    }

    private void BuildChallengeTeamModal(Transform safeRoot)
    {
        challengeTeamModal = CreateStretchPanel(safeRoot, "ChallengeTeamModal", new Color(0f, 0f, 0f, 0.78f));
        GameObject card = CreateCard(challengeTeamModal.transform, "ChallengeTeamCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(840f, 650f));

        CreateText(card.transform, "Header", "CHALLENGE TEAM", 34, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 256f), new Vector2(760f, 52f));
        challengeTeamModalTitle = CreateText(card.transform, "TargetTeam", "Target Crew", 23, FontStyles.Bold, Color.white, new Vector2(0f, 196f), new Vector2(760f, 44f));

        CreateText(card.transform, "ModeLbl", "GAME MODE", 19, FontStyles.Bold, new Color(0.75f, 0.88f, 0.93f), new Vector2(0f, 130f), new Vector2(740f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;
        Button btnSixLov = CreatePillButton(card.transform, "BtnChSixLov", "Six-Lov (2v2)", RoatanVisualAssets.IslandGold, new Vector2(-186f, 70f), new Vector2(356f, 68f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            challengeSelectedMode = MatchFormat.SIX_LOV;
            RefreshChallengeModalButtons();
        });
        challengeModeSixLovImg = btnSixLov.GetComponent<Image>();

        Button btnTwoBest = CreatePillButton(card.transform, "BtnChTwoBest", "Two-Best (1v1)", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(186f, 70f), new Vector2(356f, 68f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            challengeSelectedMode = MatchFormat.TWO_BEST;
            RefreshChallengeModalButtons();
        });
        challengeModeTwoBestImg = btnTwoBest.GetComponent<Image>();

        CreateText(card.transform, "VarLbl", "VARIANT", 19, FontStyles.Bold, new Color(0.75f, 0.88f, 0.93f), new Vector2(0f, -4f), new Vector2(740f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;
        Button btnDraw = CreatePillButton(card.transform, "BtnChDraw", "Draw", RoatanVisualAssets.BrightLagoon, new Vector2(-186f, -64f), new Vector2(356f, 68f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            challengeSelectedVariant = TableStyle.DRAW;
            RefreshChallengeModalButtons();
        });
        challengeVariantDrawImg = btnDraw.GetComponent<Image>();

        Button btnBlock = CreatePillButton(card.transform, "BtnChBlock", "Block (Cerrado)", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(186f, -64f), new Vector2(356f, 68f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            challengeSelectedVariant = TableStyle.BLOCK;
            RefreshChallengeModalButtons();
        });
        challengeVariantBlockImg = btnBlock.GetComponent<Image>();

        CreatePillButton(card.transform, "BtnSubmitTeamChallenge", "Send Challenge & Open Table", RoatanVisualAssets.PalmGreen, new Vector2(0f, -166f), new Vector2(640f, 80f), 24, SubmitTeamChallenge);
        CreatePillButton(card.transform, "BtnCancelTeamChallenge", "Cancel", new Color(0.24f, 0.22f, 0.26f, 1f), new Vector2(0f, -254f), new Vector2(360f, 64f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            challengeTargetTeam = null;
            challengeTeamModal.SetActive(false);
        });

        RefreshChallengeModalButtons();
        challengeTeamModal.SetActive(false);
    }

    private void BuildIncomingTeamChallengeModal(Transform safeRoot)
    {
        incomingTeamChallengeModal = CreateStretchPanel(safeRoot, "IncomingTeamChallengeModal", new Color(0f, 0f, 0f, 0.80f));
        GameObject card = CreateCard(incomingTeamChallengeModal.transform, "IncomingChallengeCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(840f, 590f));

        incomingTeamChallengeTitle = CreateText(card.transform, "Title", "TEAM CHALLENGE!", 36, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 214f), new Vector2(760f, 54f));
        incomingTeamChallengeBody = CreateText(card.transform, "Body", "A rival crew has challenged your team!", 23, FontStyles.Normal, Color.white, new Vector2(0f, 68f), new Vector2(740f, 210f));

        incomingTeamChallengeAcceptBtn = CreatePillButton(card.transform, "BtnAcceptTeamChallenge", "Accept & Join Table", RoatanVisualAssets.PalmGreen, new Vector2(0f, -110f), new Vector2(600f, 84f), 25, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            incomingTeamChallengeModal.SetActive(false);
            TeamChallengeDto ch = pendingTeamChallenge;
            pendingTeamChallenge = null;
            if (ch == null || !EnsureConnectedForRequest()) return;

            if (ch.Status == TeamChallengeStatus.Pending)
            {
                if (currentLobby != null)
                {
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.LeaveLobbyFrame());
                    currentLobby = null;
                }
                subscribedClient.AddToSendQueue(MessageFrameBuilder.RespondTeamChallengeFrame(new RespondTeamChallengeDto
                {
                    ChallengeId = ch.ChallengeId,
                    Accept = true
                }));
            }
            else if (ch.Status == TeamChallengeStatus.Accepted)
            {
                if (currentLobby != null && currentLobby.Id == ch.TableId)
                {
                    ShowSubView(lobbyView);
                    RefreshLobbyView();
                    return;
                }
                if (currentLobby != null)
                {
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.LeaveLobbyFrame());
                    currentLobby = null;
                }
                if (!string.IsNullOrWhiteSpace(ch.TableCode))
                {
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyWithCodeFrame(new LobbyCodeDto { Code = ch.TableCode }));
                }
                else
                {
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyFrame(new JoinLobbyDto { LobbyIdToJoin = ch.TableId }));
                }
            }
        });
        incomingTeamChallengeAcceptLabel = incomingTeamChallengeAcceptBtn.GetComponentInChildren<TMP_Text>();

        incomingTeamChallengeDeclineBtn = CreatePillButton(card.transform, "BtnDeclineTeamChallenge", "Decline", RoatanVisualAssets.SunsetCoral, new Vector2(0f, -210f), new Vector2(400f, 70f), 23, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            incomingTeamChallengeModal.SetActive(false);
            TeamChallengeDto ch = pendingTeamChallenge;
            pendingTeamChallenge = null;
            if (ch != null && ch.Status == TeamChallengeStatus.Pending && EnsureConnectedForRequest())
            {
                subscribedClient.AddToSendQueue(MessageFrameBuilder.RespondTeamChallengeFrame(new RespondTeamChallengeDto
                {
                    ChallengeId = ch.ChallengeId,
                    Accept = false
                }));
            }
        });
        incomingTeamChallengeDeclineLabel = incomingTeamChallengeDeclineBtn.GetComponentInChildren<TMP_Text>();

        incomingTeamChallengeModal.SetActive(false);
    }

    private void BuildPlayerActionModal(Transform safeRoot)
    {
        playerActionModal = CreateStretchPanel(safeRoot, "PlayerActionModal", new Color(0f, 0f, 0f, 0.80f));
        GameObject card = CreateCard(playerActionModal.transform, "PlayerActionCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(860f, 820f));

        CreateText(card.transform, "Header", "PLAYER PROFILE", 34, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-30f, 354f), new Vector2(680f, 50f));
        CreatePillButton(card.transform, "BtnClosePlayerAction", "X", new Color(0.24f, 0.22f, 0.26f, 1f), new Vector2(368f, 354f), new Vector2(66f, 56f), 26, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            playerActionModal.SetActive(false);
        });

        GameObject avObj = new GameObject("ActionAvatar");
        avObj.transform.SetParent(card.transform, false);
        RectTransform avRect = avObj.AddComponent<RectTransform>();
        avRect.anchoredPosition = new Vector2(0f, 232f);
        avRect.sizeDelta = new Vector2(138f, 138f);
        playerActionAvatarImg = avObj.AddComponent<Image>();
        playerActionAvatarInitials = CreateText(avObj.transform, "Init", "?", 48, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(120f, 88f));
        playerActionAvatarInitials.raycastTarget = false;

        playerActionNameTierLabel = CreateText(card.transform, "NameTier", "Player", 28, FontStyles.Bold, Color.white, new Vector2(0f, 120f), new Vector2(780f, 76f));

        GameObject statsBox = CreateCard(card.transform, "ActionStatsBox", new Color(0.04f, 0.14f, 0.19f, 0.95f), new Vector2(0f, 14f), new Vector2(780f, 118f));
        playerActionStatsLabel = CreateText(statsBox.transform, "StatsTxt", "W/L: 0-0 • Acc: — • Assists: 0 • Avg: —", 21, FontStyles.Bold, new Color(0.86f, 0.95f, 0.98f), Vector2.zero, new Vector2(746f, 102f));

        playerActionChallengeBtn = CreatePillButton(card.transform, "BtnActionChallenge", "Challenge Player (1v1)", RoatanVisualAssets.SunsetCoral, new Vector2(0f, -110f), new Vector2(640f, 78f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            playerActionModal.SetActive(false);
            OpenDirectChallengeModal(selectedActionPlayer);
        });

        playerActionMessageBtn = CreatePillButton(card.transform, "BtnActionMessage", "Send Message", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -204f), new Vector2(640f, 78f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            PlayerActionProfileData target = selectedActionPlayer;
            playerActionModal.SetActive(false);
            if (target != null)
            {
                OpenDirectMessageThread(target.UserId, target.Username);
            }
        });

        playerActionInviteTeamBtn = CreatePillButton(card.transform, "BtnActionInviteTeam", "Invite to Team", RoatanVisualAssets.PalmGreen, new Vector2(0f, -298f), new Vector2(640f, 78f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            PlayerActionProfileData target = selectedActionPlayer;
            playerActionModal.SetActive(false);
            if (target != null && EnsureConnectedForRequest())
            {
                subscribedClient.AddToSendQueue(MessageFrameBuilder.InviteToTeamFrame(new InviteToTeamDto
                {
                    TargetUserId = target.UserId,
                    TargetUsername = target.Username
                }));
                SetFeedback($"Sent team invite to {target.Username}!", RoatanVisualAssets.PalmGreen);
            }
        });

        playerActionModal.SetActive(false);
    }

    private void BuildDirectChallengeModal(Transform safeRoot)
    {
        directChallengeModal = CreateStretchPanel(safeRoot, "DirectChallengeModal", new Color(0f, 0f, 0f, 0.80f));
        GameObject card = CreateCard(directChallengeModal.transform, "DirectChallengeCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(840f, 640f));

        CreateText(card.transform, "Header", "CHALLENGE PLAYER (1v1)", 32, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 250f), new Vector2(760f, 50f));
        directChallengeModalTitle = CreateText(card.transform, "TargetLbl", "Player", 23, FontStyles.Bold, Color.white, new Vector2(0f, 192f), new Vector2(760f, 42f));

        CreateText(card.transform, "ModeHdr", "GAME MODE", 19, FontStyles.Bold, new Color(0.75f, 0.88f, 0.93f), new Vector2(0f, 126f), new Vector2(740f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;
        Button btnTwoBest = CreatePillButton(card.transform, "BtnDcTwoBest", "Two-Best (1v1)", RoatanVisualAssets.IslandGold, new Vector2(-186f, 66f), new Vector2(356f, 68f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            directChallengeMode = MatchFormat.TWO_BEST;
            RefreshDirectChallengeModalButtons();
        });
        directChallengeTwoBestImg = btnTwoBest.GetComponent<Image>();

        Button btnCutThroat = CreatePillButton(card.transform, "BtnDcCutThroat", "Cut-Throat (1v1)", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(186f, 66f), new Vector2(356f, 68f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            directChallengeMode = MatchFormat.CUT_THROAT;
            RefreshDirectChallengeModalButtons();
        });
        directChallengeCutThroatImg = btnCutThroat.GetComponent<Image>();

        CreateText(card.transform, "VarHdr", "VARIANT", 19, FontStyles.Bold, new Color(0.75f, 0.88f, 0.93f), new Vector2(0f, -8f), new Vector2(740f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;
        Button btnDraw = CreatePillButton(card.transform, "BtnDcDraw", "Draw", RoatanVisualAssets.BrightLagoon, new Vector2(-186f, -68f), new Vector2(356f, 68f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            directChallengeVariant = TableStyle.DRAW;
            RefreshDirectChallengeModalButtons();
        });
        directChallengeDrawImg = btnDraw.GetComponent<Image>();

        Button btnBlock = CreatePillButton(card.transform, "BtnDcBlock", "Block (Cerrado)", new Color(0.14f, 0.22f, 0.28f, 1f), new Vector2(186f, -68f), new Vector2(356f, 68f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            directChallengeVariant = TableStyle.BLOCK;
            RefreshDirectChallengeModalButtons();
        });
        directChallengeBlockImg = btnBlock.GetComponent<Image>();

        CreatePillButton(card.transform, "BtnSubmitDirectChallenge", "Send 1v1 Challenge", RoatanVisualAssets.PalmGreen, new Vector2(0f, -168f), new Vector2(600f, 78f), 24, SubmitDirectChallenge);
        CreatePillButton(card.transform, "BtnCancelDirectChallenge", "Cancel", new Color(0.24f, 0.22f, 0.26f, 1f), new Vector2(0f, -254f), new Vector2(340f, 62f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            directChallengeModal.SetActive(false);
        });

        RefreshDirectChallengeModalButtons();
        directChallengeModal.SetActive(false);
    }

    private void BuildIncomingDirectChallengeModal(Transform safeRoot)
    {
        incomingDirectChallengeModal = CreateStretchPanel(safeRoot, "IncomingDirectChallengeModal", new Color(0f, 0f, 0f, 0.80f));
        GameObject card = CreateCard(incomingDirectChallengeModal.transform, "IncomingDcCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(840f, 540f));

        CreateText(card.transform, "Title", "1v1 PLAYER CHALLENGE!", 34, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 194f), new Vector2(760f, 52f));
        incomingDirectChallengeBody = CreateText(card.transform, "Body", "A player challenged you to a 1v1 match!", 23, FontStyles.Normal, Color.white, new Vector2(0f, 54f), new Vector2(740f, 190f));

        CreatePillButton(card.transform, "BtnAcceptDirectChallenge", "Accept", RoatanVisualAssets.PalmGreen, new Vector2(0f, -104f), new Vector2(560f, 82f), 26, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            incomingDirectChallengeModal.SetActive(false);
            DirectChallengeDto ch = pendingDirectChallenge;
            pendingDirectChallenge = null;
            if (ch != null && EnsureConnectedForRequest())
            {
                subscribedClient.AddToSendQueue(MessageFrameBuilder.RespondDirectChallengeFrame(new RespondDirectChallengeDto
                {
                    ChallengeId = ch.ChallengeId,
                    Accept = true
                }));
            }
        });

        CreatePillButton(card.transform, "BtnIgnoreDirectChallenge", "Ignore", new Color(0.28f, 0.24f, 0.26f, 1f), new Vector2(0f, -200f), new Vector2(400f, 68f), 23, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            incomingDirectChallengeModal.SetActive(false);
            DirectChallengeDto ch = pendingDirectChallenge;
            pendingDirectChallenge = null;
            if (ch != null && EnsureConnectedForRequest())
            {
                subscribedClient.AddToSendQueue(MessageFrameBuilder.RespondDirectChallengeFrame(new RespondDirectChallengeDto
                {
                    ChallengeId = ch.ChallengeId,
                    Accept = false
                }));
            }
        });

        incomingDirectChallengeModal.SetActive(false);
    }

    private void BuildOAuthWaitingModal(Transform safeRoot)
    {
        oauthWaitingModal = CreateStretchPanel(safeRoot, "OAuthWaitingModal", new Color(0f, 0f, 0f, 0.78f));
        GameObject card = CreateCard(oauthWaitingModal.transform, "OAuthWaitCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(840f, 460f));
        CreateText(card.transform, "Title", "BROWSER SIGN-IN", 36, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 155f), new Vector2(760f, 54f));
        oauthWaitingLabel = CreateText(card.transform, "Body", "Complete sign-in in your browser...", 25, FontStyles.Normal, Color.white, new Vector2(0f, 20f), new Vector2(740f, 160f));
        CreatePillButton(card.transform, "BtnCancelOAuth", "Cancel", RoatanVisualAssets.SunsetCoral, new Vector2(0f, -135f), new Vector2(380f, 76f), 26, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            CancelPendingOAuthFlow();
        });
        oauthWaitingModal.SetActive(false);
    }

    private void BuildProfileModal(Transform safeRoot)
    {
        profileModal = CreateStretchPanel(safeRoot, "PlayerProfileModal", new Color(0f, 0f, 0f, 0.80f));
        GameObject card = CreateCard(profileModal.transform, "ProfileCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(880f, 1020f));

        CreateText(card.transform, "Title", "PLAYER PROFILE", 36, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-30f, 446f), new Vector2(680f, 54f));
        CreatePillButton(card.transform, "BtnCloseProfile", "X", new Color(0.24f, 0.22f, 0.26f, 1f), new Vector2(375f, 446f), new Vector2(68f, 60f), 28, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            profileModal.SetActive(false);
        });

        // Circular 164x164 avatar display
        GameObject avObj = new GameObject("ProfileAvatar");
        avObj.transform.SetParent(card.transform, false);
        RectTransform avRect = avObj.AddComponent<RectTransform>();
        avRect.anchoredPosition = new Vector2(0f, 300f);
        avRect.sizeDelta = new Vector2(164f, 164f);
        profileModalAvatarImg = avObj.AddComponent<Image>();
        profileModalAvatarInitials = CreateText(avObj.transform, "Init", "?", 58, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(140f, 100f));
        profileModalAvatarInitials.raycastTarget = false;

        profileModalNameLabel = CreateText(card.transform, "PName", "Player", 34, FontStyles.Bold, Color.white, new Vector2(0f, 180f), new Vector2(780f, 48f));
        profileModalEmailLabel = CreateText(card.transform, "PEmail", "player@example.com", 22, FontStyles.Normal, new Color(0.80f, 0.92f, 0.96f), new Vector2(0f, 132f), new Vector2(780f, 36f));

        GameObject badge = CreateCard(card.transform, "ProviderBadge", new Color(0.09f, 0.27f, 0.34f, 0.95f), new Vector2(0f, 76f), new Vector2(460f, 46f));
        profileModalProviderBadge = CreateText(badge.transform, "BadgeTxt", "Signed in with Email", 20, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(430f, 36f));

        GameObject statsCard = CreateCard(card.transform, "StatsCard", new Color(0.04f, 0.14f, 0.19f, 0.95f), new Vector2(0f, -8f), new Vector2(790f, 102f));
        profileModalStatsLabel = CreateText(statsCard.transform, "StatsTxt", "[Sailor] • 1000 Elo", 22, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(756f, 88f));

        profileModalStatusLabel = CreateText(card.transform, "StatusTxt", "", 20, FontStyles.Italic, RoatanVisualAssets.IslandGold, new Vector2(0f, -74f), new Vector2(780f, 36f));

        CreatePillButton(card.transform, "BtnChangePhoto", "Change Photo", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -148f), new Vector2(620f, 80f), 27, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            RoatanAndroidPhotoPicker.PickPhoto(
                b64 =>
                {
                    if (EnsureConnectedForRequest())
                    {
                        if (profileModalStatusLabel != null)
                        {
                            profileModalStatusLabel.text = "Uploading profile photo...";
                            profileModalStatusLabel.color = RoatanVisualAssets.IslandGold;
                        }
                        subscribedClient.AddToSendQueue(MessageFrameBuilder.UpdateProfileAvatarFrame(new UpdateProfileAvatarDto
                        {
                            AvatarBase64 = b64
                        }));
                    }
                },
                err =>
                {
                    if (profileModalStatusLabel != null)
                    {
                        profileModalStatusLabel.text = err;
                        profileModalStatusLabel.color = RoatanVisualAssets.SunsetCoral;
                    }
                    SetFeedback(err, RoatanVisualAssets.SunsetCoral);
                });
        });

        profileAdoptSocialBtn = CreatePillButton(card.transform, "BtnAdoptSocialPhoto", "Adopt Social Photo", RoatanVisualAssets.PalmGreen, new Vector2(0f, -244f), new Vector2(620f, 76f), 25, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            string url = subscribedClient?.User?.AvatarUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                StartCoroutine(AdoptSocialPhotoCoroutine(url, silent: false));
            }
        });
        profileAdoptSocialBtn.gameObject.SetActive(false);

        CreatePillButton(card.transform, "BtnDeleteAccount", "Delete Account", new Color(0.68f, 0.16f, 0.18f, 1f), new Vector2(0f, -356f), new Vector2(540f, 76f), 26, OpenDeleteAccountModal);

        CreatePillButton(card.transform, "BtnProfileDone", "Close", new Color(0.20f, 0.24f, 0.28f, 1f), new Vector2(0f, -448f), new Vector2(360f, 68f), 24, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            profileModal.SetActive(false);
        });

        profileModal.SetActive(false);
    }

    private void BuildDeleteAccountModal(Transform safeRoot)
    {
        deleteAccountModal = CreateStretchPanel(safeRoot, "DeleteAccountModal", new Color(0f, 0f, 0f, 0.84f));
        GameObject card = CreateCard(deleteAccountModal.transform, "DeleteCard", RoatanVisualAssets.DarkSlate, Vector2.zero, new Vector2(860f, 680f));

        CreateText(card.transform, "Title", "DELETE ACCOUNT?", 38, FontStyles.Bold, RoatanVisualAssets.SunsetCoral, new Vector2(0f, 265f), new Vector2(780f, 56f));
        CreateText(card.transform, "WarnBody", "This will permanently delete your account, profile photo, and Elo history, and remove you from all tables.\n\nType DELETE or tap Confirm to permanently delete your account:", 23, FontStyles.Normal, Color.white, new Vector2(0f, 130f), new Vector2(780f, 175f));

        deleteConfirmInput = CreateInputField(card.transform, "DeleteConfirmInput", "Type DELETE to confirm", "", new Vector2(0f, -2f), new Vector2(680f, 72f));
        deleteConfirmErrorLabel = CreateText(card.transform, "DelErr", "", 20, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, -72f), new Vector2(760f, 44f));

        Button confirmBtn = CreatePillButton(card.transform, "BtnConfirmDeleteAccount", "Confirm Delete", new Color(0.74f, 0.16f, 0.18f, 1f), new Vector2(0f, -162f), new Vector2(620f, 86f), 27, SubmitDeleteAccountConfirmed);
        deleteConfirmBtnLabel = confirmBtn.GetComponentInChildren<TMP_Text>();

        CreatePillButton(card.transform, "BtnCancelDeleteAccount", "Cancel", new Color(0.22f, 0.26f, 0.30f, 1f), new Vector2(0f, -264f), new Vector2(380f, 72f), 25, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            deleteConfirmSecondTapArmed = false;
            deleteAccountModal.SetActive(false);
        });

        deleteAccountModal.SetActive(false);
    }

    // ==================== HELPERS ====================

    private void ShowSubView(GameObject target)
    {
        authView.SetActive(target == authView);
        hubView.SetActive(target == hubView);
        lobbyView.SetActive(target == lobbyView);
        onlineGameView.SetActive(target == onlineGameView);
        if (target != authView)
        {
            SetServerSettingsExpanded(false);
        }
    }

    private void SelectAuthTab(bool isSignIn)
    {
        ClearAuthError();
        signInPanel.SetActive(isSignIn);
        registerPanel.SetActive(!isSignIn);
        signInTabImg.color = isSignIn ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f);
        registerTabImg.color = !isSignIn ? RoatanVisualAssets.BrightLagoon : new Color(0.14f, 0.22f, 0.28f, 1f);
    }

    private void ToggleLoginPasswordVisibility()
    {
        RoatanSoundFX.Instance?.PlayTap();
        loginPassVisible = !loginPassVisible;
        ApplyPasswordFieldVisibility(loginPassInput, loginShowPassLabel, loginPassVisible);
    }

    private void ToggleRegisterPasswordVisibility()
    {
        RoatanSoundFX.Instance?.PlayTap();
        regPassVisible = !regPassVisible;
        ApplyPasswordFieldVisibility(regPassInput, regShowPassLabel, regPassVisible);
    }

    private static void ApplyPasswordFieldVisibility(TMP_InputField field, TMP_Text toggleLabel, bool visible)
    {
        if (field != null)
        {
            field.contentType = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            field.ForceLabelUpdate();
        }
        if (toggleLabel != null)
        {
            toggleLabel.text = visible ? "Hide" : "Show";
        }
    }

    private void UpdatePasswordChecklist(string password)
    {
        string value = password ?? string.Empty;
        bool hasLen = value.Length >= 8;
        bool hasCap = HasCapitalRegex.IsMatch(value);
        bool hasNumSym = HasNumberOrSymbolRegex.IsMatch(value);

        Color okColor = new Color(0.32f, 0.90f, 0.56f, 1f);
        Color pendingColor = new Color(0.70f, 0.80f, 0.84f, 1f);

        if (passRuleLenLabel != null)
        {
            passRuleLenLabel.text = hasLen ? "✓ 8+ chars" : "○ 8+ chars";
            passRuleLenLabel.color = hasLen ? okColor : pendingColor;
        }
        if (passRuleCapLabel != null)
        {
            passRuleCapLabel.text = hasCap ? "✓ 1 Capital (A-Z)" : "○ 1 Capital (A-Z)";
            passRuleCapLabel.color = hasCap ? okColor : pendingColor;
        }
        if (passRuleNumSymLabel != null)
        {
            passRuleNumSymLabel.text = hasNumSym ? "✓ 1 Number / !@#$%" : "○ 1 Number / !@#$%";
            passRuleNumSymLabel.color = hasNumSym ? okColor : pendingColor;
        }
    }

    private void ShowAuthError(string message)
    {
        if (authErrorBannerObj == null || authErrorBannerText == null) return;
        authErrorBannerText.text = message;
        authErrorBannerObj.SetActive(!string.IsNullOrWhiteSpace(message));
    }

    private void ClearAuthError()
    {
        if (authErrorBannerObj != null) authErrorBannerObj.SetActive(false);
    }

    private static string FormatFriendlyError(ResponseDto dto, string fallback)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.ErrorMessage))
        {
            return dto.ErrorMessage;
        }
        if (dto == null || !dto.ErrorType.HasValue)
        {
            return fallback;
        }

        switch (dto.ErrorType.Value)
        {
            case ErrorTypes.InvalidPasswordFormat:
                return "Password must be at least 8 characters and include at least one capital letter (A-Z) and one number or special character (!@#$%).";
            case ErrorTypes.InvalidEmailFormat:
                return "Please enter a valid email address.";
            case ErrorTypes.LoginTooLong:
                return "Username must be 3–16 letters or numbers.";
            case ErrorTypes.InvalidDateFormat:
                return "Please enter a valid date of birth (MM/DD/YYYY).";
            case ErrorTypes.UserAlreadyExist:
                return "An account with that email or username already exists.";
            case ErrorTypes.UserNotFound:
                return "No account found with that username or email.";
            case ErrorTypes.WrongLoggingOrPassword:
                return "Incorrect username or password. Please try again.";
            case ErrorTypes.UserAlreadyLoggedIn:
                return "This account is already signed in on another device.";
            case ErrorTypes.LobbyFull:
                return "This table is full.";
            case ErrorTypes.LobbyNotFound:
                return "Table not found. Check the code and try again.";
            case ErrorTypes.AlreadyInALobby:
                return "You are already seated at a table.";
            case ErrorTypes.CreationLobbyFailed:
                return "You already host an active table.";
            default:
                return fallback;
        }
    }

    private void SelectHubBrowserTab(HubBrowserTab tab)
    {
        activeHubTab = tab;
        if (hubTablesScrollObj != null) hubTablesScrollObj.SetActive(tab == HubBrowserTab.Tables);
        if (hubPlayersScrollObj != null) hubPlayersScrollObj.SetActive(tab == HubBrowserTab.Players);
        if (hubLeaderboardViewObj != null) hubLeaderboardViewObj.SetActive(tab == HubBrowserTab.Leaderboard);
        if (hubTeamsScrollObj != null) hubTeamsScrollObj.SetActive(tab == HubBrowserTab.Teams);
        if (hubChatViewObj != null) hubChatViewObj.SetActive(tab == HubBrowserTab.Chat);

        Color inactive = new Color(0.14f, 0.22f, 0.28f, 1f);
        if (hubTablesTabImg != null) hubTablesTabImg.color = tab == HubBrowserTab.Tables ? RoatanVisualAssets.BrightLagoon : inactive;
        if (hubPlayersTabImg != null) hubPlayersTabImg.color = tab == HubBrowserTab.Players ? RoatanVisualAssets.BrightLagoon : inactive;
        if (hubLeaderboardTabImg != null) hubLeaderboardTabImg.color = tab == HubBrowserTab.Leaderboard ? RoatanVisualAssets.IslandGold : inactive;
        if (hubTeamsTabImg != null) hubTeamsTabImg.color = tab == HubBrowserTab.Teams ? RoatanVisualAssets.PalmGreen : inactive;
        if (hubChatTabImg != null) hubChatTabImg.color = tab == HubBrowserTab.Chat ? RoatanVisualAssets.BrightLagoon : inactive;
        if (hubLeaderboardTabLabel != null)
        {
            hubLeaderboardTabLabel.color = tab == HubBrowserTab.Leaderboard ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
        }

        if (tab == HubBrowserTab.Players)
        {
            RefreshPlayersRosterUI();
        }
        else if (tab == HubBrowserTab.Leaderboard)
        {
            RefreshLeaderboardUI();
            RefreshTeamLeaderboardUI();
            if (isSignedIn && Networking.Instance != null && Networking.Instance.IsConnected)
            {
                RequestLeaderboard();
                RequestTeamLeaderboard();
                RequestMyTeam();
            }
        }
        else if (tab == HubBrowserTab.Teams)
        {
            RefreshTeamsTabUI();
            if (isSignedIn && Networking.Instance != null && Networking.Instance.IsConnected)
            {
                RequestMyTeam();
                RequestTeamsList();
                RequestOpenTeamChallenges();
            }
        }
        else if (tab == HubBrowserTab.Chat)
        {
            if (myTeamDetails?.TeamInfo == null && !chatShowingDirectMessages)
            {
                chatShowingDirectMessages = true;
            }
            ClearCurrentChatChannelUnread();
            RefreshChatTabUI();
            if (isSignedIn && Networking.Instance != null && Networking.Instance.IsConnected)
            {
                RequestChatHistories();
            }
        }
    }

    // ==================== TEAMS / CREWS HUB & ROSTER MANAGEMENT ====================

    private void OnGetTeamsResponse(ResponseObjectDto<TeamsListDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null) return;
        latestTeamsList = dto.Object;
        if (myTeamDetails == null)
        {
            myTeamDetails = new MyTeamDetailsDto
            {
                PendingInvites = dto.Object.PendingInvites ?? new List<TeamInviteDto>()
            };
        }
        else if (myTeamDetails.TeamInfo == null && dto.Object.PendingInvites != null)
        {
            myTeamDetails.PendingInvites = dto.Object.PendingInvites;
        }
        UpdateTeamsTabBadge();
        if (activeHubTab == HubBrowserTab.Teams)
        {
            RefreshTeamsTabUI();
        }
    }

    private void OnTeamMutationOrDetailsResponse(ResponseObjectDto<MyTeamDetailsDto> dto)
    {
        if (dto == null) return;
        if (!dto.OkResult)
        {
            string err = FormatFriendlyError(dto, "Team action failed.");
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            return;
        }

        if (dto.Object != null)
        {
            myTeamDetails = dto.Object;
            if (myTeamDetails.TeamInfo != null && editTeamDraftTeamId != myTeamDetails.TeamInfo.Id)
            {
                SyncEditTeamDraftFromTeamInfo(myTeamDetails.TeamInfo);
            }
            else if (myTeamDetails.TeamInfo == null)
            {
                editTeamDraftTeamId = -1;
            }

            if (!string.IsNullOrWhiteSpace(myTeamDetails.StatusMessage))
            {
                SetFeedback(myTeamDetails.StatusMessage, RoatanVisualAssets.PalmGreen);
            }
        }

        UpdateTeamsTabBadge();
        if (activeHubTab == HubBrowserTab.Teams)
        {
            RefreshTeamsTabUI();
        }
        RefreshTeamLeaderboardUI();
        if (myTeamDetails?.TeamInfo == null && isSignedIn && Networking.Instance != null && Networking.Instance.IsConnected)
        {
            RequestTeamsList();
        }
    }

    private void UpdateTeamsTabBadge()
    {
        if (hubTeamsTabLabel == null) return;
        if (myTeamDetails?.TeamInfo != null)
        {
            int reqCount = myTeamDetails.PendingRequests != null ? myTeamDetails.PendingRequests.Count : 0;
            hubTeamsTabLabel.text = reqCount > 0 ? $"Teams ({reqCount})" : $"[{myTeamDetails.TeamInfo.Tag}]";
        }
        else
        {
            int invCount = GetPendingTeamInvites().Count;
            hubTeamsTabLabel.text = invCount > 0 ? $"Teams ({invCount})" : "Teams";
        }
    }

    private List<TeamInviteDto> GetPendingTeamInvites()
    {
        if (myTeamDetails?.PendingInvites != null && myTeamDetails.PendingInvites.Count > 0)
        {
            return myTeamDetails.PendingInvites;
        }
        if (latestTeamsList?.PendingInvites != null)
        {
            return latestTeamsList.PendingInvites;
        }
        return new List<TeamInviteDto>();
    }

    private void SyncEditTeamDraftFromTeamInfo(TeamInfoDto info)
    {
        if (info == null) return;
        editTeamDraftTeamId = info.Id;
        editTeamNameDraft = info.Name ?? string.Empty;
        editTeamTagDraft = info.Tag ?? string.Empty;
        editTeamJoinPolicy = info.JoinPolicy;
        editTeamActivityReq = info.ActivityRequirement;
        editTeamMinEloIndex = FindClosestMinEloOptionIndex(info.MinElo);
    }

    private static int FindClosestMinEloOptionIndex(int minElo)
    {
        int bestIdx = 0;
        for (int i = 0; i < TeamMinEloOptions.Length; i++)
        {
            if (TeamMinEloOptions[i] <= minElo) bestIdx = i;
        }
        return bestIdx;
    }

    private static string FormatMinEloOptionLabel(int minElo)
    {
        if (minElo <= RoatanRankTierHelper.MinElo)
        {
            return "Any Elo (100+)";
        }
        string tierName = RoatanRankTierHelper.FromElo(minElo).GetDisplayName();
        return $"{tierName} ({minElo}+)";
    }

    private void RefreshTeamsTabUI()
    {
        if (teamsContentRoot == null) return;
        foreach (GameObject go in spawnedTeamSectionObjs) Destroy(go);
        spawnedTeamSectionObjs.Clear();

        if (myTeamDetails?.TeamInfo != null)
        {
            BuildMyTeamDashboardUI(myTeamDetails);
        }
        else
        {
            BuildNoTeamHubUI();
        }
    }

    private void BuildNoTeamHubUI()
    {
        // 1. Pending Team Invites Banner (Accept / Decline)
        List<TeamInviteDto> invites = GetPendingTeamInvites();
        if (invites.Count > 0)
        {
            GameObject invHdr = CreateCard(teamsContentRoot, "PendingInvitesHdr", new Color(0.11f, 0.30f, 0.34f, 0.96f), Vector2.zero, new Vector2(860f, 44f));
            invHdr.AddComponent<LayoutElement>().preferredHeight = 44f;
            CreateText(invHdr.transform, "Title", $"PENDING TEAM INVITES ({invites.Count})", 20, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(820f, 34f)).alignment = TextAlignmentOptions.MidlineLeft;
            spawnedTeamSectionObjs.Add(invHdr);

            foreach (TeamInviteDto invite in invites)
            {
                TeamInviteDto capturedInv = invite;
                GameObject invRow = CreateCard(teamsContentRoot, $"TeamInvite_{invite.InviteId}", new Color(0.08f, 0.26f, 0.30f, 0.98f), Vector2.zero, new Vector2(860f, 108f));
                invRow.AddComponent<LayoutElement>().preferredHeight = 108f;

                string teamBadge = RoatanRankTierHelper.FormatRichBadge(invite.TeamElo);
                CreateText(invRow.transform, "InvTeam", $"[{invite.TeamTag}] {invite.TeamName}  <size=17>{teamBadge}</size>", 21, FontStyles.Bold, Color.white, new Vector2(-135f, 20f), new Vector2(540f, 36f)).alignment = TextAlignmentOptions.MidlineLeft;
                CreateText(invRow.transform, "InvBy", $"Invited by {invite.InvitedByUsername}", 18, FontStyles.Italic, new Color(0.78f, 0.92f, 0.96f), new Vector2(-135f, -20f), new Vector2(540f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;

                CreatePillButton(invRow.transform, "BtnAcceptTeamInvite", "Accept", RoatanVisualAssets.PalmGreen, new Vector2(214f, 0f), new Vector2(138f, 60f), 20, () =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    if (!EnsureConnectedForRequest()) return;
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.RespondTeamInviteFrame(new RespondTeamInviteDto
                    {
                        InviteId = capturedInv.InviteId,
                        Accept = true
                    }));
                });

                CreatePillButton(invRow.transform, "BtnDeclineTeamInvite", "Decline", RoatanVisualAssets.SunsetCoral, new Vector2(358f, 0f), new Vector2(132f, 60f), 19, () =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    if (!EnsureConnectedForRequest()) return;
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.RespondTeamInviteFrame(new RespondTeamInviteDto
                    {
                        InviteId = capturedInv.InviteId,
                        Accept = false
                    }));
                });

                spawnedTeamSectionObjs.Add(invRow);
            }
        }

        // 2. Create Team Form Card
        GameObject createCard = CreateCard(teamsContentRoot, "CreateTeamCard", new Color(0.07f, 0.21f, 0.27f, 0.96f), Vector2.zero, new Vector2(860f, 332f));
        createCard.AddComponent<LayoutElement>().preferredHeight = 332f;

        CreateText(createCard.transform, "CreateTeamHdr", "CREATE A TEAM / CREW", 22, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 134f), new Vector2(820f, 34f)).alignment = TextAlignmentOptions.MidlineLeft;

        TMP_InputField nameInput = CreateInputField(createCard.transform, "CreateTeamName", "Team Name (3-24 chars)", createTeamNameDraft, new Vector2(-120f, 76f), new Vector2(570f, 58f));
        nameInput.onValueChanged.AddListener(v => createTeamNameDraft = v);

        TMP_InputField tagInput = CreateInputField(createCard.transform, "CreateTeamTag", "TAG (3-4)", createTeamTagDraft, new Vector2(292f, 76f), new Vector2(236f, 58f));
        tagInput.characterLimit = 4;
        tagInput.onValueChanged.AddListener(v => createTeamTagDraft = v);

        // Join Policy toggle pills (Open vs Approval Required)
        Color activeBlue = RoatanVisualAssets.BrightLagoon;
        Color inactiveDark = new Color(0.14f, 0.22f, 0.28f, 1f);
        CreatePillButton(createCard.transform, "BtnPolicyOpen", "Open Join", createTeamJoinPolicy == TeamJoinPolicy.Open ? activeBlue : inactiveDark, new Vector2(-210f, 6f), new Vector2(400f, 54f), 20, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            createTeamJoinPolicy = TeamJoinPolicy.Open;
            RefreshTeamsTabUI();
        });
        CreatePillButton(createCard.transform, "BtnPolicyApproval", "Approval Required", createTeamJoinPolicy == TeamJoinPolicy.ApprovalRequired ? activeBlue : inactiveDark, new Vector2(210f, 6f), new Vector2(400f, 54f), 20, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            createTeamJoinPolicy = TeamJoinPolicy.ApprovalRequired;
            RefreshTeamsTabUI();
        });

        // Min Elo & Activity Requirement selector dropdown buttons
        int selectedMinElo = TeamMinEloOptions[Mathf.Clamp(createTeamMinEloIndex, 0, TeamMinEloOptions.Length - 1)];
        CreatePillButton(createCard.transform, "BtnCreateMinElo", $"Min Elo: {FormatMinEloOptionLabel(selectedMinElo)}  ▾", new Color(0.10f, 0.28f, 0.35f, 1f), new Vector2(-210f, -60f), new Vector2(400f, 54f), 18, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            createTeamMinEloIndex = (createTeamMinEloIndex + 1) % TeamMinEloOptions.Length;
            RefreshTeamsTabUI();
        });
        CreatePillButton(createCard.transform, "BtnCreateActivity", $"Activity: {createTeamActivityReq.GetDisplayName()}  ▾", new Color(0.10f, 0.28f, 0.35f, 1f), new Vector2(210f, -60f), new Vector2(400f, 54f), 18, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            createTeamActivityReq = (TeamActivityRequirement)(((int)createTeamActivityReq + 1) % 3);
            RefreshTeamsTabUI();
        });

        CreatePillButton(createCard.transform, "BtnSubmitCreateTeam", "Create Team (Starts at 1000 Elo)", RoatanVisualAssets.PalmGreen, new Vector2(0f, -128f), new Vector2(620f, 60f), 22, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            if (!EnsureConnectedForRequest()) return;
            subscribedClient.AddToSendQueue(MessageFrameBuilder.CreateTeamFrame(new CreateTeamDto
            {
                Name = createTeamNameDraft.Trim(),
                Tag = createTeamTagDraft.Trim().ToUpperInvariant(),
                JoinPolicy = createTeamJoinPolicy,
                MinElo = TeamMinEloOptions[Mathf.Clamp(createTeamMinEloIndex, 0, TeamMinEloOptions.Length - 1)],
                ActivityRequirement = createTeamActivityReq
            }));
        });
        spawnedTeamSectionObjs.Add(createCard);

        // 3. Browse Teams Search Bar + List
        GameObject searchCard = CreateCard(teamsContentRoot, "BrowseTeamsSearch", new Color(0.06f, 0.19f, 0.25f, 0.94f), Vector2.zero, new Vector2(860f, 108f));
        searchCard.AddComponent<LayoutElement>().preferredHeight = 108f;
        CreateText(searchCard.transform, "BrowseHdr", "BROWSE TEAMS", 20, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 32f), new Vector2(820f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;
        TMP_InputField searchInput = CreateInputField(searchCard.transform, "TeamSearchInput", "Search teams by name or tag...", teamSearchQuery, new Vector2(-105f, -16f), new Vector2(610f, 52f));
        searchInput.onValueChanged.AddListener(v => teamSearchQuery = v);
        CreatePillButton(searchCard.transform, "BtnSearchTeams", "Search", RoatanVisualAssets.BrightLagoon, new Vector2(310f, -16f), new Vector2(196f, 52f), 20, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            RequestTeamsList(teamSearchQuery);
        });
        spawnedTeamSectionObjs.Add(searchCard);

        List<TeamInfoDto> teams = latestTeamsList?.Teams;
        if (teams == null || teams.Count == 0)
        {
            GameObject emptyCard = CreateCard(teamsContentRoot, "NoTeamsRow", new Color(0.07f, 0.19f, 0.25f, 0.72f), Vector2.zero, new Vector2(860f, 78f));
            emptyCard.AddComponent<LayoutElement>().preferredHeight = 78f;
            CreateText(emptyCard.transform, "EmptyLbl", "No teams found yet — create the first crew above!", 20, FontStyles.Italic, new Color(0.82f, 0.92f, 0.95f), Vector2.zero, new Vector2(820f, 50f));
            spawnedTeamSectionObjs.Add(emptyCard);
            return;
        }

        foreach (TeamInfoDto team in teams)
        {
            TeamInfoDto capturedTeam = team;
            bool canJoin = team.MeetsRequirements;
            float rowHeight = canJoin ? 132f : 156f;
            GameObject row = CreateCard(teamsContentRoot, $"TeamRow_{team.Id}", new Color(0.07f, 0.22f, 0.28f, 0.96f), Vector2.zero, new Vector2(860f, rowHeight));
            row.AddComponent<LayoutElement>().preferredHeight = rowHeight;

            string tierBadge = RoatanRankTierHelper.FormatRichBadge(team.TeamElo);
            float topY = rowHeight * 0.5f - 28f;
            CreateText(row.transform, "TName", $"[{team.Tag}] {team.Name}  <size=18>{tierBadge}</size>", 22, FontStyles.Bold, Color.white, new Vector2(-120f, topY), new Vector2(580f, 34f)).alignment = TextAlignmentOptions.MidlineLeft;
            CreateText(row.transform, "TMeta", $"Capt: {team.CaptainUsername}  •  {team.MemberCount}/{team.MaxMembers}  •  W/L: {team.TeamWins}-{team.TeamLosses}  •  Squad Avg: {team.AverageMemberElo}", 16, FontStyles.Normal, new Color(0.84f, 0.94f, 0.97f), new Vector2(-120f, topY - 34f), new Vector2(580f, 28f)).alignment = TextAlignmentOptions.MidlineLeft;
            CreateText(row.transform, "TReqs", $"{team.JoinPolicy.GetDisplayName()}  •  Min: {FormatMinEloOptionLabel(team.MinElo)}  •  {team.ActivityRequirement.GetDisplayName()}", 16, FontStyles.Italic, RoatanVisualAssets.IslandGold, new Vector2(-120f, topY - 64f), new Vector2(580f, 26f)).alignment = TextAlignmentOptions.MidlineLeft;

            if (!canJoin && !string.IsNullOrWhiteSpace(team.DisqualifyReason))
            {
                CreateText(row.transform, "TDisq", $"✗ {team.DisqualifyReason}", 16, FontStyles.Bold, RoatanVisualAssets.SunsetCoral, new Vector2(-120f, topY - 94f), new Vector2(580f, 28f)).alignment = TextAlignmentOptions.MidlineLeft;
            }

            string actionLabel = team.JoinPolicy == TeamJoinPolicy.ApprovalRequired ? "Request Join" : "Join";
            Color btnColor = canJoin
                ? (team.JoinPolicy == TeamJoinPolicy.ApprovalRequired ? RoatanVisualAssets.BrightLagoon : RoatanVisualAssets.PalmGreen)
                : new Color(0.28f, 0.24f, 0.26f, 0.90f);
            Button joinBtn = CreatePillButton(row.transform, "BtnJoinTeam", actionLabel, btnColor, new Vector2(310f, 0f), new Vector2(198f, 64f), 19, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                if (!EnsureConnectedForRequest()) return;
                subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinOrRequestTeamFrame(new JoinOrRequestTeamDto
                {
                    TeamId = capturedTeam.Id
                }));
            });
            joinBtn.interactable = canJoin;

            spawnedTeamSectionObjs.Add(row);
        }
    }

    private void BuildMyTeamDashboardUI(MyTeamDetailsDto details)
    {
        TeamInfoDto info = details.TeamInfo;
        TeamRole myRole = details.MyRole ?? TeamRole.Teammate;
        bool isCaptain = myRole == TeamRole.Captain;
        bool isCaptainOrVice = myRole == TeamRole.Captain || myRole == TeamRole.ViceCaptain;
        long selfId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;

        if (editTeamDraftTeamId != info.Id)
        {
            SyncEditTeamDraftFromTeamInfo(info);
        }

        // 1. My Team Header Card
        GameObject hdrCard = CreateCard(teamsContentRoot, "MyTeamHeader", new Color(0.08f, 0.27f, 0.32f, 0.98f), Vector2.zero, new Vector2(860f, 168f));
        hdrCard.AddComponent<LayoutElement>().preferredHeight = 168f;

        string teamBadge = RoatanRankTierHelper.FormatRichBadge(info.TeamElo);
        string roleHex = myRole.GetBadgeColorHex();
        CreateText(hdrCard.transform, "MyTeamTitle", $"[{info.Tag}] {info.Name}", 28, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-115f, 48f), new Vector2(590f, 40f)).alignment = TextAlignmentOptions.MidlineLeft;
        CreateText(hdrCard.transform, "MyTeamElo", $"{teamBadge} Team  •  Squad Avg: {info.AverageMemberElo}  •  W/L: {info.TeamWins}-{info.TeamLosses}  •  {info.MemberCount}/{info.MaxMembers}", 17, FontStyles.Bold, Color.white, new Vector2(-115f, 8f), new Vector2(590f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;
        CreateText(hdrCard.transform, "MyTeamReqs", $"Entry: {info.JoinPolicy.GetDisplayName()}  •  {FormatMinEloOptionLabel(info.MinElo)}  •  {info.ActivityRequirement.GetDisplayName()}", 16, FontStyles.Normal, new Color(0.80f, 0.93f, 0.96f), new Vector2(-115f, -26f), new Vector2(590f, 26f)).alignment = TextAlignmentOptions.MidlineLeft;
        CreateText(hdrCard.transform, "MyTeamRole", $"Your Role: <color={roleHex}><b>{myRole.GetDisplayName()}</b></color>", 18, FontStyles.Bold, Color.white, new Vector2(-115f, -56f), new Vector2(590f, 28f)).alignment = TextAlignmentOptions.MidlineLeft;

        CreatePillButton(hdrCard.transform, "BtnLeaveTeam", "Leave Team", RoatanVisualAssets.SunsetCoral, new Vector2(316f, 0f), new Vector2(186f, 62f), 19, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            if (!EnsureConnectedForRequest()) return;
            subscribedClient.AddToSendQueue(MessageFrameBuilder.LeaveTeamFrame());
        });
        spawnedTeamSectionObjs.Add(hdrCard);

        // 2. Invite Player Card (available to Captain, Vice-Captain, AND Teammates)
        GameObject inviteCard = CreateCard(teamsContentRoot, "TeamInviteCard", new Color(0.06f, 0.20f, 0.26f, 0.95f), Vector2.zero, new Vector2(860f, 108f));
        inviteCard.AddComponent<LayoutElement>().preferredHeight = 108f;
        CreateText(inviteCard.transform, "InvHdr", "INVITE PLAYER TO TEAM", 19, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 32f), new Vector2(820f, 28f)).alignment = TextAlignmentOptions.MidlineLeft;
        TMP_InputField invInput = CreateInputField(inviteCard.transform, "TeamInviteUserInput", "Enter player username to invite...", teamInviteUsernameDraft, new Vector2(-108f, -16f), new Vector2(604f, 52f));
        invInput.onValueChanged.AddListener(v => teamInviteUsernameDraft = v);
        CreatePillButton(inviteCard.transform, "BtnSendTeamInvite", "Send Invite", RoatanVisualAssets.PalmGreen, new Vector2(308f, -16f), new Vector2(200f, 52f), 19, () =>
        {
            RoatanSoundFX.Instance?.PlayTap();
            if (!EnsureConnectedForRequest()) return;
            if (string.IsNullOrWhiteSpace(teamInviteUsernameDraft))
            {
                SetFeedback("Enter a player's username to send a team invite.", RoatanVisualAssets.SunsetCoral);
                return;
            }
            string targetUsername = teamInviteUsernameDraft.Trim();
            teamInviteUsernameDraft = string.Empty;
            subscribedClient.AddToSendQueue(MessageFrameBuilder.InviteToTeamFrame(new InviteToTeamDto
            {
                TargetUsername = targetUsername
            }));
        });
        spawnedTeamSectionObjs.Add(inviteCard);

        // 3. Pending Join Requests (shown ONLY to Captain & Vice-Captain)
        if (isCaptainOrVice)
        {
            List<TeamJoinRequestDto> requests = details.PendingRequests ?? new List<TeamJoinRequestDto>();
            GameObject reqHdr = CreateCard(teamsContentRoot, "JoinReqHdr", new Color(0.09f, 0.24f, 0.30f, 0.95f), Vector2.zero, new Vector2(860f, 42f));
            reqHdr.AddComponent<LayoutElement>().preferredHeight = 42f;
            CreateText(reqHdr.transform, "Title", $"PENDING JOIN REQUESTS ({requests.Count})", 19, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(820f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;
            spawnedTeamSectionObjs.Add(reqHdr);

            if (requests.Count == 0)
            {
                GameObject noneReq = CreateCard(teamsContentRoot, "NoJoinReqs", new Color(0.06f, 0.17f, 0.22f, 0.72f), Vector2.zero, new Vector2(860f, 54f));
                noneReq.AddComponent<LayoutElement>().preferredHeight = 54f;
                CreateText(noneReq.transform, "Lbl", "No pending join requests right now.", 18, FontStyles.Italic, new Color(0.76f, 0.88f, 0.92f), Vector2.zero, new Vector2(820f, 34f));
                spawnedTeamSectionObjs.Add(noneReq);
            }
            else
            {
                foreach (TeamJoinRequestDto req in requests)
                {
                    TeamJoinRequestDto capturedReq = req;
                    GameObject reqRow = CreateCard(teamsContentRoot, $"JoinReq_{req.RequestId}", new Color(0.07f, 0.22f, 0.28f, 0.96f), Vector2.zero, new Vector2(860f, 88f));
                    reqRow.AddComponent<LayoutElement>().preferredHeight = 88f;

                    GameObject avObj = new GameObject("ReqAvatar");
                    avObj.transform.SetParent(reqRow.transform, false);
                    RectTransform avRect = avObj.AddComponent<RectTransform>();
                    avRect.anchoredPosition = new Vector2(-382f, 0f);
                    avRect.sizeDelta = new Vector2(54f, 54f);
                    Image avImg = avObj.AddComponent<Image>();
                    avImg.raycastTarget = false;
                    TMP_Text avInit = CreateText(avObj.transform, "Init", "?", 19, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(46f, 36f));
                    avInit.raycastTarget = false;
                    ApplyCircularAvatar(avImg, avInit, $"treq_{req.UserId}", req.AvatarBase64, req.Username);

                    string reqBadge = RoatanRankTierHelper.FormatRichBadge(req.Elo);
                    CreateText(reqRow.transform, "ReqName", $"{req.Username}  <size=17>{reqBadge}</size>", 20, FontStyles.Bold, Color.white, new Vector2(-110f, 0f), new Vector2(460f, 44f)).alignment = TextAlignmentOptions.MidlineLeft;

                    CreatePillButton(reqRow.transform, "BtnApproveReq", "Approve", RoatanVisualAssets.PalmGreen, new Vector2(216f, 0f), new Vector2(138f, 54f), 19, () =>
                    {
                        RoatanSoundFX.Instance?.PlayTap();
                        if (!EnsureConnectedForRequest()) return;
                        subscribedClient.AddToSendQueue(MessageFrameBuilder.RespondTeamJoinRequestFrame(new RespondTeamJoinRequestDto
                        {
                            RequestId = capturedReq.RequestId,
                            Approve = true
                        }));
                    });

                    CreatePillButton(reqRow.transform, "BtnDenyReq", "Deny", RoatanVisualAssets.SunsetCoral, new Vector2(360f, 0f), new Vector2(124f, 54f), 19, () =>
                    {
                        RoatanSoundFX.Instance?.PlayTap();
                        if (!EnsureConnectedForRequest()) return;
                        subscribedClient.AddToSendQueue(MessageFrameBuilder.RespondTeamJoinRequestFrame(new RespondTeamJoinRequestDto
                        {
                            RequestId = capturedReq.RequestId,
                            Approve = false
                        }));
                    });

                    spawnedTeamSectionObjs.Add(reqRow);
                }
            }
        }

        // 4. Team Settings Edit Panel (shown ONLY to Captain)
        if (isCaptain)
        {
            GameObject settingsCard = CreateCard(teamsContentRoot, "TeamSettingsCard", new Color(0.07f, 0.21f, 0.27f, 0.96f), Vector2.zero, new Vector2(860f, 326f));
            settingsCard.AddComponent<LayoutElement>().preferredHeight = 326f;

            CreateText(settingsCard.transform, "SettingsHdr", "TEAM SETTINGS (CAPTAIN ONLY)", 20, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 132f), new Vector2(820f, 32f)).alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField editName = CreateInputField(settingsCard.transform, "EditTeamName", "Team Name", editTeamNameDraft, new Vector2(-120f, 74f), new Vector2(570f, 56f));
            editName.onValueChanged.AddListener(v => editTeamNameDraft = v);

            TMP_InputField editTag = CreateInputField(settingsCard.transform, "EditTeamTag", "TAG", editTeamTagDraft, new Vector2(292f, 74f), new Vector2(236f, 56f));
            editTag.characterLimit = 4;
            editTag.onValueChanged.AddListener(v => editTeamTagDraft = v);

            Color activeBlue = RoatanVisualAssets.BrightLagoon;
            Color inactiveDark = new Color(0.14f, 0.22f, 0.28f, 1f);
            CreatePillButton(settingsCard.transform, "BtnEditPolicyOpen", "Open Join", editTeamJoinPolicy == TeamJoinPolicy.Open ? activeBlue : inactiveDark, new Vector2(-210f, 6f), new Vector2(400f, 52f), 19, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                editTeamJoinPolicy = TeamJoinPolicy.Open;
                RefreshTeamsTabUI();
            });
            CreatePillButton(settingsCard.transform, "BtnEditPolicyApproval", "Approval Required", editTeamJoinPolicy == TeamJoinPolicy.ApprovalRequired ? activeBlue : inactiveDark, new Vector2(210f, 6f), new Vector2(400f, 52f), 19, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                editTeamJoinPolicy = TeamJoinPolicy.ApprovalRequired;
                RefreshTeamsTabUI();
            });

            int editMinElo = TeamMinEloOptions[Mathf.Clamp(editTeamMinEloIndex, 0, TeamMinEloOptions.Length - 1)];
            CreatePillButton(settingsCard.transform, "BtnEditMinElo", $"Min Elo: {FormatMinEloOptionLabel(editMinElo)}  ▾", new Color(0.10f, 0.28f, 0.35f, 1f), new Vector2(-210f, -58f), new Vector2(400f, 52f), 18, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                editTeamMinEloIndex = (editTeamMinEloIndex + 1) % TeamMinEloOptions.Length;
                RefreshTeamsTabUI();
            });
            CreatePillButton(settingsCard.transform, "BtnEditActivity", $"Activity: {editTeamActivityReq.GetDisplayName()}  ▾", new Color(0.10f, 0.28f, 0.35f, 1f), new Vector2(210f, -58f), new Vector2(400f, 52f), 18, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                editTeamActivityReq = (TeamActivityRequirement)(((int)editTeamActivityReq + 1) % 3);
                RefreshTeamsTabUI();
            });

            CreatePillButton(settingsCard.transform, "BtnSaveTeamSettings", "Save Team Settings", RoatanVisualAssets.IslandGold, new Vector2(0f, -124f), new Vector2(520f, 58f), 21, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                if (!EnsureConnectedForRequest()) return;
                subscribedClient.AddToSendQueue(MessageFrameBuilder.UpdateTeamSettingsFrame(new UpdateTeamSettingsDto
                {
                    Name = editTeamNameDraft.Trim(),
                    Tag = editTeamTagDraft.Trim().ToUpperInvariant(),
                    JoinPolicy = editTeamJoinPolicy,
                    MinElo = TeamMinEloOptions[Mathf.Clamp(editTeamMinEloIndex, 0, TeamMinEloOptions.Length - 1)],
                    ActivityRequirement = editTeamActivityReq
                }));
            }).GetComponentInChildren<TMP_Text>().color = new Color(0.08f, 0.10f, 0.12f, 1f);

            spawnedTeamSectionObjs.Add(settingsCard);
        }

        // 5. Team Roster List with Role Badges & Role-Appropriate Contextual Actions
        List<TeamMemberDto> members = details.Members ?? new List<TeamMemberDto>();
        GameObject rosterHdr = CreateCard(teamsContentRoot, "TeamRosterHdr", new Color(0.09f, 0.24f, 0.30f, 0.95f), Vector2.zero, new Vector2(860f, 42f));
        rosterHdr.AddComponent<LayoutElement>().preferredHeight = 42f;
        CreateText(rosterHdr.transform, "Title", $"TEAM ROSTER ({members.Count}/{info.MaxMembers})", 19, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(820f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;
        spawnedTeamSectionObjs.Add(rosterHdr);

        foreach (TeamMemberDto member in members)
        {
            TeamMemberDto capturedMember = member;
            bool isSelf = member.UserId == selfId;
            Color bg = isSelf
                ? new Color(0.10f, 0.31f, 0.30f, 0.98f)
                : new Color(0.07f, 0.20f, 0.26f, 0.95f);

            GameObject mRow = CreateCard(teamsContentRoot, $"TeamMember_{member.UserId}", bg, Vector2.zero, new Vector2(860f, 92f));
            mRow.AddComponent<LayoutElement>().preferredHeight = 92f;

            Color dotColor = member.IsOnline ? new Color(0.22f, 0.88f, 0.48f, 1f) : new Color(0.50f, 0.56f, 0.60f, 1f);
            CreateText(mRow.transform, "Dot", "●", 22, FontStyles.Bold, dotColor, new Vector2(-404f, 0f), new Vector2(28f, 36f));

            GameObject avObj = new GameObject("MemberAvatar");
            avObj.transform.SetParent(mRow.transform, false);
            RectTransform avRect = avObj.AddComponent<RectTransform>();
            avRect.anchoredPosition = new Vector2(-354f, 0f);
            avRect.sizeDelta = new Vector2(54f, 54f);
            Image avImg = avObj.AddComponent<Image>();
            avImg.raycastTarget = false;
            TMP_Text avInit = CreateText(avObj.transform, "Init", "?", 19, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(46f, 36f));
            avInit.raycastTarget = false;
            ApplyCircularAvatar(avImg, avInit, $"tmem_{member.UserId}", member.AvatarBase64, member.Username);

            string mRoleHex = member.Role.GetBadgeColorHex();
            string mRankBadge = RoatanRankTierHelper.FormatRichBadge(ResolveDisplayElo(member.Elo));
            string youTag = isSelf ? " <color=#F7C948>(You)</color>" : string.Empty;

            CreateText(mRow.transform, "MName", $"{member.Username}{youTag}  <size=17><color={mRoleHex}>[{member.Role.GetDisplayName()}]</color></size>", 20, FontStyles.Bold, Color.white, new Vector2(-98f, 15f), new Vector2(440f, 34f)).alignment = TextAlignmentOptions.MidlineLeft;
            CreateText(mRow.transform, "MElo", mRankBadge, 17, FontStyles.Normal, new Color(0.84f, 0.94f, 0.97f), new Vector2(-98f, -18f), new Vector2(440f, 28f)).alignment = TextAlignmentOptions.MidlineLeft;

            if (!isSelf)
            {
                Button rowClickBtn = mRow.AddComponent<Button>();
                rowClickBtn.transition = Selectable.Transition.ColorTint;
                rowClickBtn.onClick.AddListener(() =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    OpenPlayerActionModal(new PlayerActionProfileData
                    {
                        UserId = capturedMember.UserId,
                        Username = capturedMember.Username,
                        AvatarBase64 = capturedMember.AvatarBase64,
                        Elo = ResolveDisplayElo(capturedMember.Elo),
                        Wins = capturedMember.Wins,
                        Losses = capturedMember.Losses,
                        WinAssists = capturedMember.WinAssists,
                        TotalMoves = capturedMember.TotalMoves,
                        OptimalMoves = capturedMember.OptimalMoves,
                        AverageTurnSeconds = capturedMember.AverageTurnSeconds,
                        IsOnline = capturedMember.IsOnline,
                        TeamId = info.Id,
                        TeamTag = info.Tag
                    });
                });
            }

            if (!isSelf)
            {
                bool canPromote = isCaptain && member.Role == TeamRole.Teammate;
                bool canDemote = isCaptain && member.Role == TeamRole.ViceCaptain;
                bool canKick = (isCaptain && member.Role != TeamRole.Captain) ||
                               (myRole == TeamRole.ViceCaptain && member.Role == TeamRole.Teammate);

                if (canPromote)
                {
                    CreatePillButton(mRow.transform, "BtnPromote", "Promote", RoatanVisualAssets.BrightLagoon, new Vector2(216f, 0f), new Vector2(138f, 52f), 18, () =>
                    {
                        RoatanSoundFX.Instance?.PlayTap();
                        if (!EnsureConnectedForRequest()) return;
                        subscribedClient.AddToSendQueue(MessageFrameBuilder.PromoteOrDemoteMemberFrame(new PromoteOrDemoteMemberDto
                        {
                            TargetUserId = capturedMember.UserId,
                            NewRole = TeamRole.ViceCaptain
                        }));
                    });
                }
                else if (canDemote)
                {
                    CreatePillButton(mRow.transform, "BtnDemote", "Demote", new Color(0.42f, 0.34f, 0.14f, 1f), new Vector2(216f, 0f), new Vector2(138f, 52f), 18, () =>
                    {
                        RoatanSoundFX.Instance?.PlayTap();
                        if (!EnsureConnectedForRequest()) return;
                        subscribedClient.AddToSendQueue(MessageFrameBuilder.PromoteOrDemoteMemberFrame(new PromoteOrDemoteMemberDto
                        {
                            TargetUserId = capturedMember.UserId,
                            NewRole = TeamRole.Teammate
                        }));
                    });
                }

                if (canKick)
                {
                    float kickX = (canPromote || canDemote) ? 358f : 316f;
                    float kickW = (canPromote || canDemote) ? 124f : 168f;
                    CreatePillButton(mRow.transform, "BtnKick", "Kick", RoatanVisualAssets.SunsetCoral, new Vector2(kickX, 0f), new Vector2(kickW, 52f), 18, () =>
                    {
                        RoatanSoundFX.Instance?.PlayTap();
                        if (!EnsureConnectedForRequest()) return;
                        subscribedClient.AddToSendQueue(MessageFrameBuilder.KickTeamMemberFrame(new KickTeamMemberDto
                        {
                            TargetUserId = capturedMember.UserId
                        }));
                    });
                }
            }

            spawnedTeamSectionObjs.Add(mRow);
        }

        // 6. Open Challenge Board & Active 24h Team Challenges
        BuildOpenChallengeBoardSection(info, myRole);
    }

    private static string FormatPlayWindowRemaining(DateTime? playDeadlineUtc)
    {
        if (!playDeadlineUtc.HasValue) return "24h window";
        TimeSpan remaining = playDeadlineUtc.Value - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0) return "Expired";
        int hours = (int)remaining.TotalHours;
        int mins = remaining.Minutes;
        return hours > 0 ? $"{hours}h {mins}m left to play" : $"{Math.Max(1, mins)}m left to play";
    }

    private static string FormatOpenChallengeMinEloLabel(int minElo)
    {
        if (minElo <= RoatanRankTierHelper.MinElo) return "Any Team (100+)";
        string tierName = RoatanRankTierHelper.FromElo(minElo).GetDisplayName();
        return $"{tierName} ({minElo}+)";
    }

    private void BuildOpenChallengeBoardSection(TeamInfoDto myTeamInfo, TeamRole? myRole)
    {
        bool isCaptainOrVice = myTeamInfo != null && (myRole == TeamRole.Captain || myRole == TeamRole.ViceCaptain);
        bool isTeammate = myTeamInfo != null && myRole == TeamRole.Teammate;
        long myTeamId = myTeamInfo != null ? myTeamInfo.Id : -1;

        // A. Active Accepted Team Challenges (24-Hour Play Window)
        List<TeamChallengeDto> activeAccepted = latestOpenChallenges?.ActiveAcceptedChallenges;
        if (myTeamInfo != null && activeAccepted != null && activeAccepted.Count > 0)
        {
            GameObject actHdr = CreateCard(teamsContentRoot, "ActiveChallengesHdr", new Color(0.11f, 0.30f, 0.28f, 0.96f), Vector2.zero, new Vector2(860f, 44f));
            actHdr.AddComponent<LayoutElement>().preferredHeight = 44f;
            CreateText(actHdr.transform, "Title", $"ACTIVE TEAM CHALLENGES ({activeAccepted.Count}) — 24H PLAY WINDOW", 19, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(820f, 32f)).alignment = TextAlignmentOptions.MidlineLeft;
            spawnedTeamSectionObjs.Add(actHdr);

            foreach (TeamChallengeDto ch in activeAccepted)
            {
                TeamChallengeDto capturedCh = ch;
                GameObject row = CreateCard(teamsContentRoot, $"ActiveCh_{ch.ChallengeId}", new Color(0.08f, 0.26f, 0.30f, 0.98f), Vector2.zero, new Vector2(860f, 122f));
                row.AddComponent<LayoutElement>().preferredHeight = 122f;

                string modeStr = ch.GameMode == MatchFormat.TWO_BEST ? "Two-Best 1v1" : "Six-Lov 2v2";
                string varStr = ch.Variant == TableStyle.BLOCK ? "Block" : "Draw";
                string countdown = FormatPlayWindowRemaining(ch.PlayDeadlineUtc);
                string humansTag = ch.RequireFullHumanSquad ? "  •  <color=#FF9F43>[Humans Only]</color>" : string.Empty;
                string checkInStatus = $"Check-In: [{ch.ChallengerTeamTag}] {(ch.TeamACheckedIn ? "✓" : "…")}  vs  [{ch.TargetTeamTag ?? "Rival"}] {(ch.TeamBCheckedIn ? "✓" : "…")}";

                CreateText(row.transform, "Matchup", $"[{ch.ChallengerTeamTag}] {ch.ChallengerTeamName}  vs  [{ch.TargetTeamTag}] {ch.TargetTeamName}", 20, FontStyles.Bold, Color.white, new Vector2(-120f, 32f), new Vector2(580f, 32f)).alignment = TextAlignmentOptions.MidlineLeft;
                CreateText(row.transform, "Meta", $"{modeStr} • {varStr}{humansTag}  •  <color=#4BE38D><b>{countdown}</b></color>", 16, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(-120f, 0f), new Vector2(580f, 28f)).alignment = TextAlignmentOptions.MidlineLeft;
                CreateText(row.transform, "CheckIn", checkInStatus, 15, FontStyles.Normal, new Color(0.80f, 0.93f, 0.96f), new Vector2(-120f, -32f), new Vector2(580f, 26f)).alignment = TextAlignmentOptions.MidlineLeft;

                CreatePillButton(row.transform, "BtnJoinActiveCh", "Join Team Table", RoatanVisualAssets.PalmGreen, new Vector2(310f, 0f), new Vector2(204f, 62f), 18, () =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    if (!EnsureConnectedForRequest()) return;
                    if (!string.IsNullOrWhiteSpace(capturedCh.TableCode))
                    {
                        subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyWithCodeFrame(new LobbyCodeDto { Code = capturedCh.TableCode }));
                    }
                    else if (capturedCh.TableId > 0)
                    {
                        subscribedClient.AddToSendQueue(MessageFrameBuilder.JoinLobbyFrame(new JoinLobbyDto { LobbyIdToJoin = capturedCh.TableId }));
                    }
                });

                spawnedTeamSectionObjs.Add(row);
            }
        }

        // B. Open Challenge Board Header
        GameObject boardHdr = CreateCard(teamsContentRoot, "OpenChallengeBoardHdr", new Color(0.09f, 0.24f, 0.30f, 0.96f), Vector2.zero, new Vector2(860f, 44f));
        boardHdr.AddComponent<LayoutElement>().preferredHeight = 44f;
        CreateText(boardHdr.transform, "Title", "OPEN CHALLENGE BOARD", 20, FontStyles.Bold, RoatanVisualAssets.IslandGold, Vector2.zero, new Vector2(820f, 32f)).alignment = TextAlignmentOptions.MidlineLeft;
        spawnedTeamSectionObjs.Add(boardHdr);

        // C. Post Open Challenge Form (Captain & Vice-Captain ONLY)
        if (isCaptainOrVice)
        {
            GameObject postCard = CreateCard(teamsContentRoot, "PostOpenChallengeCard", new Color(0.06f, 0.19f, 0.26f, 0.96f), Vector2.zero, new Vector2(860f, 276f));
            postCard.AddComponent<LayoutElement>().preferredHeight = 276f;

            CreateText(postCard.transform, "PostHdr", "POST AN OPEN CHALLENGE (CAPTAIN / VICE-CAPTAIN)", 18, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, 108f), new Vector2(820f, 28f)).alignment = TextAlignmentOptions.MidlineLeft;

            Color activeGold = RoatanVisualAssets.IslandGold;
            Color activeBlue = RoatanVisualAssets.BrightLagoon;
            Color inactiveDark = new Color(0.14f, 0.22f, 0.28f, 1f);

            Button bSix = CreatePillButton(postCard.transform, "BtnOpenSixLov", "Six-Lov (2v2)", openChSelectedMode == MatchFormat.SIX_LOV ? activeGold : inactiveDark, new Vector2(-315f, 52f), new Vector2(194f, 50f), 17, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                openChSelectedMode = MatchFormat.SIX_LOV;
                RefreshTeamsTabUI();
            });
            if (openChSelectedMode == MatchFormat.SIX_LOV) bSix.GetComponentInChildren<TMP_Text>().color = new Color(0.08f, 0.10f, 0.12f, 1f);

            Button bTwo = CreatePillButton(postCard.transform, "BtnOpenTwoBest", "Two-Best (1v1)", openChSelectedMode == MatchFormat.TWO_BEST ? activeGold : inactiveDark, new Vector2(-110f, 52f), new Vector2(194f, 50f), 17, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                openChSelectedMode = MatchFormat.TWO_BEST;
                RefreshTeamsTabUI();
            });
            if (openChSelectedMode == MatchFormat.TWO_BEST) bTwo.GetComponentInChildren<TMP_Text>().color = new Color(0.08f, 0.10f, 0.12f, 1f);

            CreatePillButton(postCard.transform, "BtnOpenDraw", "Draw", openChSelectedVariant == TableStyle.DRAW ? activeBlue : inactiveDark, new Vector2(110f, 52f), new Vector2(194f, 50f), 17, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                openChSelectedVariant = TableStyle.DRAW;
                RefreshTeamsTabUI();
            });

            CreatePillButton(postCard.transform, "BtnOpenBlock", "Block", openChSelectedVariant == TableStyle.BLOCK ? activeBlue : inactiveDark, new Vector2(315f, 52f), new Vector2(194f, 50f), 17, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                openChSelectedVariant = TableStyle.BLOCK;
                RefreshTeamsTabUI();
            });

            int minEloVal = OpenChallengeMinEloOptions[Mathf.Clamp(openChMinEloIndex, 0, OpenChallengeMinEloOptions.Length - 1)];
            CreatePillButton(postCard.transform, "BtnOpenMinElo", $"Min Rival Elo: {FormatOpenChallengeMinEloLabel(minEloVal)}  ▾", new Color(0.10f, 0.28f, 0.35f, 1f), new Vector2(-210f, -14f), new Vector2(400f, 52f), 17, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                openChMinEloIndex = (openChMinEloIndex + 1) % OpenChallengeMinEloOptions.Length;
                RefreshTeamsTabUI();
            });

            CreatePillButton(postCard.transform, "BtnOpenNoBots", openChRequireHumansOnly ? "No Bots (Humans Only): ON ✓" : "No Bots (Humans Only): OFF", openChRequireHumansOnly ? RoatanVisualAssets.SunsetCoral : inactiveDark, new Vector2(210f, -14f), new Vector2(400f, 52f), 17, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                openChRequireHumansOnly = !openChRequireHumansOnly;
                RefreshTeamsTabUI();
            });

            CreatePillButton(postCard.transform, "BtnSubmitOpenChallenge", "Post Open Challenge", RoatanVisualAssets.PalmGreen, new Vector2(0f, -92f), new Vector2(600f, 58f), 21, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                if (!EnsureConnectedForRequest()) return;
                subscribedClient.AddToSendQueue(MessageFrameBuilder.PostOpenTeamChallengeFrame(new PostOpenTeamChallengeDto
                {
                    GameMode = openChSelectedMode,
                    Variant = openChSelectedVariant,
                    MinOpponentTeamElo = OpenChallengeMinEloOptions[Mathf.Clamp(openChMinEloIndex, 0, OpenChallengeMinEloOptions.Length - 1)],
                    RequireFullHumanSquad = openChRequireHumansOnly
                }));
            });

            spawnedTeamSectionObjs.Add(postCard);
        }

        // D. Open Board Challenge Rows (Role-Aware: Accept / Ask Leaders / Cancel)
        List<TeamChallengeDto> openList = latestOpenChallenges?.OpenChallenges;
        if (openList == null || openList.Count == 0)
        {
            GameObject emptyCard = CreateCard(teamsContentRoot, "NoOpenChallengesRow", new Color(0.06f, 0.17f, 0.22f, 0.72f), Vector2.zero, new Vector2(860f, 64f));
            emptyCard.AddComponent<LayoutElement>().preferredHeight = 64f;
            CreateText(emptyCard.transform, "Lbl", "No open challenges on the board right now — leaders can post one above!", 18, FontStyles.Italic, new Color(0.76f, 0.88f, 0.92f), Vector2.zero, new Vector2(820f, 40f));
            spawnedTeamSectionObjs.Add(emptyCard);
            return;
        }

        foreach (TeamChallengeDto ch in openList)
        {
            TeamChallengeDto capturedCh = ch;
            bool isOwnTeam = myTeamId > 0 && ch.ChallengerTeamId == myTeamId;
            bool hasEndorsements = !isOwnTeam && ch.RequestedByTeammateUsernames != null && ch.RequestedByTeammateUsernames.Count > 0;
            bool showDisqualify = !isOwnTeam && !ch.MeetsConditions && !string.IsNullOrWhiteSpace(ch.DisqualifyReason);
            float rowH = (hasEndorsements || showDisqualify) ? 148f : 118f;

            GameObject row = CreateCard(teamsContentRoot, $"OpenCh_{ch.ChallengeId}", new Color(0.07f, 0.21f, 0.28f, 0.96f), Vector2.zero, new Vector2(860f, rowH));
            row.AddComponent<LayoutElement>().preferredHeight = rowH;

            string tierBadge = RoatanRankTierHelper.FormatRichBadge(ch.ChallengerTeamElo);
            string modeStr = ch.GameMode == MatchFormat.TWO_BEST ? "Two-Best 1v1" : "Six-Lov 2v2";
            string varStr = ch.Variant == TableStyle.BLOCK ? "Block" : "Draw";
            string humansStr = ch.RequireFullHumanSquad ? "  •  <color=#FF9F43>Humans Only (No Bots)</color>" : string.Empty;
            float topY = rowH * 0.5f - 26f;

            CreateText(row.transform, "ChTeam", $"[{ch.ChallengerTeamTag}] {ch.ChallengerTeamName}  <size=17>{tierBadge}</size>", 21, FontStyles.Bold, Color.white, new Vector2(-120f, topY), new Vector2(580f, 34f)).alignment = TextAlignmentOptions.MidlineLeft;
            CreateText(row.transform, "ChMeta", $"{modeStr} • {varStr}  •  Min Rival: {FormatOpenChallengeMinEloLabel(ch.MinOpponentTeamElo)}{humansStr}", 16, FontStyles.Normal, RoatanVisualAssets.IslandGold, new Vector2(-120f, topY - 32f), new Vector2(580f, 28f)).alignment = TextAlignmentOptions.MidlineLeft;

            if (hasEndorsements)
            {
                string reqNames = string.Join(", ", ch.RequestedByTeammateUsernames);
                CreateText(row.transform, "ChEndorse", $"★ Requested by {reqNames}", 16, FontStyles.Bold, new Color(0.42f, 0.95f, 0.68f), new Vector2(-120f, topY - 64f), new Vector2(580f, 26f)).alignment = TextAlignmentOptions.MidlineLeft;
            }
            else if (showDisqualify)
            {
                CreateText(row.transform, "ChDisq", $"✗ {ch.DisqualifyReason}", 16, FontStyles.Bold, RoatanVisualAssets.SunsetCoral, new Vector2(-120f, topY - 64f), new Vector2(580f, 26f)).alignment = TextAlignmentOptions.MidlineLeft;
            }

            if (isOwnTeam && isCaptainOrVice)
            {
                CreatePillButton(row.transform, "BtnCancelOpenCh", "Cancel", RoatanVisualAssets.SunsetCoral, new Vector2(310f, 0f), new Vector2(198f, 58f), 19, () =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    if (!EnsureConnectedForRequest()) return;
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.CancelOpenTeamChallengeFrame(new CancelOpenTeamChallengeDto
                    {
                        ChallengeId = capturedCh.ChallengeId
                    }));
                });
            }
            else if (isOwnTeam)
            {
                CreateText(row.transform, "OwnPostLbl", "YOUR POST", 16, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(310f, 0f), new Vector2(198f, 40f));
            }
            else if (isCaptainOrVice)
            {
                bool canAccept = ch.MeetsConditions;
                Button accBtn = CreatePillButton(row.transform, "BtnAcceptOpenCh", "Accept Challenge", canAccept ? RoatanVisualAssets.PalmGreen : new Color(0.28f, 0.24f, 0.26f, 0.90f), new Vector2(310f, 0f), new Vector2(204f, 60f), 17, () =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    if (!EnsureConnectedForRequest()) return;
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.AcceptOpenTeamChallengeFrame(new AcceptOpenTeamChallengeDto
                    {
                        ChallengeId = capturedCh.ChallengeId
                    }));
                });
                accBtn.interactable = canAccept;
            }
            else if (isTeammate)
            {
                bool alreadyAsked = ch.AlreadyRequestedByMe;
                bool canAsk = ch.MeetsConditions && !alreadyAsked;
                string askLabel = alreadyAsked ? "Asked Leaders ✓" : "Ask Leaders";
                Color askColor = alreadyAsked
                    ? new Color(0.14f, 0.42f, 0.28f, 0.95f)
                    : (canAsk ? RoatanVisualAssets.BrightLagoon : new Color(0.28f, 0.24f, 0.26f, 0.90f));
                Button askBtn = CreatePillButton(row.transform, "BtnAskLeaders", askLabel, askColor, new Vector2(310f, 0f), new Vector2(204f, 60f), 18, () =>
                {
                    RoatanSoundFX.Instance?.PlayTap();
                    if (!EnsureConnectedForRequest()) return;
                    subscribedClient.AddToSendQueue(MessageFrameBuilder.RequestAcceptTeamChallengeFrame(new RequestAcceptTeamChallengeDto
                    {
                        ChallengeId = capturedCh.ChallengeId
                    }));
                });
                askBtn.interactable = canAsk;
            }

            spawnedTeamSectionObjs.Add(row);
        }
    }

    private void OnGetLeaderboardResponse(ResponseObjectDto<LeaderboardDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null) return;
        latestLeaderboard = dto.Object;
        RefreshLeaderboardUI();
    }

    private void RefreshLeaderboardUI()
    {
        RefreshLeaderboardSelfBanner();

        if (leaderboardListContent == null) return;
        foreach (GameObject row in spawnedLeaderboardRows) Destroy(row);
        spawnedLeaderboardRows.Clear();

        List<LeaderboardEntryDto> entries = latestLeaderboard?.Entries;
        if (entries == null || entries.Count == 0)
        {
            GameObject emptyRow = CreateCard(leaderboardListContent, "EmptyLeaderboardRow", new Color(0.08f, 0.21f, 0.27f, 0.7f), Vector2.zero, new Vector2(870f, 84f));
            emptyRow.AddComponent<LayoutElement>().preferredHeight = 84f;
            CreateText(emptyRow.transform, "EmptyLbl", "No ranked players on the leaderboard yet — complete an online match!", 21, FontStyles.Italic, new Color(0.82f, 0.92f, 0.95f), Vector2.zero, new Vector2(830f, 60f));
            spawnedLeaderboardRows.Add(emptyRow);
            return;
        }

        long selfId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;
        foreach (LeaderboardEntryDto entry in entries)
        {
            SpawnLeaderboardRow(entry, selfId);
        }
    }

    private void RefreshLeaderboardSelfBanner()
    {
        if (leaderboardSelfBannerCard == null) return;

        LeaderboardEntryDto selfEntry = latestLeaderboard?.LocalPlayerEntry;
        UserDto localUser = subscribedClient?.User;

        if (selfEntry != null)
        {
            string tierHex = string.IsNullOrWhiteSpace(selfEntry.TierColorHex) ? selfEntry.Tier.GetBadgeColorHex() : selfEntry.TierColorHex;
            string tierName = string.IsNullOrWhiteSpace(selfEntry.TierName) ? selfEntry.Tier.GetDisplayName() : selfEntry.TierName;
            string accStr = selfEntry.TotalMoves > 0 ? $"{selfEntry.AccuracyPercent:0.#}%" : "—";

            if (leaderboardSelfRankLabel != null) leaderboardSelfRankLabel.text = $"#{selfEntry.Rank}";
            if (leaderboardSelfNameTierLabel != null)
            {
                leaderboardSelfNameTierLabel.text = $"{selfEntry.Username} (You)\n<size=17><color={tierHex}>[{tierName}]</color>  •  Rank #{selfEntry.Rank} of {Math.Max(1, latestLeaderboard.TotalRankedPlayers)}</size>";
            }
            if (leaderboardSelfStatsLabel != null)
            {
                leaderboardSelfStatsLabel.text = $"<b>{selfEntry.Elo}</b> Elo  •  {selfEntry.Wins}/{selfEntry.Losses}\n<size=16><color=#CBE6EE>Acc {accStr}  •  {selfEntry.WinAssists} Ast</color></size>";
            }
            ApplyCircularAvatar(leaderboardSelfAvatarImg, leaderboardSelfAvatarInitials, $"lb_self_{selfEntry.UserId}", selfEntry.AvatarBase64, selfEntry.Username);
        }
        else if (localUser != null)
        {
            int elo = ResolveDisplayElo(localUser);
            RoatanRankTierInfo info = RoatanRankTierHelper.GetInfoForElo(elo);
            string accStr = localUser.TotalMoves > 0 ? $"{localUser.AccuracyPercent:0.#}%" : "—";
            if (leaderboardSelfRankLabel != null) leaderboardSelfRankLabel.text = "#—";
            if (leaderboardSelfNameTierLabel != null)
            {
                leaderboardSelfNameTierLabel.text = $"{localUser.Login} (You)\n<size=17><color={info.BadgeColorHex}>[{info.DisplayName}]</color>  •  Your Ranking</size>";
            }
            if (leaderboardSelfStatsLabel != null)
            {
                leaderboardSelfStatsLabel.text = $"<b>{elo}</b> Elo  •  {localUser.Wins}/{localUser.Losses}\n<size=16><color=#CBE6EE>Acc {accStr}  •  {localUser.WinAssists} Ast</color></size>";
            }
            ApplyCircularAvatar(leaderboardSelfAvatarImg, leaderboardSelfAvatarInitials, $"lb_self_{localUser.Id}", localUser.AvatarBase64, localUser.Login);
        }
    }

    private void SpawnLeaderboardRow(LeaderboardEntryDto entry, long selfId)
    {
        bool isSelf = entry.UserId == selfId;
        Color bg = isSelf
            ? new Color(0.11f, 0.34f, 0.32f, 0.98f)
            : new Color(0.07f, 0.21f, 0.27f, 0.94f);

        GameObject row = CreateCard(leaderboardListContent, $"LB_{entry.Rank}_{entry.UserId}", bg, Vector2.zero, new Vector2(870f, 84f));
        row.AddComponent<LayoutElement>().preferredHeight = 84f;

        Color rankColor = entry.Rank == 1
            ? RoatanVisualAssets.IslandGold
            : (entry.Rank == 2
                ? new Color(0.84f, 0.88f, 0.92f)
                : (entry.Rank == 3 ? new Color(0.85f, 0.56f, 0.28f) : Color.white));
        CreateText(row.transform, "RankNum", $"#{entry.Rank}", 22, FontStyles.Bold, rankColor, new Vector2(-404f, 0f), new Vector2(68f, 44f));

        GameObject avObj = new GameObject("Avatar");
        avObj.transform.SetParent(row.transform, false);
        RectTransform avRect = avObj.AddComponent<RectTransform>();
        avRect.anchoredPosition = new Vector2(-334f, 0f);
        avRect.sizeDelta = new Vector2(54f, 54f);
        Image avImg = avObj.AddComponent<Image>();
        avImg.raycastTarget = false;
        TMP_Text avInit = CreateText(avObj.transform, "Init", "?", 19, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(46f, 36f));
        avInit.raycastTarget = false;
        ApplyCircularAvatar(avImg, avInit, $"lb_{entry.UserId}", entry.AvatarBase64, entry.Username);

        string tierHex = string.IsNullOrWhiteSpace(entry.TierColorHex) ? entry.Tier.GetBadgeColorHex() : entry.TierColorHex;
        string tierName = string.IsNullOrWhiteSpace(entry.TierName) ? entry.Tier.GetDisplayName() : entry.TierName;
        string youTag = isSelf ? " <color=#F7C948>(You)</color>" : string.Empty;

        TMP_Text nameTier = CreateText(row.transform, "NameTier", $"{entry.Username}{youTag}\n<size=16><color={tierHex}>[{tierName}]</color></size>", 19, FontStyles.Bold, Color.white, new Vector2(-132f, 0f), new Vector2(336f, 68f));
        nameTier.alignment = TextAlignmentOptions.MidlineLeft;

        CreateText(row.transform, "EloVal", entry.Elo.ToString(), 22, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(105f, 0f), new Vector2(96f, 40f));
        CreateText(row.transform, "WLVal", $"{entry.Wins}/{entry.Losses}", 19, FontStyles.Normal, Color.white, new Vector2(210f, 0f), new Vector2(94f, 40f));
        string accStr = entry.TotalMoves > 0 ? $"{entry.AccuracyPercent:0}%" : "—";
        CreateText(row.transform, "AccVal", accStr, 19, FontStyles.Normal, new Color(0.68f, 0.94f, 0.80f), new Vector2(310f, 0f), new Vector2(96f, 40f));
        CreateText(row.transform, "AstVal", entry.WinAssists.ToString(), 19, FontStyles.Bold, new Color(0.68f, 0.88f, 0.96f), new Vector2(400f, 0f), new Vector2(74f, 40f));

        if (!isSelf)
        {
            LeaderboardEntryDto capturedEntry = entry;
            Button rowBtn = row.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.ColorTint;
            rowBtn.onClick.AddListener(() =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                PlayerPresenceDto presence = latestRoster.FirstOrDefault(p => p.UserId == capturedEntry.UserId);
                OpenPlayerActionModal(new PlayerActionProfileData
                {
                    UserId = capturedEntry.UserId,
                    Username = capturedEntry.Username,
                    AvatarBase64 = capturedEntry.AvatarBase64,
                    Elo = ResolveDisplayElo(capturedEntry.Elo),
                    Wins = capturedEntry.Wins,
                    Losses = capturedEntry.Losses,
                    WinAssists = capturedEntry.WinAssists,
                    TotalMoves = capturedEntry.TotalMoves,
                    OptimalMoves = capturedEntry.OptimalMoves,
                    AverageTurnSeconds = capturedEntry.AverageTurnSeconds,
                    IsOnline = presence != null && presence.IsOnline,
                    TeamId = capturedEntry.TeamId ?? presence?.TeamId,
                    TeamTag = !string.IsNullOrWhiteSpace(capturedEntry.TeamTag) ? capturedEntry.TeamTag : presence?.TeamTag
                });
            });
        }

        spawnedLeaderboardRows.Add(row);
    }

    // ==================== TEAM LEADERBOARD & TEAM CHALLENGES ====================

    private void SelectLeaderboardSubTab(bool showTeams)
    {
        leaderboardShowingTeams = showTeams;
        if (lbPlayersSubViewObj != null) lbPlayersSubViewObj.SetActive(!showTeams);
        if (lbTeamsSubViewObj != null) lbTeamsSubViewObj.SetActive(showTeams);

        Color inactive = new Color(0.14f, 0.22f, 0.28f, 1f);
        if (lbPlayersSubTabImg != null) lbPlayersSubTabImg.color = !showTeams ? RoatanVisualAssets.IslandGold : inactive;
        if (lbTeamsSubTabImg != null) lbTeamsSubTabImg.color = showTeams ? RoatanVisualAssets.PalmGreen : inactive;
        if (lbPlayersSubTabLabel != null) lbPlayersSubTabLabel.color = !showTeams ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
        if (lbTeamsSubTabLabel != null) lbTeamsSubTabLabel.color = Color.white;

        if (showTeams)
        {
            RefreshTeamLeaderboardUI();
            if (isSignedIn && Networking.Instance != null && Networking.Instance.IsConnected)
            {
                RequestTeamLeaderboard();
            }
        }
        else
        {
            RefreshLeaderboardUI();
            if (isSignedIn && Networking.Instance != null && Networking.Instance.IsConnected)
            {
                RequestLeaderboard();
            }
        }
    }

    private void OnGetTeamLeaderboardResponse(ResponseObjectDto<TeamLeaderboardDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null) return;
        latestTeamLeaderboard = dto.Object;
        RefreshTeamLeaderboardUI();
    }

    private void RefreshTeamLeaderboardUI()
    {
        RefreshTeamLeaderboardSelfBanner();

        if (teamLeaderboardListContent == null) return;
        foreach (GameObject row in spawnedTeamLeaderboardRows) Destroy(row);
        spawnedTeamLeaderboardRows.Clear();

        List<TeamLeaderboardEntryDto> entries = latestTeamLeaderboard?.Entries;
        if (entries == null || entries.Count == 0)
        {
            GameObject emptyRow = CreateCard(teamLeaderboardListContent, "EmptyTeamLeaderboardRow", new Color(0.08f, 0.21f, 0.27f, 0.7f), Vector2.zero, new Vector2(870f, 84f));
            emptyRow.AddComponent<LayoutElement>().preferredHeight = 84f;
            CreateText(emptyRow.transform, "EmptyLbl", "No teams on the leaderboard yet — create a crew in the Teams tab!", 20, FontStyles.Italic, new Color(0.82f, 0.92f, 0.95f), Vector2.zero, new Vector2(830f, 60f));
            spawnedTeamLeaderboardRows.Add(emptyRow);
            return;
        }

        long myTeamId = myTeamDetails?.TeamInfo != null ? myTeamDetails.TeamInfo.Id : -1;
        bool canChallengeOthers = myTeamDetails?.TeamInfo != null &&
                                  (myTeamDetails.MyRole == TeamRole.Captain || myTeamDetails.MyRole == TeamRole.ViceCaptain);

        foreach (TeamLeaderboardEntryDto entry in entries)
        {
            SpawnTeamLeaderboardRow(entry, myTeamId, canChallengeOthers);
        }
    }

    private void RefreshTeamLeaderboardSelfBanner()
    {
        if (teamLeaderboardSelfCard == null) return;

        TeamLeaderboardEntryDto mine = latestTeamLeaderboard?.MyTeamEntry;
        if (mine != null)
        {
            string tierHex = string.IsNullOrWhiteSpace(mine.TierColorHex) ? mine.TeamRankTier.GetBadgeColorHex() : mine.TierColorHex;
            string tierName = string.IsNullOrWhiteSpace(mine.TierName) ? mine.TeamRankTier.GetDisplayName() : mine.TierName;
            if (teamLeaderboardSelfRankLabel != null) teamLeaderboardSelfRankLabel.text = $"#{mine.Rank}";
            if (teamLeaderboardSelfNameTierLabel != null)
            {
                teamLeaderboardSelfNameTierLabel.text = $"[{mine.Tag}] {mine.Name} <color=#F7C948>(Your Team)</color>\n<size=16><color={tierHex}>[{tierName}]</color>  •  Rank #{mine.Rank} of {Math.Max(1, latestTeamLeaderboard.TotalTeams)}</size>";
            }
            if (teamLeaderboardSelfStatsLabel != null)
            {
                teamLeaderboardSelfStatsLabel.text = $"<b>{mine.TeamElo}</b> Elo  •  {mine.TeamWins}/{mine.TeamLosses}\n<size=15><color=#CBE6EE>Squad Avg: {mine.AverageMemberElo}  •  {mine.MemberCount}/{mine.MaxMembers}</color></size>";
            }
        }
        else if (myTeamDetails?.TeamInfo != null)
        {
            TeamInfoDto info = myTeamDetails.TeamInfo;
            RoatanRankTierInfo tInfo = RoatanRankTierHelper.GetInfoForElo(info.TeamElo);
            if (teamLeaderboardSelfRankLabel != null) teamLeaderboardSelfRankLabel.text = "#—";
            if (teamLeaderboardSelfNameTierLabel != null)
            {
                teamLeaderboardSelfNameTierLabel.text = $"[{info.Tag}] {info.Name} <color=#F7C948>(Your Team)</color>\n<size=16><color={tInfo.BadgeColorHex}>[{tInfo.DisplayName}]</color>  •  Captain: {info.CaptainUsername}</size>";
            }
            if (teamLeaderboardSelfStatsLabel != null)
            {
                teamLeaderboardSelfStatsLabel.text = $"<b>{info.TeamElo}</b> Elo  •  {info.TeamWins}/{info.TeamLosses}\n<size=16><color=#CBE6EE>{info.MemberCount}/{info.MaxMembers} Members</color></size>";
            }
        }
        else
        {
            if (teamLeaderboardSelfRankLabel != null) teamLeaderboardSelfRankLabel.text = "#—";
            if (teamLeaderboardSelfNameTierLabel != null)
            {
                teamLeaderboardSelfNameTierLabel.text = "Not in a Team Yet\n<size=16><color=#B8DCE6>Create or join a crew in the Teams tab to compete!</color></size>";
            }
            if (teamLeaderboardSelfStatsLabel != null) teamLeaderboardSelfStatsLabel.text = string.Empty;
        }
    }

    private void SpawnTeamLeaderboardRow(TeamLeaderboardEntryDto entry, long myTeamId, bool canChallengeOthers)
    {
        TeamLeaderboardEntryDto captured = entry;
        bool isMyTeam = myTeamId > 0 && entry.TeamId == myTeamId;
        Color bg = isMyTeam
            ? new Color(0.11f, 0.34f, 0.32f, 0.98f)
            : new Color(0.07f, 0.21f, 0.27f, 0.95f);

        GameObject row = CreateCard(teamLeaderboardListContent, $"TLB_{entry.Rank}_{entry.TeamId}", bg, Vector2.zero, new Vector2(870f, 92f));
        row.AddComponent<LayoutElement>().preferredHeight = 92f;

        Color rankColor = entry.Rank == 1
            ? RoatanVisualAssets.IslandGold
            : (entry.Rank == 2
                ? new Color(0.84f, 0.88f, 0.92f)
                : (entry.Rank == 3 ? new Color(0.85f, 0.56f, 0.28f) : Color.white));
        CreateText(row.transform, "RankNum", $"#{entry.Rank}", 22, FontStyles.Bold, rankColor, new Vector2(-404f, 0f), new Vector2(60f, 44f));

        string tierHex = string.IsNullOrWhiteSpace(entry.TierColorHex) ? entry.TeamRankTier.GetBadgeColorHex() : entry.TierColorHex;
        string tierName = string.IsNullOrWhiteSpace(entry.TierName) ? entry.TeamRankTier.GetDisplayName() : entry.TierName;
        string myTag = isMyTeam ? " <color=#F7C948>(You)</color>" : string.Empty;

        TMP_Text nameTier = CreateText(
            row.transform,
            "TeamNameTier",
            $"[{entry.Tag}] {entry.Name}{myTag}\n<size=15><color={tierHex}>[{tierName}]</color>  •  Squad Avg: {entry.AverageMemberElo}  •  Capt: {entry.CaptainUsername}</size>",
            19,
            FontStyles.Bold,
            Color.white,
            new Vector2(-180f, 0f),
            new Vector2(368f, 72f));
        nameTier.alignment = TextAlignmentOptions.MidlineLeft;

        CreateText(row.transform, "EloVal", entry.TeamElo.ToString(), 22, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(60f, 0f), new Vector2(88f, 40f));
        CreateText(row.transform, "WLVal", $"{entry.TeamWins}/{entry.TeamLosses}", 18, FontStyles.Normal, Color.white, new Vector2(150f, 0f), new Vector2(84f, 40f));
        CreateText(row.transform, "CrewVal", $"{entry.MemberCount}/{entry.MaxMembers}", 18, FontStyles.Normal, new Color(0.74f, 0.92f, 0.96f), new Vector2(236f, 0f), new Vector2(78f, 40f));

        if (canChallengeOthers && !isMyTeam)
        {
            CreatePillButton(row.transform, "BtnChallengeTeam", "Challenge", RoatanVisualAssets.SunsetCoral, new Vector2(358f, 0f), new Vector2(146f, 54f), 18, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                OpenChallengeTeamModal(captured);
            });
        }
        else if (isMyTeam)
        {
            CreateText(row.transform, "YourCrewLbl", "YOUR CREW", 15, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(358f, 0f), new Vector2(146f, 36f));
        }

        spawnedTeamLeaderboardRows.Add(row);
    }

    private void OpenChallengeTeamModal(TeamLeaderboardEntryDto targetTeam)
    {
        if (targetTeam == null || challengeTeamModal == null) return;
        challengeTargetTeam = targetTeam;
        challengeSelectedMode = MatchFormat.SIX_LOV;
        challengeSelectedVariant = TableStyle.DRAW;

        string tierBadge = RoatanRankTierHelper.FormatRichBadge(targetTeam.TeamElo);
        if (challengeTeamModalTitle != null)
        {
            challengeTeamModalTitle.text = $"[{targetTeam.Tag}] {targetTeam.Name}  •  {tierBadge}";
        }
        RefreshChallengeModalButtons();
        challengeTeamModal.transform.SetAsLastSibling();
        challengeTeamModal.SetActive(true);
    }

    private void RefreshChallengeModalButtons()
    {
        Color inactive = new Color(0.14f, 0.22f, 0.28f, 1f);
        if (challengeModeSixLovImg != null)
        {
            bool active = challengeSelectedMode == MatchFormat.SIX_LOV;
            challengeModeSixLovImg.color = active ? RoatanVisualAssets.IslandGold : inactive;
            TMP_Text lbl = challengeModeSixLovImg.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.color = active ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
        }
        if (challengeModeTwoBestImg != null)
        {
            bool active = challengeSelectedMode == MatchFormat.TWO_BEST;
            challengeModeTwoBestImg.color = active ? RoatanVisualAssets.IslandGold : inactive;
            TMP_Text lbl = challengeModeTwoBestImg.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.color = active ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
        }
        if (challengeVariantDrawImg != null)
        {
            challengeVariantDrawImg.color = challengeSelectedVariant == TableStyle.DRAW ? RoatanVisualAssets.BrightLagoon : inactive;
        }
        if (challengeVariantBlockImg != null)
        {
            challengeVariantBlockImg.color = challengeSelectedVariant == TableStyle.BLOCK ? RoatanVisualAssets.BrightLagoon : inactive;
        }
    }

    private void SubmitTeamChallenge()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (challengeTargetTeam == null || !EnsureConnectedForRequest()) return;

        long targetId = challengeTargetTeam.TeamId;
        challengeTeamModal.SetActive(false);

        if (currentLobby != null)
        {
            subscribedClient.AddToSendQueue(MessageFrameBuilder.LeaveLobbyFrame());
            currentLobby = null;
        }

        subscribedClient.AddToSendQueue(MessageFrameBuilder.SendTeamChallengeFrame(new SendTeamChallengeDto
        {
            TargetTeamId = targetId,
            GameMode = challengeSelectedMode,
            Variant = challengeSelectedVariant
        }));
        SetFeedback($"Sending challenge to [{challengeTargetTeam.Tag}] {challengeTargetTeam.Name}...", RoatanVisualAssets.IslandGold);
        challengeTargetTeam = null;
    }

    private void OnSendTeamChallengeResponse(ResponseObjectDto<TeamChallengeActionResponseDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null)
        {
            string err = FormatFriendlyError(dto, "Could not send team challenge.");
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            return;
        }

        TeamChallengeDto ch = dto.Object.Challenge;
        if (dto.Object.Table != null)
        {
            currentLobby = dto.Object.Table;
            localReady = false;
            InLobby.SetOfflineLobby(currentLobby);
            ShowSubView(lobbyView);
            RefreshLobbyView();
            RequestPlayerRoster();
        }

        string msg = !string.IsNullOrWhiteSpace(ch?.Message)
            ? ch.Message
            : $"Challenge sent to [{ch?.TargetTeamTag}] {ch?.TargetTeamName}!";
        SetFeedback(msg, RoatanVisualAssets.PalmGreen);
    }

    private void OnRespondTeamChallengeResponse(ResponseObjectDto<TeamChallengeActionResponseDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null)
        {
            string err = FormatFriendlyError(dto, "Could not respond to team challenge.");
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            return;
        }

        TeamChallengeDto ch = dto.Object.Challenge;
        if (dto.Object.Table != null)
        {
            currentLobby = dto.Object.Table;
            localReady = false;
            InLobby.SetOfflineLobby(currentLobby);
            ShowSubView(lobbyView);
            RefreshLobbyView();
            RequestPlayerRoster();
            RequestOpenTeamChallenges();
            SetFeedback($"Joined Team Challenge vs [{ch?.ChallengerTeamTag}] {ch?.ChallengerTeamName}!", RoatanVisualAssets.PalmGreen);
        }
        else
        {
            string msg = !string.IsNullOrWhiteSpace(ch?.Message) ? ch.Message : "Team challenge declined.";
            SetFeedback(msg, RoatanVisualAssets.IslandGold);
            RequestOpenTeamChallenges();
        }
    }

    private void OnGetOpenTeamChallengesResponse(ResponseObjectDto<OpenTeamChallengesDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null) return;
        latestOpenChallenges = dto.Object;
        if (activeHubTab == HubBrowserTab.Teams)
        {
            RefreshTeamsTabUI();
        }
    }

    private void OnPostOpenTeamChallengeResponse(ResponseObjectDto<TeamChallengeDto> dto)
    {
        if (dto == null || !dto.OkResult)
        {
            SetFeedback(FormatFriendlyError(dto, "Could not post open challenge."), RoatanVisualAssets.SunsetCoral);
            return;
        }
        SetFeedback("Open challenge posted to the Challenge Board!", RoatanVisualAssets.PalmGreen);
        RequestOpenTeamChallenges();
    }

    private void OnCancelOpenTeamChallengeResponse(ResponseObjectDto<TeamChallengeDto> dto)
    {
        if (dto == null || !dto.OkResult)
        {
            SetFeedback(FormatFriendlyError(dto, "Could not cancel open challenge."), RoatanVisualAssets.SunsetCoral);
            return;
        }
        SetFeedback("Open challenge cancelled.", RoatanVisualAssets.IslandGold);
        RequestOpenTeamChallenges();
    }

    private void OnRequestAcceptTeamChallengeResponse(ResponseObjectDto<TeamChallengeDto> dto)
    {
        if (dto == null || !dto.OkResult)
        {
            SetFeedback(FormatFriendlyError(dto, "Could not ask leaders."), RoatanVisualAssets.SunsetCoral);
            return;
        }
        SetFeedback("Asked your Captain & Vice-Captains to accept this challenge!", RoatanVisualAssets.PalmGreen);
        RequestOpenTeamChallenges();
    }

    private void OnAcceptOpenTeamChallengeResponse(ResponseObjectDto<TeamChallengeActionResponseDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null)
        {
            SetFeedback(FormatFriendlyError(dto, "Could not accept open challenge."), RoatanVisualAssets.SunsetCoral);
            RequestOpenTeamChallenges();
            return;
        }

        TeamChallengeDto ch = dto.Object.Challenge;
        if (dto.Object.Table != null)
        {
            currentLobby = dto.Object.Table;
            localReady = false;
            InLobby.SetOfflineLobby(currentLobby);
            ShowSubView(lobbyView);
            RefreshLobbyView();
            RequestPlayerRoster();
        }
        RequestOpenTeamChallenges();
        SetFeedback($"Accepted [{ch?.ChallengerTeamTag}] {ch?.ChallengerTeamName}'s challenge — 24h play window started!", RoatanVisualAssets.PalmGreen);
    }

    private void OnTeamChallengeNotification(TeamChallengeDto notification)
    {
        if (notification == null) return;

        // Teammate "Ask Leaders" endorsement notification for an Open Board Challenge
        if (notification.IsOpenBoardChallenge && notification.Status == TeamChallengeStatus.Pending)
        {
            if (!string.IsNullOrWhiteSpace(notification.Message))
            {
                SetFeedback(notification.Message, RoatanVisualAssets.IslandGold);
            }
            if (isSignedIn && Networking.Instance != null && Networking.Instance.IsConnected)
            {
                RequestOpenTeamChallenges();
            }
            return;
        }

        if (notification.Status == TeamChallengeStatus.Declined)
        {
            string declinedMsg = !string.IsNullOrWhiteSpace(notification.Message)
                ? notification.Message
                : $"[{notification.TargetTeamTag}] {notification.TargetTeamName} declined your team challenge.";
            SetFeedback(declinedMsg, RoatanVisualAssets.SunsetCoral);
            if (currentLobby != null && currentLobby.Id == notification.TableId)
            {
                currentLobby = null;
                ShowSubView(hubView);
                RequestLobbiesList();
            }
            return;
        }

        // If already seated at this challenge table and it was accepted, just toast!
        if (notification.Status == TeamChallengeStatus.Accepted && currentLobby != null && currentLobby.Id == notification.TableId)
        {
            SetFeedback($"[{notification.TargetTeamTag}] {notification.TargetTeamName} accepted the challenge!", RoatanVisualAssets.PalmGreen);
            return;
        }

        if (incomingTeamChallengeModal == null) return;

        pendingTeamChallenge = notification;
        string modeStr = notification.GameMode == MatchFormat.TWO_BEST ? "Two-Best (1v1)" : "Six-Lov (2v2)";
        string varStr = notification.Variant == TableStyle.BLOCK ? "Block (Cerrado)" : "Draw";

        if (notification.Status == TeamChallengeStatus.Pending)
        {
            if (incomingTeamChallengeTitle != null) incomingTeamChallengeTitle.text = "INCOMING TEAM CHALLENGE!";
            if (incomingTeamChallengeBody != null)
            {
                incomingTeamChallengeBody.text =
                    $"<b>[{notification.ChallengerTeamTag}] {notification.ChallengerTeamName}</b> ({notification.ChallengerTeamElo} Elo)\n" +
                    $"challenged your crew to a <b>{modeStr} • {varStr}</b> match!\n\n" +
                    $"<size=19><color=#F7C948>Accept to enter the Team Challenge table immediately.</color></size>";
            }
            if (incomingTeamChallengeAcceptLabel != null) incomingTeamChallengeAcceptLabel.text = "Accept & Join Table";
            if (incomingTeamChallengeDeclineLabel != null) incomingTeamChallengeDeclineLabel.text = "Decline";
        }
        else if (notification.Status == TeamChallengeStatus.Accepted)
        {
            if (incomingTeamChallengeTitle != null) incomingTeamChallengeTitle.text = "CREW MATCH LIVE!";
            if (incomingTeamChallengeBody != null)
            {
                incomingTeamChallengeBody.text =
                    $"<b>[{notification.ChallengerTeamTag}] {notification.ChallengerTeamName}</b> vs <b>[{notification.TargetTeamTag}] {notification.TargetTeamName}</b>\n" +
                    $"Mode: <b>{modeStr} • {varStr}</b>\n\n" +
                    $"<size=19><color=#4BE38D>Your team's open seat is waiting — tap below to join!</color></size>";
            }
            if (incomingTeamChallengeAcceptLabel != null) incomingTeamChallengeAcceptLabel.text = "Join Team Table";
            if (incomingTeamChallengeDeclineLabel != null) incomingTeamChallengeDeclineLabel.text = "Dismiss";
        }

        incomingTeamChallengeModal.transform.SetAsLastSibling();
        incomingTeamChallengeModal.SetActive(true);
    }

    // ==================== LIVE MATCH SPECTATING ====================

    private void OnJoinAsSpectatorResponse(ResponseObjectDto<SpectatorMatchSnapshotDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null)
        {
            string err = FormatFriendlyError(dto, "Unable to spectate that match.");
            SetFeedback(err, RoatanVisualAssets.SunsetCoral);
            return;
        }

        SpectatorMatchSnapshotDto snap = dto.Object;
        isSpectatingMatch = true;
        spectatingGameId = snap.GameId;
        spectatingMatchTitle = !string.IsNullOrWhiteSpace(snap.MatchTitle) ? snap.MatchTitle : $"Live Match #{snap.GameId}";
        liveSpectatorCount = Math.Max(1, snap.SpectatorCount);
        spectatingPlayers.Clear();
        if (snap.Players != null) spectatingPlayers.AddRange(snap.Players);

        CancelOnlineShuffle();
        ClearOnlineBoardVisuals();
        onlineHand.Clear();
        onlinePossibleMoves = null;
        onlineSelectedDomino = null;
        onlineTurnTimerActive = false;
        onlineIsBotControlled = false;
        if (onlineTimerBadgeObj != null) onlineTimerBadgeObj.SetActive(false);
        if (onlineDrawBtn != null) onlineDrawBtn.gameObject.SetActive(false);
        if (onlineEndOverlay != null) onlineEndOverlay.SetActive(false);
        RefreshOnlineTakeoverIndicators();

        if (snap.BoardMoves != null)
        {
            foreach (MoveDto move in snap.BoardMoves)
            {
                SpawnOnlineBoardMove(move);
            }
        }

        RebuildOnlineHandUI();
        ShowSubView(onlineGameView);
        RefreshOnlineSpectatorHUD();

        if (snap.NumberOfDominoPerPlayer != null)
        {
            OnOnlineCountsUpdated(new NumberOfDominoPerPlayerDto { NumberOfDomino = snap.NumberOfDominoPerPlayer });
        }

        string activeName = spectatingPlayers.FirstOrDefault(p => p.Id == snap.CurrentTurnPlayerId)?.Name ?? "Player";
        onlineMatchStatus.text = $"SPECTATING LIVE  •  {activeName}'s turn  •  Hand #{Math.Max(1, snap.HandNumber)}";
        SetFeedback($"Watching {spectatingMatchTitle} live!", RoatanVisualAssets.BrightLagoon);
    }

    private void OnLeaveSpectatorResponse(ResponseDto dto)
    {
        isSpectatingMatch = false;
        spectatingGameId = 0;
        liveSpectatorCount = 0;
        spectatingPlayers.Clear();
        RefreshOnlineSpectatorHUD();
        ShowSubView(hubView);
        RequestLobbiesList();
    }

    private void OnSpectatorCountUpdated(SpectatorCountUpdateDto dto)
    {
        if (dto == null) return;
        bool matchesCurrent = (isSpectatingMatch && dto.GameId == spectatingGameId) ||
                              (!isSpectatingMatch && currentLobby != null && currentLobby.Id == dto.GameId);
        if (!matchesCurrent && onlineGameView != null && !onlineGameView.activeSelf) return;

        liveSpectatorCount = Math.Max(0, dto.SpectatorCount);
        RefreshOnlineSpectatorHUD();
    }

    private void LeaveSpectatingMatch()
    {
        RoatanSoundFX.Instance?.PlayTap();
        int gameIdToLeave = spectatingGameId;
        isSpectatingMatch = false;
        spectatingGameId = 0;
        liveSpectatorCount = 0;
        spectatingPlayers.Clear();
        RefreshOnlineSpectatorHUD();

        if (EnsureConnectedForRequest())
        {
            subscribedClient.AddToSendQueue(MessageFrameBuilder.LeaveSpectatorFrame(new LeaveSpectatorDto { GameId = gameIdToLeave }));
        }
        ShowSubView(hubView);
        RequestLobbiesList();
    }

    private void RefreshOnlineSpectatorHUD()
    {
        if (onlineSpectatorBadgeObj != null)
        {
            bool showBadge = isSpectatingMatch || liveSpectatorCount > 0;
            onlineSpectatorBadgeObj.SetActive(showBadge);
            if (onlineSpectatorBadgeLabel != null)
            {
                onlineSpectatorBadgeLabel.text = $"Watching: {liveSpectatorCount}";
            }
        }
        if (onlineLeaveSpectateBtn != null)
        {
            onlineLeaveSpectateBtn.gameObject.SetActive(isSpectatingMatch);
        }
    }

    // ==================== PLAYER ACTION CARD & DIRECT CHALLENGES ====================

    private void OpenPlayerActionModal(PlayerActionProfileData target)
    {
        if (target == null || playerActionModal == null) return;
        selectedActionPlayer = target;

        ApplyCircularAvatar(playerActionAvatarImg, playerActionAvatarInitials, $"act_{target.UserId}", target.AvatarBase64, target.Username);
        string tierBadge = RoatanRankTierHelper.FormatRichBadge(ResolveDisplayElo(target.Elo));
        string teamTagStr = !string.IsNullOrWhiteSpace(target.TeamTag) ? $"  <color=#F7C948>[{target.TeamTag}]</color>" : string.Empty;
        string onlineStr = target.IsOnline ? "<color=#4BE38D>● Online</color>" : "<color=#8FA6B0>● Offline</color>";

        if (playerActionNameTierLabel != null)
        {
            playerActionNameTierLabel.text = $"{target.Username}{teamTagStr}\n<size=20>{tierBadge}  •  {onlineStr}</size>";
        }

        if (playerActionStatsLabel != null)
        {
            double acc = RoatanRankTierHelper.CalculateAccuracyPercent(target.OptimalMoves, target.TotalMoves);
            string accStr = target.TotalMoves > 0 ? $"{acc:0.#}%" : "—";
            string speedStr = target.AverageTurnSeconds > 0.01 ? $"{target.AverageTurnSeconds:0.0}s" : "—";
            playerActionStatsLabel.text =
                $"Wins / Losses: <b>{target.Wins} - {target.Losses}</b>  •  Win Assists: <b>{target.WinAssists}</b>\n" +
                $"Accuracy: <b>{accStr}</b>  •  Avg Speed: <b>{speedStr}</b>";
        }

        if (playerActionChallengeBtn != null)
        {
            playerActionChallengeBtn.interactable = target.IsOnline;
            TMP_Text chLbl = playerActionChallengeBtn.GetComponentInChildren<TMP_Text>();
            if (chLbl != null)
            {
                chLbl.text = target.IsOnline ? "Challenge Player (1v1)" : "Player Offline (Cannot Challenge)";
            }
        }

        if (playerActionInviteTeamBtn != null)
        {
            bool senderCanInvite = myTeamDetails?.TeamInfo != null &&
                                   myTeamDetails.TeamInfo.MemberCount < TeamInfoDto.DefaultMaxMembers &&
                                   !target.TeamId.HasValue;
            playerActionInviteTeamBtn.gameObject.SetActive(senderCanInvite);
        }

        playerActionModal.transform.SetAsLastSibling();
        playerActionModal.SetActive(true);
    }

    private void OpenDirectChallengeModal(PlayerActionProfileData target)
    {
        if (target == null || directChallengeModal == null) return;
        selectedActionPlayer = target;
        directChallengeMode = MatchFormat.TWO_BEST;
        directChallengeVariant = TableStyle.DRAW;

        if (directChallengeModalTitle != null)
        {
            string badge = RoatanRankTierHelper.FormatRichBadge(ResolveDisplayElo(target.Elo));
            directChallengeModalTitle.text = $"{target.Username}  •  {badge}";
        }
        RefreshDirectChallengeModalButtons();
        directChallengeModal.transform.SetAsLastSibling();
        directChallengeModal.SetActive(true);
    }

    private void RefreshDirectChallengeModalButtons()
    {
        Color inactive = new Color(0.14f, 0.22f, 0.28f, 1f);
        if (directChallengeTwoBestImg != null)
        {
            bool active = directChallengeMode == MatchFormat.TWO_BEST;
            directChallengeTwoBestImg.color = active ? RoatanVisualAssets.IslandGold : inactive;
            TMP_Text lbl = directChallengeTwoBestImg.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.color = active ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
        }
        if (directChallengeCutThroatImg != null)
        {
            bool active = directChallengeMode == MatchFormat.CUT_THROAT;
            directChallengeCutThroatImg.color = active ? RoatanVisualAssets.IslandGold : inactive;
            TMP_Text lbl = directChallengeCutThroatImg.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.color = active ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
        }
        if (directChallengeDrawImg != null)
        {
            directChallengeDrawImg.color = directChallengeVariant == TableStyle.DRAW ? RoatanVisualAssets.BrightLagoon : inactive;
        }
        if (directChallengeBlockImg != null)
        {
            directChallengeBlockImg.color = directChallengeVariant == TableStyle.BLOCK ? RoatanVisualAssets.BrightLagoon : inactive;
        }
    }

    private void SubmitDirectChallenge()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (selectedActionPlayer == null || !EnsureConnectedForRequest()) return;

        long targetId = selectedActionPlayer.UserId;
        string targetName = selectedActionPlayer.Username;
        directChallengeModal.SetActive(false);

        subscribedClient.AddToSendQueue(MessageFrameBuilder.SendDirectChallengeFrame(new SendDirectChallengeDto
        {
            TargetUserId = targetId,
            GameMode = directChallengeMode,
            Variant = directChallengeVariant
        }));
        SetFeedback($"Sent 1v1 challenge to {targetName}!", RoatanVisualAssets.IslandGold);
    }

    private void OnSendDirectChallengeResponse(ResponseObjectDto<DirectChallengeActionResponseDto> dto)
    {
        if (dto == null || !dto.OkResult)
        {
            SetFeedback(FormatFriendlyError(dto, "Could not send challenge."), RoatanVisualAssets.SunsetCoral);
            return;
        }
        string targetName = dto.Object?.Challenge?.TargetUsername ?? "player";
        SetFeedback($"Challenge sent to {targetName} — waiting for response...", RoatanVisualAssets.PalmGreen);
    }

    private void OnRespondDirectChallengeResponse(ResponseObjectDto<DirectChallengeActionResponseDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null)
        {
            SetFeedback(FormatFriendlyError(dto, "Could not respond to challenge."), RoatanVisualAssets.SunsetCoral);
            return;
        }

        if (dto.Object.Table != null)
        {
            currentLobby = dto.Object.Table;
            localReady = false;
            InLobby.SetOfflineLobby(currentLobby);
            ShowSubView(lobbyView);
            RefreshLobbyView();
            RequestPlayerRoster();
            SetFeedback($"Joined 1v1 challenge table vs {dto.Object.Challenge?.ChallengerUsername}!", RoatanVisualAssets.PalmGreen);
        }
        else
        {
            SetFeedback("Challenge ignored.", RoatanVisualAssets.IslandGold);
        }
    }

    private void OnDirectChallengeNotification(DirectChallengeDto notification)
    {
        if (notification == null) return;

        if (notification.Status == DirectChallengeStatus.Accepted)
        {
            SetFeedback($"{notification.TargetUsername} accepted your 1v1 challenge!", RoatanVisualAssets.PalmGreen);
            if (currentLobby != null && notification.TableId.HasValue && currentLobby.Id == notification.TableId.Value)
            {
                ShowSubView(lobbyView);
                RefreshLobbyView();
            }
            return;
        }

        if (notification.Status == DirectChallengeStatus.Ignored)
        {
            SetFeedback($"{notification.TargetUsername} ignored your 1v1 challenge.", RoatanVisualAssets.SunsetCoral);
            return;
        }

        if (incomingDirectChallengeModal == null) return;
        pendingDirectChallenge = notification;
        string modeStr = notification.GameMode == MatchFormat.CUT_THROAT ? "Cut-Throat (1v1)" : "Two-Best (1v1)";
        string varStr = notification.Variant == TableStyle.BLOCK ? "Block (Cerrado)" : "Draw";
        string rankBadge = RoatanRankTierHelper.FormatRichBadge(ResolveDisplayElo(notification.ChallengerElo));

        if (incomingDirectChallengeBody != null)
        {
            incomingDirectChallengeBody.text =
                $"<b>{notification.ChallengerUsername}</b>  {rankBadge}\n" +
                $"challenged you to a <b>{modeStr} • {varStr}</b> match!\n\n" +
                $"<size=19><color=#4BE38D>Tap Accept to jump straight into a private 2-seat table.</color></size>";
        }

        incomingDirectChallengeModal.transform.SetAsLastSibling();
        incomingDirectChallengeModal.SetActive(true);
    }

    // ==================== TEAM CHAT & DIRECT MESSAGING ====================

    private void SelectChatChannel(bool showDirectMessages)
    {
        chatShowingDirectMessages = showDirectMessages;
        ClearCurrentChatChannelUnread();
        RefreshChatTabUI();
    }

    private void OpenDirectMessageThread(long partnerUserId, string partnerUsername)
    {
        if (partnerUserId <= 0) return;
        activeDmPartnerUserId = partnerUserId;
        activeDmPartnerUsername = string.IsNullOrWhiteSpace(partnerUsername) ? $"Player {partnerUserId}" : partnerUsername;
        dmPartnerNamesByUserId[partnerUserId] = activeDmPartnerUsername;
        if (!dmThreadsByUserId.ContainsKey(partnerUserId))
        {
            dmThreadsByUserId[partnerUserId] = new List<ChatMessageDto>();
        }

        chatShowingDirectMessages = true;
        unreadDmCountByUserId[partnerUserId] = 0;
        ShowSubView(hubView);
        SelectHubBrowserTab(HubBrowserTab.Chat);
        if (EnsureConnectedForRequest())
        {
            subscribedClient.AddToSendQueue(MessageFrameBuilder.GetChatHistoryFrame(new GetChatHistoryDto
            {
                ChannelType = SocialChatChannelType.Direct,
                OtherUserId = partnerUserId,
                Limit = 60
            }));
        }
    }

    private void SubmitSocialChatMessage()
    {
        RoatanSoundFX.Instance?.PlayTap();
        if (chatComposeInput == null || !EnsureConnectedForRequest()) return;
        string body = chatComposeInput.text.Trim();
        if (string.IsNullOrEmpty(body)) return;

        if (!chatShowingDirectMessages)
        {
            if (myTeamDetails?.TeamInfo == null)
            {
                SetFeedback("Join or create a team first to use Team Chat.", RoatanVisualAssets.SunsetCoral);
                return;
            }
            chatComposeInput.text = string.Empty;
            subscribedClient.AddToSendQueue(MessageFrameBuilder.SendSocialChatMessageFrame(new ChatMessageDto
            {
                ChannelType = SocialChatChannelType.Team,
                TeamId = myTeamDetails.TeamInfo.Id,
                Body = body
            }));
        }
        else
        {
            if (activeDmPartnerUserId <= 0)
            {
                SetFeedback("Select a player above (or tap Send Message on any player) first.", RoatanVisualAssets.SunsetCoral);
                return;
            }
            chatComposeInput.text = string.Empty;
            subscribedClient.AddToSendQueue(MessageFrameBuilder.SendSocialChatMessageFrame(new ChatMessageDto
            {
                ChannelType = SocialChatChannelType.Direct,
                RecipientUserId = activeDmPartnerUserId,
                Body = body
            }));
        }
    }

    private void OnSendSocialChatMessageResponse(ResponseObjectDto<ChatMessageDto> dto)
    {
        if (dto == null) return;
        if (!dto.OkResult)
        {
            SetFeedback(FormatFriendlyError(dto, "Could not send message."), RoatanVisualAssets.SunsetCoral);
            return;
        }
        if (dto.Object != null)
        {
            AppendChatMessageIfNew(dto.Object, countAsUnread: false);
            if (activeHubTab == HubBrowserTab.Chat)
            {
                RefreshChatTabUI();
            }
        }
    }

    private void OnGetChatHistoryResponse(ResponseObjectDto<ChatHistoryDto> dto)
    {
        if (dto == null || !dto.OkResult || dto.Object == null) return;
        if (dto.Object.Messages != null)
        {
            foreach (ChatMessageDto msg in dto.Object.Messages)
            {
                AppendChatMessageIfNew(msg, countAsUnread: false);
            }
        }
        UpdateChatTabUnreadBadges();
        if (activeHubTab == HubBrowserTab.Chat)
        {
            RefreshChatTabUI();
        }
    }

    private void OnChatMessageReceived(ChatMessageDto msg)
    {
        if (msg == null) return;
        long selfId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;
        bool isFromMe = msg.SenderUserId == selfId;
        bool isViewingNow = hubView != null && hubView.activeSelf && activeHubTab == HubBrowserTab.Chat &&
                            ((msg.ChannelType == SocialChatChannelType.Team && !chatShowingDirectMessages) ||
                             (msg.ChannelType == SocialChatChannelType.Direct && chatShowingDirectMessages &&
                              (msg.SenderUserId == activeDmPartnerUserId || msg.RecipientUserId == activeDmPartnerUserId)));

        AppendChatMessageIfNew(msg, countAsUnread: !isFromMe && !isViewingNow);
        UpdateChatTabUnreadBadges();
        if (activeHubTab == HubBrowserTab.Chat)
        {
            RefreshChatTabUI();
        }
    }

    private void AppendChatMessageIfNew(ChatMessageDto msg, bool countAsUnread)
    {
        if (msg == null) return;
        long selfId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;

        if (msg.ChannelType == SocialChatChannelType.Team)
        {
            if (teamChatMessages.Any(m => m.MessageId > 0 && m.MessageId == msg.MessageId)) return;
            teamChatMessages.Add(msg);
            if (countAsUnread) unreadTeamChatCount++;
        }
        else
        {
            long partnerId = msg.SenderUserId == selfId ? (msg.RecipientUserId ?? 0) : msg.SenderUserId;
            if (partnerId <= 0) return;

            string partnerName = msg.SenderUserId == selfId
                ? (msg.RecipientUsername ?? $"Player {partnerId}")
                : (msg.SenderUsername ?? $"Player {partnerId}");
            dmPartnerNamesByUserId[partnerId] = partnerName;

            if (!dmThreadsByUserId.TryGetValue(partnerId, out List<ChatMessageDto> thread))
            {
                thread = new List<ChatMessageDto>();
                dmThreadsByUserId[partnerId] = thread;
            }
            if (thread.Any(m => m.MessageId > 0 && m.MessageId == msg.MessageId)) return;
            thread.Add(msg);

            if (countAsUnread)
            {
                unreadDmCountByUserId[partnerId] = unreadDmCountByUserId.TryGetValue(partnerId, out int prev) ? prev + 1 : 1;
            }
        }
    }

    private void ClearCurrentChatChannelUnread()
    {
        if (!chatShowingDirectMessages)
        {
            unreadTeamChatCount = 0;
        }
        else if (activeDmPartnerUserId > 0)
        {
            unreadDmCountByUserId[activeDmPartnerUserId] = 0;
        }
        UpdateChatTabUnreadBadges();
    }

    private void UpdateChatTabUnreadBadges()
    {
        int totalDmUnread = unreadDmCountByUserId.Values.Sum();
        int totalUnread = unreadTeamChatCount + totalDmUnread;

        if (hubChatTabLabel != null)
        {
            hubChatTabLabel.text = totalUnread > 0 ? $"Chat ({totalUnread})" : "Chat";
        }
        if (chatTeamSubTabLabel != null)
        {
            chatTeamSubTabLabel.text = unreadTeamChatCount > 0 ? $"Team Chat ({unreadTeamChatCount})" : "Team Chat";
        }
        if (chatDmSubTabLabel != null)
        {
            chatDmSubTabLabel.text = totalDmUnread > 0 ? $"Direct Messages ({totalDmUnread})" : "Direct Messages";
        }
    }

    private void RefreshChatTabUI()
    {
        Color inactive = new Color(0.14f, 0.22f, 0.28f, 1f);
        if (chatTeamSubTabImg != null) chatTeamSubTabImg.color = !chatShowingDirectMessages ? RoatanVisualAssets.PalmGreen : inactive;
        if (chatDmSubTabImg != null) chatDmSubTabImg.color = chatShowingDirectMessages ? RoatanVisualAssets.BrightLagoon : inactive;
        if (chatDmPartnersStripObj != null) chatDmPartnersStripObj.SetActive(chatShowingDirectMessages);
        UpdateChatTabUnreadBadges();

        if (chatShowingDirectMessages)
        {
            RefreshDmPartnersStripUI();
            if (chatActiveChannelHeader != null)
            {
                chatActiveChannelHeader.text = activeDmPartnerUserId > 0
                    ? $"DIRECT MESSAGE  •  {activeDmPartnerUsername}"
                    : "DIRECT MESSAGES — Tap a player below or tap Send Message on any player";
            }
        }
        else
        {
            if (chatActiveChannelHeader != null)
            {
                chatActiveChannelHeader.text = myTeamDetails?.TeamInfo != null
                    ? $"TEAM CHAT  •  [{myTeamDetails.TeamInfo.Tag}] {myTeamDetails.TeamInfo.Name}"
                    : "TEAM CHAT — Join or create a team to chat with your crew";
            }
        }

        if (chatMessagesListContent == null) return;
        foreach (GameObject go in spawnedChatMessageRows) Destroy(go);
        spawnedChatMessageRows.Clear();

        List<ChatMessageDto> messages = !chatShowingDirectMessages
            ? teamChatMessages
            : (activeDmPartnerUserId > 0 && dmThreadsByUserId.TryGetValue(activeDmPartnerUserId, out List<ChatMessageDto> dmList)
                ? dmList
                : new List<ChatMessageDto>());

        if (messages.Count == 0)
        {
            GameObject emptyCard = CreateCard(chatMessagesListContent, "EmptyChatRow", new Color(0.07f, 0.19f, 0.25f, 0.72f), Vector2.zero, new Vector2(860f, 74f));
            emptyCard.AddComponent<LayoutElement>().preferredHeight = 74f;
            string emptyMsg = !chatShowingDirectMessages
                ? (myTeamDetails?.TeamInfo != null ? "No team messages yet — say hello to your crew!" : "Join or create a team in the Teams tab to unlock Team Chat.")
                : (activeDmPartnerUserId > 0 ? $"No messages with {activeDmPartnerUsername} yet — start the conversation!" : "Select a player chip above to open a Direct Message thread.");
            CreateText(emptyCard.transform, "EmptyLbl", emptyMsg, 19, FontStyles.Italic, new Color(0.80f, 0.92f, 0.96f), Vector2.zero, new Vector2(820f, 52f));
            spawnedChatMessageRows.Add(emptyCard);
            return;
        }

        long selfId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;
        foreach (ChatMessageDto msg in messages)
        {
            bool isMine = msg.SenderUserId == selfId;
            Color bubbleBg = isMine
                ? new Color(0.10f, 0.34f, 0.30f, 0.96f)
                : new Color(0.07f, 0.21f, 0.28f, 0.95f);

            GameObject row = CreateCard(chatMessagesListContent, $"ChatMsg_{msg.MessageId}", bubbleBg, Vector2.zero, new Vector2(860f, 78f));
            row.AddComponent<LayoutElement>().preferredHeight = 78f;

            string senderTag = isMine ? "<color=#F7C948>You</color>" : $"<b>{msg.SenderUsername}</b>";
            string timeStr = msg.SentUtc.ToLocalTime().ToString("HH:mm");
            TMP_Text headerTxt = CreateText(row.transform, "Sender", $"{senderTag}  <size=15><color=#9FC5D0>{timeStr}</color></size>", 17, FontStyles.Bold, Color.white, new Vector2(0f, 18f), new Vector2(820f, 26f));
            headerTxt.alignment = isMine ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;

            TMP_Text bodyTxt = CreateText(row.transform, "Body", msg.Body ?? string.Empty, 19, FontStyles.Normal, new Color(0.92f, 0.97f, 0.99f), new Vector2(0f, -14f), new Vector2(820f, 34f));
            bodyTxt.alignment = isMine ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;

            spawnedChatMessageRows.Add(row);
        }
    }

    private void RefreshDmPartnersStripUI()
    {
        if (chatDmPartnersContent == null) return;
        foreach (GameObject chip in spawnedChatPartnerChips) Destroy(chip);
        spawnedChatPartnerChips.Clear();

        long selfId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;
        Dictionary<long, string> partners = new Dictionary<long, string>(dmPartnerNamesByUserId);

        if (myTeamDetails?.Members != null)
        {
            foreach (TeamMemberDto m in myTeamDetails.Members)
            {
                if (m.UserId != selfId && !partners.ContainsKey(m.UserId))
                {
                    partners[m.UserId] = m.Username;
                }
            }
        }
        foreach (PlayerPresenceDto p in latestRoster)
        {
            if (p.UserId != selfId && p.IsOnline && !partners.ContainsKey(p.UserId))
            {
                partners[p.UserId] = p.Username;
            }
        }

        if (activeDmPartnerUserId <= 0 && partners.Count > 0)
        {
            KeyValuePair<long, string> first = partners.First();
            activeDmPartnerUserId = first.Key;
            activeDmPartnerUsername = first.Value;
        }

        foreach (KeyValuePair<long, string> kv in partners.Take(6))
        {
            long partnerId = kv.Key;
            string partnerName = kv.Value;
            int unread = unreadDmCountByUserId.TryGetValue(partnerId, out int u) ? u : 0;
            bool selected = partnerId == activeDmPartnerUserId;
            string label = unread > 0 ? $"{partnerName} ({unread})" : partnerName;
            Color bg = selected ? RoatanVisualAssets.IslandGold : new Color(0.12f, 0.25f, 0.32f, 1f);

            Button chipBtn = CreatePillButton(chatDmPartnersContent, $"DmChip_{partnerId}", label, bg, Vector2.zero, new Vector2(148f, 42f), 16, () =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                OpenDirectMessageThread(partnerId, partnerName);
            });
            chipBtn.gameObject.AddComponent<LayoutElement>().preferredWidth = 148f;
            if (selected)
            {
                TMP_Text lbl = chipBtn.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.color = new Color(0.08f, 0.10f, 0.12f, 1f);
            }
            spawnedChatPartnerChips.Add(chipBtn.gameObject);
        }
    }

    private void RefreshPlayersRosterUI()
    {
        if (playersRosterContent == null) return;
        foreach (GameObject row in spawnedRosterRows) Destroy(row);
        spawnedRosterRows.Clear();

        if (latestRoster.Count == 0)
        {
            GameObject emptyRow = CreateCard(playersRosterContent, "EmptyPlayersRow", new Color(0.08f, 0.21f, 0.27f, 0.7f), Vector2.zero, new Vector2(860f, 84f));
            emptyRow.AddComponent<LayoutElement>().preferredHeight = 84f;
            CreateText(emptyRow.transform, "EmptyLbl", "No registered players found yet.", 23, FontStyles.Italic, new Color(0.82f, 0.92f, 0.95f), Vector2.zero, new Vector2(820f, 60f));
            spawnedRosterRows.Add(emptyRow);
            return;
        }

        long selfId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;
        foreach (PlayerPresenceDto player in latestRoster)
        {
            SpawnRosterRow(playersRosterContent, spawnedRosterRows, player, selfId);
        }
    }

    private void RefreshInvitePlayersModalUI()
    {
        if (invitePlayersListContent == null) return;
        foreach (GameObject row in spawnedInviteModalRows) Destroy(row);
        spawnedInviteModalRows.Clear();

        long selfId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;
        List<PlayerPresenceDto> invitable = latestRoster.Where(p => p.UserId != selfId).ToList();
        if (invitable.Count == 0)
        {
            GameObject emptyRow = CreateCard(invitePlayersListContent, "NoInvitableRow", new Color(0.08f, 0.21f, 0.27f, 0.7f), Vector2.zero, new Vector2(800f, 84f));
            emptyRow.AddComponent<LayoutElement>().preferredHeight = 84f;
            CreateText(emptyRow.transform, "EmptyLbl", "No other players registered yet.", 23, FontStyles.Italic, new Color(0.82f, 0.92f, 0.95f), Vector2.zero, new Vector2(760f, 60f));
            spawnedInviteModalRows.Add(emptyRow);
            return;
        }

        foreach (PlayerPresenceDto player in invitable)
        {
            SpawnRosterRow(invitePlayersListContent, spawnedInviteModalRows, player, selfId);
        }
    }

    private void SpawnRosterRow(RectTransform parent, List<GameObject> bucket, PlayerPresenceDto player, long selfId)
    {
        PlayerPresenceDto captured = player;
        Color bg = player.IsOnline
            ? new Color(0.08f, 0.24f, 0.30f, 0.96f)
            : new Color(0.07f, 0.14f, 0.18f, 0.80f);

        GameObject row = CreateCard(parent, $"Roster_{player.UserId}", bg, Vector2.zero, new Vector2(840f, 96f));
        row.AddComponent<LayoutElement>().preferredHeight = 96f;

        Color dotColor = player.IsOnline ? new Color(0.22f, 0.88f, 0.48f, 1f) : new Color(0.50f, 0.56f, 0.60f, 1f);
        CreateText(row.transform, "Dot", "●", 24, FontStyles.Bold, dotColor, new Vector2(-386f, 0f), new Vector2(30f, 40f));

        // Circular Player Avatar Thumbnail
        GameObject avObj = new GameObject("Avatar");
        avObj.transform.SetParent(row.transform, false);
        RectTransform avRect = avObj.AddComponent<RectTransform>();
        avRect.anchoredPosition = new Vector2(-334f, 0f);
        avRect.sizeDelta = new Vector2(60f, 60f);
        Image avImg = avObj.AddComponent<Image>();
        avImg.raycastTarget = false;
        TMP_Text avInit = CreateText(avObj.transform, "Init", "?", 22, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(52f, 40f));
        avInit.raycastTarget = false;
        ApplyCircularAvatar(avImg, avInit, $"roster_{player.UserId}", player.AvatarBase64, player.Username);

        string youSuffix = player.UserId == selfId ? " (You)" : string.Empty;
        string rosterRankBadge = RoatanRankTierHelper.FormatRichBadge(ResolveDisplayElo(player.Elo));
        CreateText(row.transform, "PName", $"{player.Username}{youSuffix}  <size=18>{rosterRankBadge}</size>", 21, FontStyles.Bold, player.IsOnline ? Color.white : new Color(0.68f, 0.74f, 0.78f), new Vector2(-68f, 16f), new Vector2(460f, 36f)).alignment = TextAlignmentOptions.MidlineLeft;

        string statusText = player.IsOnline
            ? (string.IsNullOrWhiteSpace(player.TableName) ? player.Status : $"{player.Status} • {player.TableName}")
            : "Offline";
        Color statusColor = !player.IsOnline
            ? new Color(0.55f, 0.62f, 0.66f)
            : (player.Status == "Available" ? new Color(0.32f, 0.90f, 0.56f) : RoatanVisualAssets.IslandGold);
        CreateText(row.transform, "PStatus", statusText, 18, FontStyles.Normal, statusColor, new Vector2(-78f, -18f), new Vector2(430f, 30f)).alignment = TextAlignmentOptions.MidlineLeft;

        if (player.UserId != selfId)
        {
            Button rowBtn = row.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.ColorTint;
            rowBtn.onClick.AddListener(() =>
            {
                RoatanSoundFX.Instance?.PlayTap();
                OpenPlayerActionModal(new PlayerActionProfileData
                {
                    UserId = captured.UserId,
                    Username = captured.Username,
                    AvatarBase64 = captured.AvatarBase64,
                    Elo = ResolveDisplayElo(captured.Elo),
                    Wins = captured.Wins,
                    Losses = captured.Losses,
                    WinAssists = captured.WinAssists,
                    TotalMoves = captured.TotalMoves,
                    OptimalMoves = captured.OptimalMoves,
                    AverageTurnSeconds = captured.AverageTurnSeconds,
                    IsOnline = captured.IsOnline,
                    TeamId = captured.TeamId,
                    TeamTag = captured.TeamTag
                });
            });
        }

        if (player.IsOnline && player.UserId != selfId)
        {
            bool canInviteNow = currentLobby != null && currentLobby.Players.Count < currentLobby.MaxNumberOfPlayer;
            Button invBtn = CreatePillButton(row.transform, "BtnInvite", "Invite", canInviteNow ? RoatanVisualAssets.PalmGreen : new Color(0.18f, 0.28f, 0.34f, 0.85f), new Vector2(290f, 0f), new Vector2(168f, 56f), 21, () =>
            {
                RequestInvitePlayer(captured);
            });
            invBtn.interactable = canInviteNow;
        }

        bucket.Add(row);
    }

    private void RefreshConnectionBadge()
    {
        if (connectionBadgeText == null) return;
        bool connected = Networking.Instance != null && Networking.Instance.IsConnected && !Networking.Instance.IsOfflineMode;
        if (isConnecting)
        {
            connectionBadgeText.text = $"● {RoatanLocalization.T("connecting")}";
            connectionBadgeBg.color = new Color(0.55f, 0.42f, 0.10f, 0.95f);
        }
        else if (connected)
        {
            connectionBadgeText.text = $"● {RoatanLocalization.T("server_connected")}";
            connectionBadgeBg.color = new Color(0.11f, 0.45f, 0.28f, 0.95f);
        }
        else
        {
            connectionBadgeText.text = "● Server Disconnected — Tap Edit to Configure";
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

    private bool IsLocalPlayerTableHost()
    {
        if (currentLobby == null || subscribedClient?.User == null) return false;
        long myId = subscribedClient.User.Id;
        if (currentLobby.Creator != null && currentLobby.Creator.Id == myId) return true;
        if (currentLobby.IsHouseTable && (currentLobby.Creator == null || currentLobby.Creator.Id == 0))
        {
            PlayerDto firstHuman = currentLobby.Players?.FirstOrDefault(p => !p.IsBot);
            return firstHuman != null && firstHuman.Id == myId;
        }
        return false;
    }

    private void RefreshHostBotDifficultyButtons()
    {
        foreach (KeyValuePair<string, Image> kv in hostBotDiffBtns)
        {
            if (kv.Value == null) continue;
            bool selected = string.Equals(kv.Key, selectedHostBotDifficulty, StringComparison.OrdinalIgnoreCase);
            kv.Value.color = selected ? RoatanVisualAssets.IslandGold : new Color(0.14f, 0.22f, 0.28f, 1f);
            TMP_Text lbl = kv.Value.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.color = selected ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
            }
        }
    }

    private void RefreshLobbyView()
    {
        if (currentLobby == null) return;
        lobbyTitleLabel.text = currentLobby.Name;
        string codePart = !string.IsNullOrEmpty(currentLobby.Code) ? $"  •  Code: {currentLobby.Code}" : "";
        string deadlinePart = currentLobby.PlayDeadlineUtc.HasValue ? $"  •  {FormatPlayWindowRemaining(currentLobby.PlayDeadlineUtc)}" : "";
        string humansOnlyPart = currentLobby.IsTeamChallenge && currentLobby.RequireFullHumanSquad ? "  •  Humans Only" : "";
        string rulesBadge = FormatLobbyRulesBadge(currentLobby.MatchFormat, currentLobby.TableStyle, currentLobby.OneOnePlayoff);
        int seatedCount = currentLobby.Players != null ? currentLobby.Players.Count : 0;
        lobbyInfoLabel.text = $"{rulesBadge}{humansOnlyPart}\nSeated: {seatedCount}/{currentLobby.MaxNumberOfPlayer}{codePart}{deadlinePart}";

        bool isHost = IsLocalPlayerTableHost();
        bool botsAllowed = !(currentLobby.IsTeamChallenge && currentLobby.RequireFullHumanSquad);
        bool isTeam2v2 = currentLobby.MaxNumberOfPlayer == 4 &&
                         (currentLobby.MatchFormat == MatchFormat.SIX_LOV || currentLobby.MatchFormat == MatchFormat.TWO_BEST);

        if (tableFeltLabel != null)
        {
            if (currentLobby.IsTeamChallenge)
            {
                string teamALabel = string.IsNullOrWhiteSpace(currentLobby.TeamAName) ? "Team A" : currentLobby.TeamAName;
                string teamBLabel = string.IsNullOrWhiteSpace(currentLobby.TeamBName) ? "Team B" : currentLobby.TeamBName;
                tableFeltLabel.text = $"TEAM CHALLENGE\n<size=17><color=#F7C948>{teamALabel}\nvs\n{teamBLabel}</color></size>";
            }
            else
            {
                string layoutHint = currentLobby.MaxNumberOfPlayer == 2
                    ? "1v1 Head-to-Head"
                    : (isTeam2v2 ? "2v2 Partners\n(Across = Padna)" : $"{currentLobby.MaxNumberOfPlayer}P Free-For-All");
                tableFeltLabel.text = $"ROATAN TABLE\n<size=19><color=#F7C948>{layoutHint}</color></size>";
            }
        }

        if (hostBotDifficultyRow != null)
        {
            hostBotDifficultyRow.SetActive(isHost && botsAllowed);
            RefreshHostBotDifficultyButtons();
        }

        // Build seat list from server Seats or fall back to Players list
        List<LobbySeatDto> seats = new List<LobbySeatDto>();
        if (currentLobby.Seats != null && currentLobby.Seats.Count == currentLobby.MaxNumberOfPlayer)
        {
            seats.AddRange(currentLobby.Seats.OrderBy(s => s.SeatIndex));
        }
        else
        {
            for (int i = 0; i < currentLobby.MaxNumberOfPlayer; i++)
            {
                PlayerDto occupant = currentLobby.Players != null && i < currentLobby.Players.Count ? currentLobby.Players[i] : null;
                seats.Add(new LobbySeatDto
                {
                    SeatIndex = i,
                    IsOccupied = occupant != null,
                    IsBot = occupant != null && occupant.IsBot,
                    BotDifficulty = occupant?.BotDifficulty,
                    Player = occupant
                });
            }
        }

        long localUserId = subscribedClient?.User != null ? subscribedClient.User.Id : -1;
        int localSeatIndex = -1;
        foreach (LobbySeatDto s in seats)
        {
            if (s.IsOccupied && !s.IsBot && s.Player != null && s.Player.Id == localUserId)
            {
                localSeatIndex = s.SeatIndex;
                break;
            }
        }

        foreach (GameObject go in spawnedSeatCards) Destroy(go);
        spawnedSeatCards.Clear();

        // Seat positions around the visual domino table diagram
        Vector2[] positions2P = { new Vector2(0f, -195f), new Vector2(0f, 195f) };
        Vector2[] positions4P = { new Vector2(0f, -205f), new Vector2(305f, 0f), new Vector2(0f, 205f), new Vector2(-305f, 0f) };
        Vector2[] seatPositions = currentLobby.MaxNumberOfPlayer == 2 ? positions2P : positions4P;

        for (int i = 0; i < seats.Count && i < seatPositions.Length; i++)
        {
            LobbySeatDto seat = seats[i];
            int capturedSeatIdx = seat.SeatIndex;
            bool isTopOrBottom = currentLobby.MaxNumberOfPlayer == 2 || i == 0 || i == 2;
            Vector2 cardSize = isTopOrBottom ? new Vector2(330f, 152f) : new Vector2(246f, 176f);

            string roleLabel;
            if (currentLobby.IsTeamChallenge)
            {
                bool isTeamASeat = (seat.SeatIndex % 2) == 0;
                string crewName = isTeamASeat
                    ? (!string.IsNullOrWhiteSpace(currentLobby.TeamAName) ? currentLobby.TeamAName : "Team A")
                    : (!string.IsNullOrWhiteSpace(currentLobby.TeamBName) ? currentLobby.TeamBName : "Team B");
                roleLabel = seat.SeatIndex == localSeatIndex
                    ? $"{crewName} (You)"
                    : $"{crewName} • Seat {seat.SeatIndex + 1}";
            }
            else if (seat.SeatIndex == localSeatIndex)
            {
                roleLabel = $"Seat {seat.SeatIndex + 1}: You";
            }
            else if (isTeam2v2 && localSeatIndex >= 0 && ((seat.SeatIndex % 2) == (localSeatIndex % 2)))
            {
                roleLabel = $"Seat {seat.SeatIndex + 1}: Padna";
            }
            else if (isTeam2v2 && localSeatIndex < 0 && seat.SeatIndex == 2)
            {
                roleLabel = $"Seat {seat.SeatIndex + 1}: Padna";
            }
            else
            {
                roleLabel = $"Seat {seat.SeatIndex + 1}: Opponent";
            }

            Color seatBg;
            if (seat.SeatIndex == localSeatIndex)
            {
                seatBg = new Color(0.10f, 0.36f, 0.30f, 0.98f);
            }
            else if (seat.IsOccupied && seat.IsBot)
            {
                seatBg = new Color(0.16f, 0.25f, 0.34f, 0.96f);
            }
            else if (seat.IsOccupied)
            {
                seatBg = new Color(0.10f, 0.26f, 0.34f, 0.96f);
            }
            else
            {
                seatBg = new Color(0.05f, 0.14f, 0.20f, 0.92f);
            }

            GameObject seatCard = CreateCard(tableDiagramRoot, $"SeatCard_{seat.SeatIndex}", seatBg, seatPositions[i], cardSize);
            CreateText(seatCard.transform, "Role", roleLabel, 18, FontStyles.Bold, RoatanVisualAssets.IslandGold, new Vector2(0f, cardSize.y * 0.5f - 24f), new Vector2(cardSize.x - 16f, 28f));

            if (seat.IsOccupied && seat.Player != null)
            {
                string occupantTitle = seat.IsBot
                    ? $"{seat.Player.Name}"
                    : (seat.Player.Id == localUserId ? $"{seat.Player.Name} (You)" : seat.Player.Name);
                string seatRankBadge = RoatanRankTierHelper.FormatRichBadge(ResolveDisplayElo(seat.Player.Elo));
                string subLine = seat.IsBot
                    ? $"Bot • {seat.BotDifficulty ?? "Pro"}"
                    : (seat.IsReady ? $"{seatRankBadge} • READY" : seatRankBadge);

                // Circular seat avatar thumbnail
                GameObject seatAvObj = new GameObject("SeatAvatar");
                seatAvObj.transform.SetParent(seatCard.transform, false);
                RectTransform seatAvRect = seatAvObj.AddComponent<RectTransform>();
                float avSize = isTopOrBottom ? 54f : 46f;
                float avX = -cardSize.x * 0.5f + avSize * 0.5f + 14f;
                float avY = seat.IsBot && isHost ? 4f : -6f;
                seatAvRect.anchoredPosition = new Vector2(avX, avY);
                seatAvRect.sizeDelta = new Vector2(avSize, avSize);
                Image seatAvImg = seatAvObj.AddComponent<Image>();
                seatAvImg.raycastTarget = false;
                TMP_Text seatAvInit = CreateText(seatAvObj.transform, "Init", "?", isTopOrBottom ? 20 : 17, FontStyles.Bold, Color.white, Vector2.zero, new Vector2(avSize - 6f, avSize - 10f));
                seatAvInit.raycastTarget = false;
                ApplyCircularAvatar(seatAvImg, seatAvInit, $"seat_{seat.Player.Id}", seat.Player.AvatarBase64, seat.Player.Name);

                float textLeftPad = avSize + 24f;
                float textW = cardSize.x - textLeftPad - 12f;
                float textX = textLeftPad * 0.5f - 4f;
                TMP_Text occNameTxt = CreateText(seatCard.transform, "OccName", occupantTitle, isTopOrBottom ? 20 : 18, FontStyles.Bold, Color.white, new Vector2(textX, avY + 12f), new Vector2(textW, 30f));
                occNameTxt.alignment = TextAlignmentOptions.MidlineLeft;
                TMP_Text occSubTxt = CreateText(seatCard.transform, "OccSub", subLine, isTopOrBottom ? 16 : 15, FontStyles.Normal, seat.IsReady ? new Color(0.45f, 0.95f, 0.65f) : new Color(0.80f, 0.92f, 0.96f), new Vector2(textX, avY - 14f), new Vector2(textW, 24f));
                occSubTxt.alignment = TextAlignmentOptions.MidlineLeft;

                if (seat.IsBot && isHost)
                {
                    CreatePillButton(seatCard.transform, "BtnRemoveBot", "Remove Bot", RoatanVisualAssets.SunsetCoral, new Vector2(0f, -cardSize.y * 0.5f + 26f), new Vector2(cardSize.x - 36f, 40f), 17, () =>
                    {
                        RequestRemoveBot(capturedSeatIdx);
                    });
                }
            }
            else
            {
                CreateText(seatCard.transform, "EmptyLbl", "Open Seat", 19, FontStyles.Italic, new Color(0.68f, 0.80f, 0.85f), new Vector2(0f, 12f), new Vector2(cardSize.x - 20f, 28f));

                if (isHost && botsAllowed)
                {
                    float btnW = isTopOrBottom ? 142f : 108f;
                    float offsetX = isTopOrBottom ? 76f : 56f;
                    CreatePillButton(seatCard.transform, "BtnSwitchSeat", "Switch", RoatanVisualAssets.BrightLagoon, new Vector2(-offsetX, -cardSize.y * 0.5f + 32f), new Vector2(btnW, 46f), 17, () =>
                    {
                        RequestSwitchSeat(capturedSeatIdx);
                    });
                    CreatePillButton(seatCard.transform, "BtnAddBot", "+ Add Bot", RoatanVisualAssets.PalmGreen, new Vector2(offsetX, -cardSize.y * 0.5f + 32f), new Vector2(btnW, 46f), 17, () =>
                    {
                        RequestAddBot(capturedSeatIdx);
                    });
                }
                else
                {
                    CreatePillButton(seatCard.transform, "BtnSwitchSeat", "Switch Seat", RoatanVisualAssets.BrightLagoon, new Vector2(0f, -cardSize.y * 0.5f + 32f), new Vector2(cardSize.x - 36f, 46f), 18, () =>
                    {
                        RequestSwitchSeat(capturedSeatIdx);
                    });
                }
            }

            spawnedSeatCards.Add(seatCard);
        }

        // Enforce Minimum 2 Real Human Players for Online Matches
        int humanCount = currentLobby.Players != null ? currentLobby.Players.Count(p => !p.IsBot) : 0;
        bool hasEnoughHumans = humanCount >= 2;
        bool allFilled = seatedCount >= currentLobby.MaxNumberOfPlayer;
        bool canStart = allFilled && hasEnoughHumans;

        if (lobbyTimerLabel != null)
        {
            if (!hasEnoughHumans)
            {
                lobbyTimerLabel.text = $"Need at least 2 real players to start online ({humanCount}/2 humans seated) — use Offline Mode for solo vs bots";
                lobbyTimerLabel.color = RoatanVisualAssets.IslandGold;
            }
            else if (!allFilled)
            {
                lobbyTimerLabel.text = "Tap an open seat to switch, add bots, or Ready up!";
                lobbyTimerLabel.color = Color.white;
            }
            else
            {
                lobbyTimerLabel.text = isHost
                    ? "All seats filled — tap Start Match when ready!"
                    : "All seats filled — waiting for Host to start match...";
                lobbyTimerLabel.color = new Color(0.45f, 0.95f, 0.65f);
            }
        }

        // Host Start Match Button vs Ready Button layout
        if (startMatchBtn != null)
        {
            startMatchBtn.gameObject.SetActive(isHost);
            startMatchBtn.interactable = canStart;
            if (startMatchBtnImg != null)
            {
                startMatchBtnImg.color = canStart ? RoatanVisualAssets.PalmGreen : new Color(0.22f, 0.30f, 0.34f, 0.90f);
            }
            if (startMatchBtnLabel != null)
            {
                if (!hasEnoughHumans)
                {
                    startMatchBtnLabel.text = $"Need 2+ Humans ({humanCount}/2)";
                }
                else if (!allFilled)
                {
                    startMatchBtnLabel.text = $"Fill Seats ({seatedCount}/{currentLobby.MaxNumberOfPlayer})";
                }
                else
                {
                    startMatchBtnLabel.text = "Start Match";
                }
            }
        }

        if (readyBtnObj != null)
        {
            RectTransform rr = readyBtnObj.GetComponent<RectTransform>();
            rr.anchoredPosition = isHost ? new Vector2(265f, -402f) : new Vector2(0f, -402f);
            rr.sizeDelta = isHost ? new Vector2(310f, 94f) : new Vector2(560f, 94f);
        }

        RefreshLobbyReadyButton();
    }

    private TMP_Text CreateFieldTitle(Transform parent, string name, string title, Vector2 pos)
    {
        TMP_Text lbl = CreateText(parent, name, title, 21, FontStyles.Bold, RoatanVisualAssets.IslandGold, pos, new Vector2(820f, 30f));
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        return lbl;
    }

    private GameObject CreateVerticalScrollList(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax, out RectTransform contentRect)
    {
        GameObject scrollObj = new GameObject(name);
        scrollObj.transform.SetParent(parent, false);
        RectTransform srRect = scrollObj.AddComponent<RectTransform>();
        srRect.anchorMin = Vector2.zero;
        srRect.anchorMax = Vector2.one;
        srRect.offsetMin = offsetMin;
        srRect.offsetMax = offsetMax;
        scrollObj.AddComponent<RectMask2D>();
        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contentRect;
        sr.viewport = srRect;
        return scrollObj;
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
        if (refreshLobbiesBtnLabel != null) refreshLobbiesBtnLabel.text = "Refresh";
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

    // ==================== SERVER ENDPOINT & URL NORMALIZATION ====================

    public static string GetLocalhostServerUrl()
    {
        return Application.platform == RuntimePlatform.Android
            ? "http://10.0.2.2:5000"
            : "http://localhost:5000";
    }

    public static string GetPresetUrl(RoatanServerPreset preset)
    {
        switch (preset)
        {
            case RoatanServerPreset.Production:
                return ProductionServerUrl;
            case RoatanServerPreset.CustomTunnel:
                return PlayerPrefs.GetString(PrefCustomServerUrlKey, string.Empty).Trim();
            case RoatanServerPreset.Localhost:
            default:
                return GetLocalhostServerUrl();
        }
    }

    public static RoatanServerPreset GetSavedPreset()
    {
        int raw = PlayerPrefs.GetInt(PrefServerPresetKey, (int)RoatanServerPreset.Localhost);
        if (raw < 0 || raw > 2) return RoatanServerPreset.Localhost;
        return (RoatanServerPreset)raw;
    }

    public static string GetSavedServerUrl()
    {
        string saved = PlayerPrefs.GetString(PrefServerUrlKey, string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(saved))
        {
            return saved;
        }

        // Graceful fallback if only legacy host/port PlayerPrefs existed
        if (PlayerPrefs.HasKey(PrefHostKey))
        {
            string legacyHost = PlayerPrefs.GetString(PrefHostKey, GetDefaultHost()).Trim();
            int legacyPort = PlayerPrefs.GetInt(PrefPortKey, 5000);
            if (!string.IsNullOrWhiteSpace(legacyHost))
            {
                return legacyHost.Contains("://")
                    ? legacyHost
                    : $"http://{legacyHost}:{(legacyPort > 0 ? legacyPort : 5000)}";
            }
        }

        RoatanServerPreset preset = GetSavedPreset();
        string presetUrl = GetPresetUrl(preset);
        return string.IsNullOrWhiteSpace(presetUrl) ? GetLocalhostServerUrl() : presetUrl;
    }

    public static RoatanServerPreset DetectPresetForUrl(string rawUrl, RoatanServerPreset fallback)
    {
        string normalized = NormalizeDisplayUrl(rawUrl);
        if (string.Equals(normalized, NormalizeDisplayUrl(ProductionServerUrl), StringComparison.OrdinalIgnoreCase))
        {
            return RoatanServerPreset.Production;
        }

        if (string.Equals(normalized, NormalizeDisplayUrl(GetLocalhostServerUrl()), StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "http://localhost:5000", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "http://127.0.0.1:5000", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "http://10.0.2.2:5000", StringComparison.OrdinalIgnoreCase))
        {
            return RoatanServerPreset.Localhost;
        }

        return string.IsNullOrWhiteSpace(rawUrl) ? fallback : RoatanServerPreset.CustomTunnel;
    }

    public static void SaveServerEndpoint(RoatanServerPreset preset, string rawUrl)
    {
        string trimmed = string.IsNullOrWhiteSpace(rawUrl) ? GetPresetUrl(preset) : rawUrl.Trim();
        string displayUrl = NormalizeDisplayUrl(trimmed);

        PlayerPrefs.SetInt(PrefServerPresetKey, (int)preset);
        PlayerPrefs.SetString(PrefServerUrlKey, displayUrl);
        if (preset == RoatanServerPreset.CustomTunnel && !string.IsNullOrWhiteSpace(displayUrl))
        {
            PlayerPrefs.SetString(PrefCustomServerUrlKey, displayUrl);
        }

        if (TryResolveServerEndpoint(displayUrl, out Uri wsUri, out _, out bool isTcp, out string tcpHost, out int tcpPort, out _, out _))
        {
            PlayerPrefs.SetString(PrefHostKey, isTcp ? tcpHost : wsUri.Host);
            PlayerPrefs.SetInt(PrefPortKey, isTcp ? tcpPort : wsUri.Port);
        }

        PlayerPrefs.Save();
        ServerEndpointChanged?.Invoke();
    }

    public static string NormalizeDisplayUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return GetLocalhostServerUrl();
        }

        string candidate = rawUrl.Trim().TrimEnd('/');
        if (!candidate.Contains("://"))
        {
            bool local = IsLocalOrLanHost(candidate);
            candidate = (local ? "http://" : "https://") + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri parsed))
        {
            return rawUrl.Trim().TrimEnd('/');
        }

        UriBuilder ub = new UriBuilder(parsed);
        if ((string.Equals(ub.Scheme, "https", StringComparison.OrdinalIgnoreCase) || string.Equals(ub.Scheme, "wss", StringComparison.OrdinalIgnoreCase)) && ub.Port == 443)
        {
            ub.Port = -1;
        }
        else if ((string.Equals(ub.Scheme, "http", StringComparison.OrdinalIgnoreCase) || string.Equals(ub.Scheme, "ws", StringComparison.OrdinalIgnoreCase)) && ub.Port == 80)
        {
            ub.Port = -1;
        }
        else if (IsLocalOrLanHost(ub.Host) && parsed.IsDefaultPort && !rawUrl.Contains(":"))
        {
            ub.Port = 5000;
        }

        string path = ub.Path?.TrimEnd('/') ?? string.Empty;
        ub.Path = string.IsNullOrEmpty(path) ? string.Empty : path;
        return ub.Uri.ToString().TrimEnd('/');
    }

    /// <summary>
    /// Normalizes any https://, wss://, http://, ws://, bare domain (e.g. Cloudflare Tunnel),
    /// or legacy tcp:// endpoint into a ready-to-connect WebSocket URI or TCP target.
    /// </summary>
    public static bool TryResolveServerEndpoint(
        string rawUrl,
        out Uri wsUri,
        out string normalizedDisplayUrl,
        out bool isLegacyTcp,
        out string tcpHost,
        out int tcpPort,
        out bool tcpUseSsl,
        out string errorMessage)
    {
        wsUri = null;
        normalizedDisplayUrl = string.Empty;
        isLegacyTcp = false;
        tcpHost = string.Empty;
        tcpPort = 0;
        tcpUseSsl = false;
        errorMessage = string.Empty;

        string trimmed = string.IsNullOrWhiteSpace(rawUrl) ? GetLocalhostServerUrl() : rawUrl.Trim();
        trimmed = trimmed.TrimEnd('/');

        // Explicit legacy TCP scheme (tcp://host:port or tcps://host:port)
        if (trimmed.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("tcps://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri tcpUri) || string.IsNullOrWhiteSpace(tcpUri.Host))
            {
                errorMessage = "Malformed TCP endpoint.";
                return false;
            }

            isLegacyTcp = true;
            tcpHost = tcpUri.Host;
            tcpPort = tcpUri.Port > 0 ? tcpUri.Port : 10000;
            tcpUseSsl = trimmed.StartsWith("tcps://", StringComparison.OrdinalIgnoreCase);
            normalizedDisplayUrl = $"{(tcpUseSsl ? "tcps" : "tcp")}://{tcpHost}:{tcpPort}";
            return true;
        }

        string withScheme = trimmed;
        bool hadExplicitPort = false;
        if (!withScheme.Contains("://"))
        {
            hadExplicitPort = withScheme.Contains(":");
            bool local = IsLocalOrLanHost(withScheme);
            withScheme = (local ? "http://" : "https://") + withScheme;
        }
        else
        {
            int schemeEnd = withScheme.IndexOf("://", StringComparison.Ordinal) + 3;
            string authority = withScheme.Substring(schemeEnd);
            int slashIdx = authority.IndexOf('/');
            if (slashIdx >= 0) authority = authority.Substring(0, slashIdx);
            hadExplicitPort = authority.Contains(":");
        }

        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out Uri parsed) || string.IsNullOrWhiteSpace(parsed.Host))
        {
            errorMessage = "Enter a valid https://, wss://, or http:// URL.";
            return false;
        }

        string lowerScheme = parsed.Scheme.ToLowerInvariant();
        if (lowerScheme != "https" && lowerScheme != "http" && lowerScheme != "wss" && lowerScheme != "ws")
        {
            errorMessage = $"Unsupported scheme '{parsed.Scheme}'. Use https:// or wss://.";
            return false;
        }

        bool isSecure = lowerScheme == "https" || lowerScheme == "wss";
        string wsScheme = isSecure ? "wss" : "ws";

        UriBuilder wsBuilder = new UriBuilder(parsed)
        {
            Scheme = wsScheme
        };

        // Strip redundant default ports (:443 for wss, :80 for ws); default local hosts without port to :5000
        if (isSecure && (parsed.IsDefaultPort || wsBuilder.Port == 443 || wsBuilder.Port == 80))
        {
            wsBuilder.Port = -1;
        }
        else if (!isSecure && !hadExplicitPort && IsLocalOrLanHost(parsed.Host))
        {
            wsBuilder.Port = 5000;
        }
        else if (!isSecure && (parsed.IsDefaultPort || wsBuilder.Port == 80))
        {
            wsBuilder.Port = -1;
        }

        string cleanPath = (wsBuilder.Path ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrEmpty(cleanPath))
        {
            wsBuilder.Path = "/ws";
        }
        else
        {
            wsBuilder.Path = cleanPath;
        }

        wsUri = wsBuilder.Uri;

        UriBuilder displayBuilder = new UriBuilder(wsUri)
        {
            Scheme = lowerScheme == "ws" || lowerScheme == "wss" ? lowerScheme : (isSecure ? "https" : "http"),
            Path = string.Equals(cleanPath, "/ws", StringComparison.OrdinalIgnoreCase) ? string.Empty : cleanPath
        };
        if ((isSecure && displayBuilder.Port == 443) || (!isSecure && displayBuilder.Port == 80))
        {
            displayBuilder.Port = -1;
        }
        normalizedDisplayUrl = displayBuilder.Uri.ToString().TrimEnd('/');
        return true;
    }

    private static bool IsLocalOrLanHost(string hostOrAuthority)
    {
        if (string.IsNullOrWhiteSpace(hostOrAuthority)) return true;
        string hostOnly = hostOrAuthority.Trim();
        int slashIdx = hostOnly.IndexOf('/');
        if (slashIdx >= 0) hostOnly = hostOnly.Substring(0, slashIdx);
        int colonIdx = hostOnly.IndexOf(':');
        if (colonIdx >= 0) hostOnly = hostOnly.Substring(0, colonIdx);

        if (string.Equals(hostOnly, "localhost", StringComparison.OrdinalIgnoreCase) ||
            hostOnly == "127.0.0.1" ||
            hostOnly == "10.0.2.2")
        {
            return true;
        }

        return hostOnly.StartsWith("192.168.", StringComparison.Ordinal) ||
               hostOnly.StartsWith("10.", StringComparison.Ordinal) ||
               hostOnly.StartsWith("172.16.", StringComparison.Ordinal) ||
               hostOnly.StartsWith("172.17.", StringComparison.Ordinal) ||
               hostOnly.StartsWith("172.18.", StringComparison.Ordinal) ||
               hostOnly.StartsWith("172.19.", StringComparison.Ordinal) ||
               hostOnly.StartsWith("172.2", StringComparison.Ordinal) ||
               hostOnly.StartsWith("172.30.", StringComparison.Ordinal) ||
               hostOnly.StartsWith("172.31.", StringComparison.Ordinal);
    }

    private void SelectServerPresetInAuthView(RoatanServerPreset preset)
    {
        activeServerPreset = preset;
        string targetUrl = GetPresetUrl(preset);

        if (serverUrlInput != null)
        {
            suppressServerInputEvents = true;
            serverUrlInput.text = targetUrl;
            suppressServerInputEvents = false;
        }

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            SaveServerEndpoint(preset, targetUrl);
        }
        else
        {
            PlayerPrefs.SetInt(PrefServerPresetKey, (int)preset);
            PlayerPrefs.Save();
            RefreshServerPresetVisuals();
            UpdateServerEndpointPreview(targetUrl);
        }
    }

    private void OnAuthServerUrlChanged(string newText)
    {
        if (suppressServerInputEvents) return;
        activeServerPreset = DetectPresetForUrl(newText, RoatanServerPreset.CustomTunnel);
        RefreshServerPresetVisuals();
        UpdateServerEndpointPreview(newText);
    }

    private void SyncServerEndpointFieldsFromPrefs()
    {
        activeServerPreset = GetSavedPreset();
        string savedUrl = GetSavedServerUrl();
        if (serverUrlInput != null)
        {
            suppressServerInputEvents = true;
            serverUrlInput.text = savedUrl;
            suppressServerInputEvents = false;
        }
        RefreshServerPresetVisuals();
        UpdateServerEndpointPreview(savedUrl);
    }

    private void RefreshServerPresetVisuals()
    {
        foreach (KeyValuePair<RoatanServerPreset, Image> kv in serverPresetBtns)
        {
            if (kv.Value == null) continue;
            bool selected = kv.Key == activeServerPreset;
            kv.Value.color = selected ? RoatanVisualAssets.IslandGold : new Color(0.14f, 0.22f, 0.28f, 1f);
            TMP_Text lbl = kv.Value.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.color = selected ? new Color(0.08f, 0.10f, 0.12f, 1f) : Color.white;
            }
        }
    }

    private void UpdateServerEndpointPreview(string rawUrl)
    {
        if (serverEndpointPreviewLabel == null) return;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            serverEndpointPreviewLabel.text = "Paste your Cloudflare Tunnel URL (e.g. https://random-words.trycloudflare.com)";
            serverEndpointPreviewLabel.color = new Color(0.75f, 0.90f, 0.94f);
            return;
        }

        if (TryResolveServerEndpoint(rawUrl, out Uri wsUri, out _, out bool isTcp, out string tcpHost, out int tcpPort, out bool tcpSsl, out string err))
        {
            serverEndpointPreviewLabel.text = isTcp
                ? $"Endpoint: {(tcpSsl ? "tcps" : "tcp")}://{tcpHost}:{tcpPort}"
                : $"WebSocket: {wsUri}";
            serverEndpointPreviewLabel.color = new Color(0.68f, 0.95f, 0.80f);
        }
        else
        {
            serverEndpointPreviewLabel.text = err;
            serverEndpointPreviewLabel.color = RoatanVisualAssets.SunsetCoral;
        }
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
