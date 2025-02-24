using System;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;

using TMPro;

using UnityEngine;

public class LobbyListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text creatorText;
    [SerializeField] private TMP_Text numberOfPlayer;
    [SerializeField] private TMP_Text lobbyName;

    public event Action<LobbyInfoDto> LobbySelected;

    private LobbyInfoDto lobbyDto;

    public void SetUpLobby(LobbyInfoDto lobby)
    {
        lobbyDto = lobby;
        UpdateInfo();
    }

    public int GetLobbyId()
    {
        return lobbyDto.Id;
    }

    private void UpdateInfo()
    {
        creatorText.text = lobbyDto.Creator;
        numberOfPlayer.text = $"{lobbyDto.NumberOfPlayer} / {lobbyDto.MaxNumberOfPlayer}";
        lobbyName.text = lobbyDto.Name;
    }

    public void LobbyListSelected()
    {
        LobbySelected?.Invoke(lobbyDto);
    }
}
