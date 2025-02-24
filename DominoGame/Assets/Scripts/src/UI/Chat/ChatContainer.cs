using System;
using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;

using UnityEngine;

public class ChatContainer : MonoBehaviour
{
    [SerializeField] private GameObject chatContainer;
    [SerializeField] private ChatMessage chatMessagePrefab;

    private readonly List<ChatMessage> chatMessages = new();

    public ChatDto Chat { get; private set; }
    
    public Tuple<GameObject, GameObject> ReceiveChatMessage(ReceiveChatMessageDto dto, bool isActualClient)
    {
        ChatMessage chatMessage = Instantiate(chatMessagePrefab);
        chatMessage.SetMessage(dto.Message, dto.Sender, isActualClient);
        chatMessages.Add(chatMessage);
        return new Tuple<GameObject, GameObject>(chatMessage.gameObject, chatContainer);
    }

    public void SetChat(ChatDto dto)
    {
        Chat = dto;
    }
}
