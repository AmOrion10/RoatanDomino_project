using System;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;

using TMPro;

using UnityEngine;

public class LobbyPlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerEloText;
    [SerializeField] private GameObject playerReadyGameObject;
    [SerializeField] private GameObject addFriendButton;

    public event Action<PlayerDto> AddFriendEvent;
    public event Action<PlayerDto> ShowProfileEvent;

    public PlayerDto PlayerDto { get; private set; }
    public bool IsReady { get; private set; }

    public void SetUpPlayer(PlayerDto playerDto)
    {
        PlayerDto = playerDto;
        playerNameText.text = playerDto.Name;
        playerEloText.text = $"Elo : {playerDto.Elo}";
    }

    public void PlayerReadyChanged(bool isReady)
    {
        playerReadyGameObject.SetActive(isReady);
        IsReady = isReady;
    }

    public void AddFriend()
    {
        AddFriendEvent?.Invoke(PlayerDto);
    }

    public void DisableAddFriend()
    {
        addFriendButton.SetActive(false);
    }

    public void ShowProfile()
    {
        ShowProfileEvent?.Invoke(PlayerDto);
    }
}
