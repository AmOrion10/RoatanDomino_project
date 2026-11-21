using System;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using UnityEngine;
using UnityEngine.SceneManagement;

public class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }
    public bool IsActiveRoundChatLocked { get; private set; }

    private IClient client;
    private List<Chat> chats = new();
    private static readonly List<ChatDto> ChatsConnected = new();

    private void Awake()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("ChatManager");
        if (objs.Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        Instance = this;
    }

    private void Start()
    {
        FindChatsInScene();
        RefreshClient();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    public void RefreshClient()
    {
        if (client != null)
        {
            client.EventHandlers.ReceiveChatMessageEvent -= ReceiveChatMessageCallback;
            client.EventHandlers.JoinChatEvent -= JoinChatCallback;
            client.EventHandlers.LeaveChatEvent -= LeaveChatCallback;
        }

        if (Networking.Instance == null) return;

        client = Networking.Instance.GetClient();
        if (client != null)
        {
            client.EventHandlers.ReceiveChatMessageEvent += ReceiveChatMessageCallback;
            client.EventHandlers.JoinChatEvent += JoinChatCallback;
            client.EventHandlers.LeaveChatEvent += LeaveChatCallback;
        }
    }

    private void OnDestroy()
    {
        if (Instance != null && Instance != this)
            return;
        if (client != null)
        {
            client.EventHandlers.ReceiveChatMessageEvent -= ReceiveChatMessageCallback;
            client.EventHandlers.JoinChatEvent -= JoinChatCallback;
            client.EventHandlers.LeaveChatEvent -= LeaveChatCallback;
        }
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void FindChatsInScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        chats = Resources.FindObjectsOfTypeAll<Chat>().Where(r => r.gameObject.scene == activeScene).ToList();
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        FindChatsInScene();
        lock (ChatsConnected)
        {
            foreach (ChatDto chatConnected in ChatsConnected.ToList())
            {
                foreach (Chat chat in chats)
                {
                    List<Tuple<GameObject, GameObject>> objsAndContainers = chat.JoinChat(chatConnected);
                    foreach (Tuple<GameObject, GameObject> objAndContainer in objsAndContainers)
                    {
                        objAndContainer.Item1.transform.SetParent(objAndContainer.Item2.transform, false);
                    }
                }
            }
        }
    }

    private void ReceiveChatMessageCallback(ReceiveChatMessageDto dto)
    {
        foreach (Chat chat in chats)
        {
            Tuple<GameObject, GameObject> objAndContainer = chat.ReceiveChatMessage(dto, dto.Sender.Equals(client.User!.Login));

            objAndContainer.Item1.transform.SetParent(objAndContainer.Item2.transform, false);
        }
    }

    private void JoinChatCallback(ChatDto dto)
    {
        lock (ChatsConnected)
        {
            ChatsConnected.Add(dto);
        }
        foreach (Chat chat in chats)
        {
            List<Tuple<GameObject, GameObject>> objsAndContainers = chat.JoinChat(dto);
            foreach (Tuple<GameObject, GameObject> objAndContainer in objsAndContainers)
            {
                objAndContainer.Item1.transform.SetParent(objAndContainer.Item2.transform, false);
            }
        }
    }

    private void LeaveChatCallback(ChatDto dto)
    {
        lock (ChatsConnected)
        {
            ChatDto existing = ChatsConnected.FirstOrDefault(c => c.Id == dto.Id);
            if (existing != null)
            {
                ChatsConnected.Remove(existing);
            }
        }
        foreach (Chat chat in chats)
        {
            chat.LeaveChat(dto);
        }
    }

    public void SetActiveRoundChatLocked(bool locked)
    {
        IsActiveRoundChatLocked = locked;
        foreach (Chat chat in chats)
        {
            if (chat != null)
            {
                chat.gameObject.SetActive(!locked);
            }
        }
    }

    public void SendMessage(int chatId, string message)
    {
        // Anti-cheating policy: block free-text chat during active rounds; allow whitelisted quick-emotes only
        if (IsActiveRoundChatLocked && !RoatanQuickEmotes.IsAllowedQuickEmote(message))
        {
            Debug.Log("[ChatManager] Free-text chat is disabled during active rounds (anti-cheating). Use Quick-Emotes instead.");
            return;
        }

        client.AddToSendQueue(MessageFrameBuilder.SendMessageFrame(new SendChatMessageDto
        {
            ChatId = chatId,
            Message = message
        }));
    }
}
