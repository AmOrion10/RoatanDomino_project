using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private List<GamePlayerItem> gamePlayers;
    [SerializeField] private HandManager handManager;
    [SerializeField] private GameStartTimer gameStartTimer;
    [SerializeField] private GameGrid gameGrid;
    [SerializeField] private PlayerRankings playerRankings;
    [SerializeField] private GameTimer gameTimer;
    [SerializeField] private OtherProfile otherProfile;

    private IClient client;

    private LobbyDto lobby;
    private long userIdPlaying = -1;

    private void Start()
    {
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[MenuPrincipal.IdLanguageUsed];

        playerRankings.gameObject.SetActive(false);

        if (InLobby.Lobby == null)
        {
            if (OfflineModeUI.Instance != null)
            {
                OfflineModeUI.Instance.OpenSetupModal();
            }
            return;
        }

        client = Networking.Instance.GetClient();
        SetUpGameFromLobby(InLobby.Lobby);

        client.AddToSendQueue(MessageFrameBuilder.ReadyInGameFrame());
        client.EventHandlers.GameStartingTimerEvent += GameStartingTimerCallback;
        client.EventHandlers.DominoDistrubitionEvent += DominoDistrubitionCallback;
        client.EventHandlers.ChangePlayerTurnEvent += ChangePlayerTurnCallback;
        client.EventHandlers.GameGeneralTimerEvent += GameGeneralTimerCallback;
        client.EventHandlers.PlayerPlaysEvent += PlayerPlaysCallback;
        client.EventHandlers.PlayerPlaysResponseEvent += PlayerPlaysResponseCallback;
        client.EventHandlers.PossiblesMovesEvent += PossiblesMovesCallback;
        client.EventHandlers.NumberOfDominoPerPlayerEvent += NumberOfDominoPerPlayerCallback;
        client.EventHandlers.EndGameEvent += EndGameCallback;
        client.EventHandlers.PlayerLeftGameEvent += PlayerLeftGameCallback;
        client.EventHandlers.LeaveGameResponseEvent += LeaveGameCallback;
        client.EventHandlers.GetOtherUserInfoResponseEvent += GetOtherUserInfoCallback;
        gameGrid.PossibleMoveSelectedEvent += PossibleMoveSelectedCallback;
        handManager.DominoSelectedEvent += DominoSelectedCallback;

        for (int i = 1; i < gamePlayers.Count; i++)
        {
            GamePlayerItem gamePlayer = gamePlayers[i];
            gamePlayer.ClickOnProfileCallback += ClickOnGamePlayerProfileCallback;
        }

        gameGrid.SetHand(handManager);
    }

    private void GetOtherUserInfoCallback(ResponseObjectDto<OtherUserInfoDto> dto)
    {
        if (dto.OkResult)
        {
            otherProfile.SetUp(dto.Object);
            otherProfile.gameObject.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        if (client != null)
        {
            client.EventHandlers.GameStartingTimerEvent -= GameStartingTimerCallback;
            client.EventHandlers.DominoDistrubitionEvent -= DominoDistrubitionCallback;
            client.EventHandlers.ChangePlayerTurnEvent -= ChangePlayerTurnCallback;
            client.EventHandlers.GameGeneralTimerEvent -= GameGeneralTimerCallback;
            client.EventHandlers.PlayerPlaysEvent -= PlayerPlaysCallback;
            client.EventHandlers.PlayerPlaysResponseEvent -= PlayerPlaysResponseCallback;
            client.EventHandlers.PossiblesMovesEvent -= PossiblesMovesCallback;
            client.EventHandlers.NumberOfDominoPerPlayerEvent -= NumberOfDominoPerPlayerCallback;
            client.EventHandlers.EndGameEvent -= EndGameCallback;
            client.EventHandlers.PlayerLeftGameEvent -= PlayerLeftGameCallback;
            client.EventHandlers.LeaveGameResponseEvent -= LeaveGameCallback;
            client.EventHandlers.GetOtherUserInfoResponseEvent -= GetOtherUserInfoCallback;
        }
        gameGrid.PossibleMoveSelectedEvent -= PossibleMoveSelectedCallback;
        handManager.DominoSelectedEvent -= DominoSelectedCallback;
    }

    private void LeaveGameCallback(ResponseDto dto)
    {
        if (!dto.OkResult)
        {
            Debug.LogError($"LeaveGameCallback failed : {dto.ErrorType}");
        }
        if (Networking.Instance != null && Networking.Instance.IsOfflineMode)
        {
            Networking.Instance.StopOfflineMode();
            SceneManager.LoadScene(0);
            return;
        }
        SceneManager.LoadScene(1);
    }

    private void GameGeneralTimerCallback(TimerDto dto)
    {
        gameTimer.StartTimer(dto.Duration);
    }

    private void PlayerLeftGameCallback(PlayerDto dto)
    {
        lobby.Players.Remove(lobby.Players.First(p => p.Id == dto.Id));

        foreach (GamePlayerItem player in gamePlayers)
        {
            if (player.Player == null || player.Player.Id == client.User!.Id) continue;
            player.gameObject.SetActive(false);
            player.IsActive = false;
        }
        SetUpGameFromLobby(lobby);
    }

    private void ClickOnGamePlayerProfileCallback(PlayerDto dto)
    {
        client.AddToSendQueue(MessageFrameBuilder.GetOtherUserInfoFrame(new GetOtherUserInfoDto()
        {
            Login = dto.Name
        }));
    }

    private void GameStartingTimerCallback(TimerDto dto)
    {
        gameStartTimer.gameObject.SetActive(true);
        gameStartTimer.StartTimer(dto);
    }

    private void DominoDistrubitionCallback(HandDto dto)
    {
        handManager.SetUpHand(dto);
    }

    private void PlayerPlaysCallback(PlayerPlayDto dto)
    {
        if (dto.PlayActionType == PlayActionType.PlaceDomino)
        {
            if (dto.IdUserPlayed == client.User!.Id)
            {
                gameGrid.MoveDomino(dto.Moves);
                gameGrid.ClearPossiblesMoves();
            }
            else
            {
                gameGrid.PlaceDomino(dto.Moves);
            }
            gameGrid.CachePossiblesMoves(null);
        }
        else if (dto.PlayActionType == PlayActionType.Draw)
        {
            //TODO: animation si le player qui draw n'est pas nous

            if (dto.IdUserPlayed == client.User!.Id && dto.Moves != null && dto.Moves.Domino != null)
            {
                handManager.AddDomino(dto.Moves.Domino);
            }
        }
    }

    private void ChangePlayerTurnCallback(PlayerTurnDto dto)
    {
        if (userIdPlaying == -1) // First time setting userIdPlaying
        {
            userIdPlaying = dto.IdUserPlaying;
            gamePlayers.First(gp => gp.Player.Id == userIdPlaying).StartTurn(dto.Timer.Duration);
        }
        else
        {
            gamePlayers.First(gp => gp.Player.Id == userIdPlaying).StopTurn();
            userIdPlaying = dto.IdUserPlaying;
            gamePlayers.First(gp => gp.Player.Id == userIdPlaying).StartTurn(dto.Timer.Duration);
            gameGrid.CachePossiblesMoves(null);
            gameGrid.ClearPossiblesMoves();
            handManager.ClearDominoOutlines();
        }
    }

    private void PossibleMoveSelectedCallback(MoveDto move)
    {
        client.AddToSendQueue(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
        {
            IdUserPlayed = client.User!.Id,
            Moves = move,
            PlayActionType = PlayActionType.PlaceDomino
        }));
    }

    private void PossiblesMovesCallback(PossibleMovesDto dto)
    {
        gameGrid.CachePossiblesMoves(dto);
        HashSet<DominoDto> dominoAlreadyPutInEvidence = new();
        foreach (MoveDto move in dto.Moves)
        {
            if (dominoAlreadyPutInEvidence.Add(move.Domino))
            {
                handManager.PutDominoInEvidence(move.Domino);
            }
        }
    }

    private void DominoSelectedCallback(DominoDto domino)
    {
        gameGrid.ShowPossiblesMovesForDomino(domino);
    }

    private void NumberOfDominoPerPlayerCallback(NumberOfDominoPerPlayerDto dto)
    {
        foreach (GamePlayerItem player in gamePlayers)
        {
            if (!player.IsActive || player.Player.Id == client.User!.Id) continue;
            player.SetNumberOfDomino(dto.NumberOfDomino[player.Player.Id]);
        }
    }

    private void EndGameCallback(EndGameDto dto)
    {
        // We stop every timer
        foreach (GamePlayerItem player in gamePlayers)
        {
            if (player.IsActive)
            {
                player.StopTurn();
            }
        }

        playerRankings.gameObject.SetActive(true);
        playerRankings.ShowRankings(lobby, dto);
    }

    private void PlayerPlaysResponseCallback(ResponseDto dto)
    {
        if (!dto.OkResult)
        {
            Debug.LogError($"Player plays response event error : {dto.ErrorType}");
        }
    }

    private void SetUpGameFromLobby(LobbyDto lobbyDto)
    {
        lobby = lobbyDto;
        int playerIndex = 1;
        foreach (PlayerDto player in lobby.Players)
        {
            if (player.Id == client.User!.Id)
            {
                gamePlayers[0].SetUpPlayer(player);
            }
            else
            {
                gamePlayers[playerIndex].SetUpPlayer(player);
                gamePlayers[playerIndex].gameObject.SetActive(true);
                playerIndex++;
            }
        }
    }

    public void Draw()
    {
        if (client.User!.Id != userIdPlaying) return; // We don't want to draw if we are not player playing

        client.AddToSendQueue(MessageFrameBuilder.PlayerPlaysFrame(new PlayerPlayDto
        {
            IdUserPlayed = client.User!.Id,
            PlayActionType = PlayActionType.Draw
        }));
    }

    public void QuitGame()
    {
        client.AddToSendQueue(MessageFrameBuilder.LeaveGameFrame());
    }

    public void QuitScene()
    {
        if (Networking.Instance != null && Networking.Instance.IsOfflineMode)
        {
            Networking.Instance.StopOfflineMode();
            SceneManager.LoadScene(0);
            return;
        }
        SceneManager.LoadScene(1);
    }
}
