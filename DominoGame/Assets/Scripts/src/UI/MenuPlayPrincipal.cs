using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

public class MenuPlayPrincipal : MonoBehaviour
{
    [SerializeField] private PopupManager popupMainPanel;
    [SerializeField] private GameObject loadingScreenGameObject;
    [SerializeField] private InLobby inLobby;

    public static bool SucessfulConnection = true;

    private IClient client;
    
    private void Start()
    {
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[MenuPrincipal.IdLanguageUsed];

        client = Networking.Instance.GetClient();

        if (SucessfulConnection)
        {
            SucessfulConnection = false;
            string successMessage = Translation.GetTranslation(Translation.StringType.SuccessfulConnectionMessage);
            popupMainPanel.ShowMessage(successMessage, Color.green);

            client.AddToSendQueue(new List<MessageFrame>
            {
                // Temp
                //MessageFrameBuilder.ConnectionFrame(new LoggingDto
                //{
                //    Login = "jm",
                //    Password = "jmjmjmjm1"
                //}),
                MessageFrameBuilder.ReadyConnectedFrame(),
                MessageFrameBuilder.GetAllFriendsFrame()
            });
        }

        client.EventHandlers.CreateLobbyResponseEvent += JoinLobbyCallback;
        client.EventHandlers.JoinLobbyResponseEvent += JoinLobbyCallback;
    }
    
    private void OnDestroy()
    {
        client.EventHandlers.CreateLobbyResponseEvent -= JoinLobbyCallback;
        client.EventHandlers.JoinLobbyResponseEvent -= JoinLobbyCallback;
    }

    public void Disconnect()
    {
        client.AddToSendQueue(MessageFrameBuilder.DisconnectionFrame());
        SceneManager.LoadScene(0);
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void GoToTutorial()
    {
        SceneManager.LoadScene("TutorialScene");
    }
    private void JoinLobbyCallback(ResponseObjectDto<LobbyDto> dto)
    {
        if (dto.OkResult)
        {
            gameObject.SetActive(false);
            inLobby.gameObject.SetActive(true);
            inLobby.SetUp(dto.Object);
        }
    }

    public void GoToRankedGame()
    {
        //loadingScreenGameObject.SetActive(true);
        //loadingScreenGameObject.GetComponent<LoadingScreen>().LoadingScreenFunction();
        client.AddToSendQueue(MessageFrameBuilder.JoinRankedFrame());
    }
}
