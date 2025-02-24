using System.Collections;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using TMPro;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InLobby : MonoBehaviour
{
    [SerializeField] private float timeBetweenReadyClick = 1f;
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text lobbyVisibilityText;
    [SerializeField] private TMP_Text lobbyMaxNumberOfPlayerText;
    [SerializeField] private TMP_Text lobbyCreatorText;
    [SerializeField] private TMP_Text lobbyStateText;
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private TMP_Text numberOfPlayerReadyText;

    [SerializeField] private GameObject lobbyPlayersContainer;
    [SerializeField] private LobbyPlayerListItem lobbyPlayer;
    [SerializeField] private GameObject lobbyPlayerPrefab;
    [SerializeField] private Button readyButton;
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private OtherProfile otherProfile;

    public static LobbyDto Lobby { get; private set; }

    private TMP_Text readyButtonText;

    private IClient client;
    private readonly List<LobbyPlayerListItem> playersListItem = new();
    private bool isReady;
    private int timer;

    private void Start()
    {
        if (!playersListItem.Contains(lobbyPlayer)) playersListItem.Add(lobbyPlayer);
        readyButtonText = readyButton.GetComponentInChildren<TMP_Text>();

        client ??= Networking.Instance.GetClient();
        client.EventHandlers.PlayerJoinedLobbyEvent += PlayerJoinedLobbyCallback;
        client.EventHandlers.PlayerLeftLobbyEvent += PlayerLeftLobbyCallback;
        client.EventHandlers.LobbyCreatorChangedEvent += LobbyCreatorChangedCallback;
        client.EventHandlers.LobbyPlayerReadyStateChangedEvent += LobbyPlayerReadyStateChangedCallback;
        client.EventHandlers.LobbyTimerUpdatedEvent += LobbyTimerUpdatedCallback;
        client.EventHandlers.LobbyTimerExpiredEvent += LobbyTimerExpiredCallback;
        client.EventHandlers.GetOtherUserInfoResponseEvent += GetOtherUserInfoCallback;
        client.EventHandlers.SendFriendRequestResponseEvent += SendFriendResponseCallback;
    }

    private void OnDestroy()
    {
        client.EventHandlers.PlayerJoinedLobbyEvent -= PlayerJoinedLobbyCallback;
        client.EventHandlers.PlayerLeftLobbyEvent -= PlayerLeftLobbyCallback;
        client.EventHandlers.LobbyCreatorChangedEvent -= LobbyCreatorChangedCallback;
        client.EventHandlers.LobbyPlayerReadyStateChangedEvent -= LobbyPlayerReadyStateChangedCallback;
        client.EventHandlers.LobbyTimerUpdatedEvent -= LobbyTimerUpdatedCallback;
        client.EventHandlers.LobbyTimerExpiredEvent -= LobbyTimerExpiredCallback;
        client.EventHandlers.GetOtherUserInfoResponseEvent -= GetOtherUserInfoCallback;
        client.EventHandlers.SendFriendRequestResponseEvent -= SendFriendResponseCallback;
    }

    private void GetOtherUserInfoCallback(ResponseObjectDto<OtherUserInfoDto> dto)
    {
        if (dto.OkResult)
        {
            otherProfile.SetUp(dto.Object);
            otherProfile.gameObject.SetActive(true);
        }
    }

    private void PlayerJoinedLobbyCallback(PlayerDto dto)
    {
        playersListItem.Add(CreateLobbyPlayerListItem(dto));
        Lobby.Players.Add(dto);
    }

    private void PlayerLeftLobbyCallback(PlayerDto dto)
    {
        foreach (LobbyPlayerListItem lobbyPlayerListItem in playersListItem.ToList())
        {
            if (lobbyPlayerListItem.PlayerDto.Id != dto.Id) continue;
            playersListItem.Remove(lobbyPlayerListItem);
            lobbyPlayerListItem.AddFriendEvent -= AddFriendCallback;
            lobbyPlayerListItem.ShowProfileEvent -= ShowProfileCallback;
            Lobby.Players.Remove(Lobby.Players.First(p => p.Id == dto.Id));
            Destroy(lobbyPlayerListItem.gameObject);
        }
    }

    private void LobbyCreatorChangedCallback(PlayerDto dto)
    {
        lobbyCreatorText.text = $"{Translation.GetTranslation(Translation.StringType.LobbyCreator)} : {dto.Name}";
    }

    private void LobbyTimerUpdatedCallback(TimerDto dto)
    {
        StopCoroutine(nameof(LobbyTimerCoroutine));
        if (dto.HasStarted)
        {
            timer = dto.Duration;
            lobbyStateText.text = $"{Translation.GetTranslation(Translation.StringType.BeginsIn)} {dto.Duration} {Translation.GetTranslation(Translation.StringType.Seconds)}";
            StartCoroutine(nameof(LobbyTimerCoroutine));
        }
        else
        {
            lobbyStateText.text = Translation.GetTranslation(Translation.StringType.Waiting);
        }
    }

    private void LobbyPlayerReadyStateChangedCallback(PlayerReadyDto dto)
    {
        foreach (LobbyPlayerListItem lobbyPlayerListItem in playersListItem)
        {
            if (lobbyPlayerListItem.PlayerDto.Id != dto.PlayerId) continue;
            lobbyPlayerListItem.PlayerReadyChanged(dto.IsReady);
            numberOfPlayerReadyText.text = $"{playersListItem.Count(p => p.IsReady)} {Translation.GetTranslation(Translation.StringType.PlayersReady)}";
        }
    }

    private void LobbyTimerExpiredCallback()
    {
        SceneManager.LoadScene(2);
    }

    private void SendFriendResponseCallback(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            string message = Translation.GetTranslation(Translation.StringType.FriendRequest);
            popupManager.ShowMessage($"{message}", Color.green);
        }
        else
        {
            string error = Translation.GetTranslationError(dto.ErrorType!.Value);
            string message = Translation.GetTranslation(Translation.StringType.FriendRequestError);
            popupManager.ShowMessage($"{message} : {error}", Color.green);
        }
    }

    private void SetTextInfo(LobbyDto lobby)
    {
        lobbyNameText.text = lobby.Name;
        lobbyVisibilityText.text = lobby.IsPublic ? $"{Translation.GetTranslation(Translation.StringType.PublicLobby)}" : $"{Translation.GetTranslation(Translation.StringType.PrivateLobby)}";
        lobbyMaxNumberOfPlayerText.text = $"{Translation.GetTranslation(Translation.StringType.MaxPlayers)} : {lobby.MaxNumberOfPlayer}";
        lobbyCreatorText.text = $"{Translation.GetTranslation(Translation.StringType.LobbyCreator)} : {lobby.Creator.Name}";
        lobbyCodeText.text = lobby.Code;
    }

    private void SetPlayerList(LobbyDto lobby)
    {
        foreach (LobbyPlayerListItem listItem in playersListItem.ToList())
        {
            if (listItem == lobbyPlayer) continue;
            Destroy(listItem.gameObject);
            playersListItem.Remove(listItem);
            listItem.AddFriendEvent -= AddFriendCallback;
            listItem.ShowProfileEvent -= ShowProfileCallback;
        }

        foreach (PlayerDto player in lobby.Players)
        {
            if (player.Id == client.User.Id) continue;
            playersListItem.Add(CreateLobbyPlayerListItem(player));
        }
    }

    private void SetCurrentPlayer()
    {
        lobbyPlayer.SetUpPlayer(new PlayerDto
        {
            Id = client.User.Id,
            Name = client.User.Login,
            Elo = client.User.Score
        });
    }

    private LobbyPlayerListItem CreateLobbyPlayerListItem(PlayerDto player)
    {
        GameObject lobbyPlayerPanel = Instantiate(lobbyPlayerPrefab, lobbyPlayersContainer.transform);
        LobbyPlayerListItem newItem = lobbyPlayerPanel.GetComponent<LobbyPlayerListItem>();
        newItem.AddFriendEvent += AddFriendCallback;
        newItem.ShowProfileEvent += ShowProfileCallback;
        newItem.SetUpPlayer(player);
        if (client.Friends.Any(f => f.Login.Equals(player.Name)))
        {
            newItem.DisableAddFriend();
        }
        return newItem;
    }

    private void AddFriendCallback(PlayerDto dto)
    {
        client.AddToSendQueue(MessageFrameBuilder.FriendRequestFrame(new FriendRequestDto
        {
            Login = dto.Name
        }));
    }

    private void ShowProfileCallback(PlayerDto dto)
    {
        client.AddToSendQueue(MessageFrameBuilder.GetOtherUserInfoFrame(new GetOtherUserInfoDto()
        {
            Login = dto.Name
        }));
    }

    public void SetUp(LobbyDto lobby)
    {
        // We have to do this here and not in Start because it happens that the Start fonction is called after the setup in our logic
        client ??= Networking.Instance.GetClient();

        Lobby = lobby;

        SetTextInfo(lobby);
        SetCurrentPlayer();
        SetPlayerList(lobby);
    }

    public void LeaveLobby()
    {
        client.AddToSendQueue(MessageFrameBuilder.LeaveLobbyFrame());
    }

    public void Ready()
    {
        readyButton.interactable = false;
        StartCoroutine(nameof(ReadyButtonCoroutine));
        isReady = !isReady;
        client.AddToSendQueue(MessageFrameBuilder.ReadyLobbyFrame());
        //readyButtonText.text = isReady ? "pas pret" : "pret";
        readyButtonText.text = isReady ? $"{Translation.GetTranslation(Translation.StringType.NotReady)}" : $"{Translation.GetTranslation(Translation.StringType.Ready)}";
    }

    private IEnumerator ReadyButtonCoroutine()
    {
        yield return new WaitForSeconds(timeBetweenReadyClick);
        readyButton.interactable = true;
    }

    private IEnumerator LobbyTimerCoroutine()
    {
        while (timer > 0)
        {
            yield return new WaitForSeconds(1);
            timer--;
            lobbyStateText.text = $"{Translation.GetTranslation(Translation.StringType.BeginsIn)} {timer} {Translation.GetTranslation(Translation.StringType.Seconds)}";
        }
    }
}
