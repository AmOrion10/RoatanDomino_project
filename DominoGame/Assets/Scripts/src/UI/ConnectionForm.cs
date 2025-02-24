using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class ConnectionForm : MonoBehaviour
{
    public TMP_InputField LoginInput;
    public PasswordInfo PasswordInput;

    [SerializeField] private Button connectionButton;

    public event Action ConnectionButtonClicked;

    private void Start()
    {
        LoginInput.onEndEdit.AddListener(OnEndEdit);
        PasswordInput.InputPassword.onEndEdit.AddListener(OnEndEdit);

        LoginInput.onValueChanged.AddListener(delegate { InputsOnValueChanged(); });
        PasswordInput.InputPassword.onValueChanged.AddListener(delegate { InputsOnValueChanged(); });

        CheckForValidInputs();
    }

    private void OnDestroy()
    {
        LoginInput.onEndEdit.RemoveListener(OnEndEdit);
        PasswordInput.InputPassword.onEndEdit.RemoveListener(OnEndEdit);
    }

    private void OnEndEdit(string text)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnSubmit(text);
        }
    }

    private void OnSubmit(string text)
    {
        if (!IsThereAnyEmptyFields())
        {
            Connect();
        }
    }

    public void InputsOnValueChanged()
    {
        CheckForValidInputs();
    }

    private void CheckForValidInputs()
    {
        connectionButton.interactable = !IsThereAnyEmptyFields();
    }

    private bool IsThereAnyEmptyFields()
    {
        return string.IsNullOrWhiteSpace(LoginInput.text) ||
               string.IsNullOrWhiteSpace(PasswordInput.InputPassword.text);
    }

    public void Connect()
    {
        ConnectionButtonClicked?.Invoke();
    }
}
