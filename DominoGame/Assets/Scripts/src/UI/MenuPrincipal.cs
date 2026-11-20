using System.Threading.Tasks;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Color = UnityEngine.Color;

public class MenuPrincipal : MonoBehaviour
{
    [SerializeField] private GameObject panelConnection;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject errorText;
    [SerializeField] private GameObject connectingText;
    [SerializeField] private Button buttonRetryConnection;

    [SerializeField] private RegistrationForm registrationForm;
    [SerializeField] private ConnectionForm connectionForm;
    [SerializeField] private GameObject connectionGameObject;
    [SerializeField] private GameObject registrationGameObject;
    [SerializeField] private PopupManager popupManager;

    private IClient client;

    public static int IdLanguageUsed = 0;

    private void Start()
    {
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[IdLanguageUsed];

        Networking.Instance.ConnectionEvent += NetworkingConnectionCallback;

        client = Networking.Instance.GetClient();
        
        registrationForm.RegisterButtonClicked += RegisterButtonCallback;
        connectionForm.ConnectionButtonClicked += ConnectionButtonCallback;
        
        client.EventHandlers.RegistrationResponseEvent += RegistrationResponseCallback;
        client.EventHandlers.ConnectionResponseEvent += ConnectionResponseCallback;
        client.EventHandlers.DisconnectionResponseEvent += DisconnectionResponseCallback;

        NetworkingConnectionCallback();
    }

    private void OnDestroy()
    {
        Networking.Instance.ConnectionEvent -= NetworkingConnectionCallback;

        registrationForm.RegisterButtonClicked -= RegisterButtonCallback;
        connectionForm.ConnectionButtonClicked -= ConnectionButtonCallback;

        client.EventHandlers.RegistrationResponseEvent -= RegistrationResponseCallback;
        client.EventHandlers.ConnectionResponseEvent -= ConnectionResponseCallback;
        client.EventHandlers.DisconnectionResponseEvent -= DisconnectionResponseCallback;
    }

    private void NetworkingConnectionCallback()
    {
        if (Networking.Instance.IsConnected)
        {
            panelConnection.SetActive(false);
            mainPanel.SetActive(true);
        }
        else
        {
            errorText.SetActive(true);
            connectingText.SetActive(false);
            buttonRetryConnection.interactable = true;
            buttonRetryConnection.gameObject.SetActive(true);
        }
    }

    private void RegisterButtonCallback()
    {
        client.AddToSendQueue(MessageFrameBuilder.RegistrationFrame(
            new RegistrationDto
            {
                Email = registrationForm.EmailInput.text,
                Login = registrationForm.LoginInput.text,
                Password = registrationForm.PasswordInput.InputPassword.text,
                Birthdate = registrationForm.BirthdayInput.text
            }
        ));
    }

    private void RegistrationResponseCallback(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            string successMessage = Translation.GetTranslation(Translation.StringType.SuccessfulInscriptionMessage);
            popupManager.ShowMessage(successMessage, Color.green);
            registrationGameObject.SetActive(false);
            connectionGameObject.SetActive(true);
        }
        else
        {
            string errorMessage = Translation.GetTranslation(Translation.StringType.NonSuccessfulInscriptionMessage);
            string error = Translation.GetTranslationError(dto.ErrorType!.Value);
            popupManager.ShowMessage($"{errorMessage} {error}", Color.red);
        }
    }
    
    private void ConnectionButtonCallback()
    {
        client.AddToSendQueue(MessageFrameBuilder.ConnectionFrame(
            new LoggingDto
            {
                Login = connectionForm.LoginInput.text,
                Password = connectionForm.PasswordInput.InputPassword.text
            }
        ));
    }

    private void ConnectionResponseCallback(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            SceneManager.LoadScene(1);
        }
        else
        {
            string message = Translation.GetTranslation(Translation.StringType.ConnectionError);
            string error = Translation.GetTranslationError(dto.ErrorType!.Value);
            popupManager.ShowMessage($"{message} {error}", Color.red);
        }
    }

    private void DisconnectionResponseCallback(ResponseDto dto)
    {
        if (dto.OkResult)
        {
            string message = Translation.GetTranslation(Translation.StringType.ConnectionError);
            popupManager.ShowMessage(message, Color.green);
        }
        else
        {
            string message = Translation.GetTranslation(Translation.StringType.DisconnectionError);
            string error = Translation.GetTranslationError(dto.ErrorType!.Value);
           popupManager.ShowMessage($"{message} {error}", Color.red);
        }
    }

    public void RetryConnection()
    {
        buttonRetryConnection.interactable = false;
        errorText.SetActive(false);
        connectingText.SetActive(true);
        Task.Factory.StartNew(() => Networking.Instance.RetryConnection());
    }

    public void Quit()
    {
        Application.Quit();
    }
}
