using System;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;

using TMPro;

using UnityEngine;

public class Chat : MonoBehaviour
{
    [SerializeField] private GameObject chatsContainer;
    [SerializeField] private GameObject chatsSelectorContainer;
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private ChatSelector chatSelectorPrefab;
    [SerializeField] private ChatContainer chatContainerPrefab;
    [SerializeField] private bool isOpen;

    private Animator animator;
    private ChatContainer actualChatContainer;
    private readonly List<ChatContainer> chats = new();
    private readonly List<ChatSelector> chatSelectors = new();

    private void Start()
    {
        animator = GetComponent<Animator>();
        messageInput.onSubmit.AddListener(SendMessageStr);
    }

    private void OnDestroy()
    {
        messageInput.onSubmit.RemoveListener(SendMessageStr);

        foreach (ChatSelector chatSelector in chatSelectors)
        {
            chatSelector.ChatSelectedEvent -= ChatSelectedButtonCallback;
        }
    }

    private void ChatSelectedButtonCallback(int chatId)
    {
        if (actualChatContainer == null)
        {
            actualChatContainer = chats.First(c => c.Chat.Id == chatId);
            actualChatContainer.gameObject.SetActive(true);
        }
        else if (actualChatContainer.Chat.Id != chatId)
        {
            actualChatContainer.gameObject.SetActive(false);
            actualChatContainer = chats.First(c => c.Chat.Id == chatId);
            actualChatContainer.gameObject.SetActive(true);
        }
    }

    private void SendMessageStr(string _)
    {
        SendMessage();
    }

    public List<Tuple<GameObject, GameObject>> JoinChat(ChatDto dto)
    {
        List<Tuple<GameObject, GameObject>> res = new();

        ChatContainer chatContainer = Instantiate(chatContainerPrefab);
        chatContainer.SetChat(dto);
        chats.Add(chatContainer);
        res.Add(new Tuple<GameObject, GameObject>(chatContainer.gameObject, chatsContainer));

        ChatSelector chatSelector = Instantiate(chatSelectorPrefab);
        chatSelector.SetChat(dto);
        chatSelector.ChatSelectedEvent += ChatSelectedButtonCallback;
        chatSelectors.Add(chatSelector);
        res.Add(new Tuple<GameObject, GameObject>(chatSelector.gameObject, chatsSelectorContainer));

        ChatSelectedButtonCallback(dto.Id);

        return res;
    }

    public void LeaveChat(ChatDto dto)
    {
        ChatContainer chatContainer = chats.First(c => c.Chat.Id == dto.Id);
        ChatSelector chatSelector = chatSelectors.First(c => c.Chat.Id == dto.Id);
        chatSelector.ChatSelectedEvent -= ChatSelectedButtonCallback;
        chats.Remove(chatContainer);
        Destroy(chatContainer.gameObject);
        Destroy(chatSelector.gameObject);

        if (actualChatContainer == chatContainer && chats.Count > 0)
        {
            actualChatContainer = null;
            ChatSelectedButtonCallback(chats.First().Chat.Id);
        }
    }

    public Tuple<GameObject, GameObject> ReceiveChatMessage(ReceiveChatMessageDto dto, bool isActualClient)
    {
        ChatContainer chatContainer = chats.First(c => c.Chat.Id == dto.ChatId);
        return chatContainer.ReceiveChatMessage(dto, isActualClient);
    }

    public void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(messageInput.text) || actualChatContainer == null)
        {
            return;
        }

        ChatManager.Instance.SendMessage(actualChatContainer.Chat.Id, messageInput.text);

        messageInput.text = string.Empty;
        messageInput.Select();
    }

    public void OpenCloseChat()
    {
        //animator.Play(isOpen ? "ChatCloseAnimation" : "ChatOpenAnimation");
        isOpen = !isOpen;
        animator.SetBool("open", isOpen);
    }
}
