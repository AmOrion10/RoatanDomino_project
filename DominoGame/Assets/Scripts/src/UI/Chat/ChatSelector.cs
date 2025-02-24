using System;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class ChatSelector : MonoBehaviour
{
    [SerializeField] private TMP_Text chatNameText;

    public ChatDto Chat { get; private set; }
    public event Action<int> ChatSelectedEvent;

    private Button button;

    private void OnEnable()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ButtonClickCallback);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(ButtonClickCallback);
    }

    private void ButtonClickCallback()
    {
        ChatSelectedEvent?.Invoke(Chat.Id);
    }

    public void SetChat(ChatDto dto)
    {
        Chat = dto;
        chatNameText.text = dto.Name;
    }
}
