using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using UnityEngine;

public class PanelFriendRequests : MonoBehaviour
{
    [SerializeField] private FriendRequestItem friendRequestItemPrefab;
    [SerializeField] private GameObject friendRequestContainer;
    
    private readonly List<FriendRequestItem> friendRequests = new();
    private IClient client;

    private void OnEnable()
    {
        client = Networking.Instance.GetClient();
        client.EventHandlers.GetAllFriendRequestsResponseEvent += GetAllFriendRequestsCallback;
        client.AddToSendQueue(MessageFrameBuilder.GetAllFriendRequestsFrame());
    }

    private void OnDisable()
    {
        client.EventHandlers.GetAllFriendRequestsResponseEvent -= GetAllFriendRequestsCallback;
    }

    private void GetAllFriendRequestsCallback(ResponseObjectDto<FriendRequestsDto> dto)
    {
        ClearFriendRequests();
        foreach (FriendRequestDto friendRequestDto in dto.Object.FriendRequests)
        {
            FriendRequestItem newItem = Instantiate(friendRequestItemPrefab, friendRequestContainer.transform);
            newItem.SetUp(friendRequestDto);
            newItem.AcceptFriendRequestEvent += AcceptFriendRequestCallback;
            newItem.DeclineFriendRequestEvent += DeclineFriendRequestCallback;
            friendRequests.Add(newItem);
        }
    }

    private void ClearFriendRequests()
    {
        foreach (FriendRequestItem friendRequestItem in friendRequests.ToList())
        {
            friendRequests.Remove(friendRequestItem);
            friendRequestItem.AcceptFriendRequestEvent -= AcceptFriendRequestCallback;
            friendRequestItem.DeclineFriendRequestEvent -= DeclineFriendRequestCallback;
            Destroy(friendRequestItem.gameObject);
        }
    }

    private void AcceptFriendRequestCallback(FriendRequestDto dto)
    {
        client.AddToSendQueue(new List<MessageFrame>
        {
            MessageFrameBuilder.AcceptFriendRequestFrame(dto),
            MessageFrameBuilder.GetAllFriendRequestsFrame()
        });
    }

    private void DeclineFriendRequestCallback(FriendRequestDto dto)
    {
        client.AddToSendQueue(new List<MessageFrame>
        {
            MessageFrameBuilder.DeclineFriendRequestFrame(dto),
            MessageFrameBuilder.GetAllFriendRequestsFrame()
        });
    }
}
