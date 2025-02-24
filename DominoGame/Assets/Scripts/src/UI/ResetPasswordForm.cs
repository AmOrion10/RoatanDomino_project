using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResetPasswordForm : MonoBehaviour
{
    [SerializeField] private GameObject connectionForm;
    [SerializeField] private GameObject askResetForm;
    [SerializeField] private GameObject resetForm;
    [SerializeField] private Button askResetButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private PopupManager popupManager;

    private IClient client;

    private void Start()
    {
        client = Networking.Instance.GetClient();
        client.EventHandlers.AskResetPasswordResponseEvent += AskResetPasswordCallback;
        client.EventHandlers.ResetPasswordResponseEvent += ResetPasswordCallback;
    }

    private void OnDestroy()
    {
        client.EventHandlers.AskResetPasswordResponseEvent -= AskResetPasswordCallback;
        client.EventHandlers.ResetPasswordResponseEvent -= ResetPasswordCallback;
    }

    private void AskResetPasswordCallback(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            string message = Translation.GetTranslation(Translation.StringType.EmailSent);
            //popupManager.ShowMessage("Email sent", Color.green);
            popupManager.ShowMessage(message, Color.green);
            askResetForm.SetActive(false);
            resetForm.SetActive(true);
        }
        else
        {
            // Error translate
            string message = Translation.GetTranslation(Translation.StringType.ErrorAskingResetPassword);
            //popupManager.ShowMessage($"Error while asking to reset password : {dto.ErrorType}", Color.red);
            popupManager.ShowMessage($"{message} : {dto.ErrorType}", Color.red);
        }
    }

    private void ResetPasswordCallback(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            string message = Translation.GetTranslation(Translation.StringType.PasswordChanged);
            //popupManager.ShowMessage("Password changed sucessfuly", Color.green);
            popupManager.ShowMessage(message, Color.green);
            gameObject.SetActive(false);
            connectionForm.SetActive(true);
        }
        else
        {
            string error = Translation.GetTranslationError(dto.ErrorType!.Value);
            string message = Translation.GetTranslation(Translation.StringType.ErrorResetPassword);
            //popupManager.ShowMessage($"Error while reseting password : {dto.ErrorType}", Color.red);
            popupManager.ShowMessage($"{message} : {error}", Color.red);
        }
    }

    public void AskReset()
    {
        client.AddToSendQueue(MessageFrameBuilder.AskResetPasswordFrame(new AskResetPasswordDto
        {
            Email = emailInputField.text.Trim()
        }));
    }

    public void Reset()
    {
        client.AddToSendQueue(MessageFrameBuilder.ResetPasswordFrame(new ResetPasswordDto
        {
            Code = codeInputField.text.Trim(),
            Email = emailInputField.text.Trim(),
            Password = passwordInputField.text
        }));
    }

    public void OnValueChangeEmailAddress()
    {
        askResetButton.interactable = !string.IsNullOrWhiteSpace(emailInputField.text);
    }

    public void OnValueChangeReset()
    {
        resetButton.interactable = !string.IsNullOrWhiteSpace(codeInputField.text) &&
                                   !string.IsNullOrWhiteSpace(passwordInputField.text);
    }
}
