using System;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;

using TMPro;

using UnityEngine;

public class FriendItem : MonoBehaviour
{
    [SerializeField] private TMP_Text friendNameText;

    public event Action<FriendDto> RemoveFriendEvent;

    private FriendDto Friend { get; set; }

    public void SetUp(FriendDto dto)
    {
        Friend = dto;
        friendNameText.text = dto.Login;
    }

    public void RemoveFriend()
    {
        RemoveFriendEvent?.Invoke(Friend);
    }
}
