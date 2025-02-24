using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class JoinLobbyWithCodeForm : MonoBehaviour
{
    public TMP_InputField CodeInput;
    [SerializeField] private Button joinButton;

    public event Action JoinButtonClicked;

    private void OnEnable()
    {
        joinButton.interactable = false;
        CodeInput.text = string.Empty;
    }

    public void InputsOnValueChanged()
    {
        if (!string.IsNullOrWhiteSpace(CodeInput.text))
        {
            joinButton.interactable = true;
        }
    }

    public void Join()
    {
        JoinButtonClicked?.Invoke();
    }
}
