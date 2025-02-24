using System;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using TMPro;
using UnityEngine;

public class FriendRequestItem : MonoBehaviour
{
    [SerializeField] private TMP_Text friendRequestNameText;

    public event Action<FriendRequestDto> AcceptFriendRequestEvent;
    public event Action<FriendRequestDto> DeclineFriendRequestEvent;

    private FriendRequestDto FriendRequestDto { get; set; }

    public void SetUp(FriendRequestDto dto)
    {
        FriendRequestDto = dto;
        friendRequestNameText.text = dto.Login;
    }

    public void Accept()
    {
        AcceptFriendRequestEvent?.Invoke(FriendRequestDto);
    }

    public void Decline()
    {
        DeclineFriendRequestEvent?.Invoke(FriendRequestDto);
    }
}
