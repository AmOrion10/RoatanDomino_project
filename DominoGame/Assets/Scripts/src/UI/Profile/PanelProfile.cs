using System;
using System.Collections.Generic;
using System.Globalization;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class PanelProfile : MonoBehaviour
{
    private static readonly DateTime MinimumDateOfBirth = new(1900, 1, 1);

    [SerializeField] private TMP_Text loginText;
    [SerializeField] private TMP_Text emailText;
    [SerializeField] private TMP_Text nationalityText;
    [SerializeField] private TMP_Text signupText;
    [SerializeField] private UpdateProfileForm updateProfileForm;
    [SerializeField] private TMP_Text birthdateText;
    [SerializeField] private TMP_Text birthdateErrorText;
    [SerializeField] private TMP_InputField actualPasswordInput;
    [SerializeField] private TMP_InputField newPasswordInput;
    [SerializeField] private TMP_InputField confirmNewPasswordInput;
    [SerializeField] private Button validateNewPassword;
    [SerializeField] private TMP_Text newPasswordErrorText;
    [SerializeField] private GameObject panelProfileGo;
    [SerializeField] private GameObject panelModifyPasswordGo;

    private IClient client;

    private void OnEnable()
    {
        client = Networking.Instance.GetClient();
        client.EventHandlers.GetInfoProfileResponseEvent += GetInfoProfileCallback;
        client.EventHandlers.UpdatePasswordResponseEvent += UpdatePasswordResponseCallback;
        client.AddToSendQueue(MessageFrameBuilder.GetInfoProfileFrame());
    }

    private void OnDisable()
    {
        client.EventHandlers.GetInfoProfileResponseEvent -= GetInfoProfileCallback;
        client.EventHandlers.UpdatePasswordResponseEvent -= UpdatePasswordResponseCallback;
    }

    private void UpdatePasswordResponseCallback(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            newPasswordErrorText.gameObject.SetActive(false);
            panelProfileGo.SetActive(true);
            panelModifyPasswordGo.SetActive(false);
        }
        else
        {   
            newPasswordErrorText.gameObject.SetActive(true);         
            newPasswordErrorText.text = Translation.GetTranslationError(dto.ErrorType!.Value);
        }
    }

    private void GetInfoProfileCallback(ResponseObjectDto<InfoProfileDto> dto)
    {
        if (dto.OkResult)
        {
            SetUpProfile(dto.Object.User);
        }
    }

    private void SetUpProfile(UserDto user)
    {
        loginText.text = user.Login;
        emailText.text = user.Email;
        nationalityText.text = user.Nationality ?? string.Empty;
        signupText.text = user.DateSignup;
        birthdateText.text = user.BirthDate;
        updateProfileForm.SetUp(user);
    }

    public void ApplyModifications()
    {
        if (!NeedToSendUpdate())
        {
            return;
        }
        
        string nationality = updateProfileForm.NationalityDropDown.options[updateProfileForm.NationalityDropDown.value].text;

        if (!BirthdayIsValid())
        {
            if (updateProfileForm.BirthDateInputField.text.Length < 10)
            {
                if (updateProfileForm.BirthDateInputField.text == string.Empty)
                    birthdateErrorText.text = string.Empty;
                else
                    birthdateErrorText.text = Translation.GetTranslation(Translation.StringType.BirthdayFormatError);
            }
            else
            {
                birthdateErrorText.text = Translation.GetTranslation(Translation.StringType.BirthdayAgeError);
            }
            return;
        }
        else
        {
            birthdateErrorText.text = string.Empty;
        }

        client.AddToSendQueue(new List<MessageFrame>
        {
            MessageFrameBuilder.UpdateProfileFrame(new UpdateProfileDto
            {
                Login = updateProfileForm.LoginInputField.text,
                Nationality = nationality,
                Birthdate = updateProfileForm.BirthDateInputField.text
            }),
            MessageFrameBuilder.GetInfoProfileFrame()
        });
    }

    private bool BirthdayIsValid()
    {
        if (updateProfileForm.BirthDateInputField.text.Length != 10) return false;
        if (!DateTime.TryParseExact(updateProfileForm.BirthDateInputField.text, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime datetime)) return false;
        return datetime >= MinimumDateOfBirth;
    }

    private bool NeedToSendUpdate()
    {
        string nationality = updateProfileForm.NationalityDropDown.options[updateProfileForm.NationalityDropDown.value].text;
        string clientNationality = client.User.Nationality;

        bool nationalityChange = (string.IsNullOrWhiteSpace(clientNationality) && !string.IsNullOrWhiteSpace(nationality)) ||
                                 (!string.IsNullOrWhiteSpace(clientNationality) && (string.IsNullOrWhiteSpace(nationality) || !clientNationality.Equals(nationality)));

        return !client.User.Login.Equals(updateProfileForm.LoginInputField.text) ||
               nationalityChange ||
               !client.User.BirthDate.Equals(updateProfileForm.BirthDateInputField.text);
    }

    public void OnValueChangeModifyPassword()
    {
        validateNewPassword.interactable = !string.IsNullOrWhiteSpace(actualPasswordInput.text) &&
                                           !string.IsNullOrWhiteSpace(newPasswordInput.text) &&
                                           !string.IsNullOrWhiteSpace(confirmNewPasswordInput.text) &&
                                           newPasswordInput.text.Equals(confirmNewPasswordInput.text);
    }

    public void ModifyPassword()
    {
        client.AddToSendQueue(MessageFrameBuilder.UpdatePasswordFrame(new PasswordChangeDto()
        {
            ActualPassword = actualPasswordInput.text,
            NewPassword = newPasswordInput.text
        }));
    }
}
