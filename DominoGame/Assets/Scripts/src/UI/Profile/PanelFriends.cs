using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using UnityEngine;

public class PanelFriends : MonoBehaviour
{
    [SerializeField] private FriendItem friendItemPrefab;
    [SerializeField] private GameObject friendItemContainer;

    private readonly List<FriendItem> friends = new();
    private IClient client;

    private void OnEnable()
    {
        client = Networking.Instance.GetClient();
        client.EventHandlers.GetAllFriendsResponseEvent += GetAllFriendsCallback;
        client.AddToSendQueue(MessageFrameBuilder.GetAllFriendsFrame());
    }

    private void OnDisable()
    {
        client.EventHandlers.GetAllFriendsResponseEvent -= GetAllFriendsCallback;
    }

    private void GetAllFriendsCallback(ResponseObjectDto<FriendsDto> dto)
    {
        ClearFriends();
        foreach (FriendDto friendDto in dto.Object.Friends)
        {
            FriendItem newItem = Instantiate(friendItemPrefab, friendItemContainer.transform);
            newItem.SetUp(friendDto);
            newItem.RemoveFriendEvent += RemoveFriendCallback;
            friends.Add(newItem);
        }
    }

    private void ClearFriends()
    {
        foreach (FriendItem friendItem in friends.ToList())
        {
            friends.Remove(friendItem);
            friendItem.RemoveFriendEvent -= RemoveFriendCallback;
            Destroy(friendItem.gameObject);
        }
    }

    private void RemoveFriendCallback(FriendDto dto)
    {
        client.AddToSendQueue(new List<MessageFrame>
        {
            MessageFrameBuilder.RemoveFriendFrame(dto),
            MessageFrameBuilder.GetAllFriendsFrame()
        });
    }
}
