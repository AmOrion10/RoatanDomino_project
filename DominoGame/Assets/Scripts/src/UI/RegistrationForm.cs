using System;
using System.Globalization;
using System.Text.RegularExpressions;

using TMPro;

using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class RegistrationForm : MonoBehaviour
{
    private static readonly DateTime MinimumDateOfBirth = new(1900, 1, 1);
    
    public TMP_InputField LoginInput;
    public TMP_InputField EmailInput;
    public TMP_Text EmailErrorText;
    public TMP_InputField BirthdayInput;
    public TMP_Text BirthdayErrorText;
    public PasswordInfo PasswordInput;
    public PasswordInfo ConfirmPasswordInput;
    public TMP_Text PasswordErrorText;

    [SerializeField] private Button registrationButton;

    public event Action RegisterButtonClicked;

    private string previousBirthdayValue = string.Empty;

    private void Start()
    {
        LoginInput.onEndEdit.AddListener(OnEndEdit);
        EmailInput.onEndEdit.AddListener(OnEndEdit);
        BirthdayInput.onEndEdit.AddListener(OnEndEdit);
        PasswordInput.InputPassword.onEndEdit.AddListener(OnEndEdit);
        ConfirmPasswordInput.InputPassword.onEndEdit.AddListener(OnEndEdit);

        LoginInput.onValueChanged.AddListener(delegate { InputsOnValueChanged(); });
        PasswordInput.InputPassword.onValueChanged.AddListener(delegate { InputsOnValueChanged(); });

        CheckForValidRegistration();
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
        if (!IsThereAnyEmptyFields() && PasswordsAreTheSame() && EmailIsValid() && BirthdayIsValid())
        {
            Register();
        }
    }

    public void InputsOnValueChanged()
    {
        CheckForValidRegistration();
    }
    
    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        UpdateErrorMessages();
    }

    private void CheckForValidRegistration()
    {
        UpdateErrorMessages();
        
        if (IsThereAnyEmptyFields() || !PasswordsAreTheSame() || !EmailIsValid() || !BirthdayIsValid())
        {
            registrationButton.interactable = false;
            return;
        }

        registrationButton.interactable = true;
    }

    private void UpdateErrorMessages()
    {
        if (!EmailIsValid() && EmailInput.text != string.Empty)
        {
            EmailErrorText.text = Translation.GetTranslation(Translation.StringType.EmailFormatError);
            EmailInput.transform.Find("Text Area").GetComponent<RectTransform>().offsetMax = new Vector2(-400, -7);
        }
        else
        {
            EmailErrorText.text = string.Empty;
            EmailInput.transform.Find("Text Area").GetComponent<RectTransform>().offsetMax = new Vector2(-50, -7);
        }

        if (!PasswordsAreTheSame() && ConfirmPasswordInput.InputPassword.text != string.Empty)
        {
            PasswordErrorText.text = Translation.GetTranslation(Translation.StringType.PasswordMatchError);
            ConfirmPasswordInput.transform.Find("Text Area").GetComponent<RectTransform>().offsetMax = new Vector2(-530, -7);
        }
        else
        {
            PasswordErrorText.text = string.Empty;
            ConfirmPasswordInput.transform.Find("Text Area").GetComponent<RectTransform>().offsetMax = new Vector2(-50, -7);
        }

        if (!BirthdayIsValid())
        {
            if (BirthdayInput.text.Length < 10)
            {
                if (BirthdayInput.text == string.Empty)
                    BirthdayErrorText.text = string.Empty;
                else
                    BirthdayErrorText.text = Translation.GetTranslation(Translation.StringType.BirthdayFormatError);
            }
            else
            {
                BirthdayErrorText.text = Translation.GetTranslation(Translation.StringType.BirthdayAgeError);
            }
        }
        else
        {
            BirthdayErrorText.text = string.Empty;
        }
    }
    private bool IsThereAnyEmptyFields()
    {
        return string.IsNullOrWhiteSpace(LoginInput.text) || 
               string.IsNullOrWhiteSpace(EmailInput.text) ||
               string.IsNullOrWhiteSpace(BirthdayInput.text) ||
               string.IsNullOrWhiteSpace(PasswordInput.InputPassword.text) ||
               string.IsNullOrWhiteSpace(ConfirmPasswordInput.InputPassword.text);
    }

    private bool PasswordsAreTheSame()
    {
        return PasswordInput.InputPassword.text.Equals(ConfirmPasswordInput.InputPassword.text);
    }

    private bool EmailIsValid()
    {
        return Regex.IsMatch(EmailInput.text, @"^([\w\.\-]+)@([\w\-]+)((\.([a-z]){2,3})+)$");
    }

    private bool BirthdayIsValid()
    {
        if (BirthdayInput.text.Length != 10) return false;
        if (!DateTime.TryParseExact(BirthdayInput.text, "dd/MM/yyyy", null, DateTimeStyles.None, out DateTime datetime)) return false;
        return datetime >= MinimumDateOfBirth;
    }

    public void BirthdayInputOnValueChanged()
    {
        string input = BirthdayInput.text;
        if (input == previousBirthdayValue) return;
        if (input.Length == 2 || input.Length == 5)
        {
            if (previousBirthdayValue.Length < input.Length)
            {
                input += "/";
                previousBirthdayValue = input;
                BirthdayInput.text = input;
                BirthdayInput.stringPosition = input.Length;
            }
            else
            {
                input = input.Remove(input.Length - 1, 1);
                previousBirthdayValue = input;
                BirthdayInput.text = input;
                BirthdayInput.stringPosition = input.Length;
            }
        }
        CheckForValidRegistration();
    }

    public void Register()
    {
        RegisterButtonClicked?.Invoke();
    }
}
