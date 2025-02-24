using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class ChatMessage : MonoBehaviour
{
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private HorizontalLayoutGroup horizontalGroup;

    public void SetMessage(string msg, string playerLogin, bool isActualClient)
    {
        horizontalGroup.reverseArrangement = !isActualClient;
        message.text = msg;
        playerName.text = playerLogin;
    }
}
