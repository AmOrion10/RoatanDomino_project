using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class CreateLobbyForm : MonoBehaviour
{
    public TMP_InputField LobbyNameInput;
    public Toggle LobbyPrivate;
    public TMP_Dropdown LobbyNumberOfPlayerDropDown;

    [SerializeField] private Button createLobbyButton;

    public event Action CreateLobbyButtonClicked;

    public void InputsOnValueChanged()
    {
        CheckForValidInputs();
    }

    private void CheckForValidInputs()
    {
        createLobbyButton.interactable = !string.IsNullOrWhiteSpace(LobbyNameInput.text);
    }

    public void CreateLobby()
    {
        CreateLobbyButtonClicked?.Invoke();
    }
}
