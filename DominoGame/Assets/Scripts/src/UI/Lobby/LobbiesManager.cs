using System.Collections;
using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class LobbiesManager : MonoBehaviour
{
    [SerializeField] private GameObject lobbyPrefab;
    [SerializeField] private GameObject lobbyContainer;
    [SerializeField] private TMP_Text searchLobbyName;
    [SerializeField] private TMP_Text lobbyCountText;
    [SerializeField] private PopupManager popupLobbyPanel;
    [SerializeField] private CreateLobbyForm createLobbyForm;
    [SerializeField] private JoinLobbyWithCodeForm joinLobbyWithCodeForm;
    [SerializeField] private InLobby inLobby;
    [SerializeField] private Button joinButton;

    private IClient client;
    private readonly List<LobbyListItem> lobbyPanels = new();
    private LobbyInfoDto lobbySelected;

    private void OnEnable()
    {
        client = Networking.Instance.GetClient();
        client.AddToSendQueue(MessageFrameBuilder.GetAllLobbyFrame());
        joinButton.interactable = false;
        StartCoroutine(nameof(UpdateLobbies));
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void Start()
    {
        client.EventHandlers.GetAllLobbiesResponseEvent += GetAllLobbiesCallback;
        client.EventHandlers.GetLobbiesWithNameResponseEvent += GetLobbiesWithNameCallback;
        client.EventHandlers.CreateLobbyResponseEvent += CreateLobbyCallback;
        client.EventHandlers.JoinLobbyResponseEvent += JoinLobbyCallback;
        client.EventHandlers.JoinLobbyWithCodeResponseEvent += JoinLobbyWithCodeCallback;
        createLobbyForm.CreateLobbyButtonClicked += CreateLobbyButtonCallback;
        joinLobbyWithCodeForm.JoinButtonClicked += JoinLobbyWithCodeButtonCallback;
    }

    private void OnDestroy()
    {
        client.EventHandlers.GetAllLobbiesResponseEvent -= GetAllLobbiesCallback;
        client.EventHandlers.GetLobbiesWithNameResponseEvent -= GetLobbiesWithNameCallback;
        client.EventHandlers.CreateLobbyResponseEvent -= CreateLobbyCallback;
        client.EventHandlers.JoinLobbyResponseEvent -= JoinLobbyCallback;
        client.EventHandlers.JoinLobbyWithCodeResponseEvent -= JoinLobbyWithCodeCallback;
        createLobbyForm.CreateLobbyButtonClicked -= CreateLobbyButtonCallback;
        joinLobbyWithCodeForm.JoinButtonClicked -= JoinLobbyWithCodeButtonCallback;
    }

    public void GetLobbiesWithName()
    {
        Debug.Log(searchLobbyName.text);
        client.AddToSendQueue(MessageFrameBuilder.GetLobbiesWithNameFrame(new LobbyNameDto
        {
            Name = searchLobbyName.text
        }));
    }

    private void GetAllLobbiesCallback(ResponseObjectDto<LobbiesInfoDto> dto)
    {
        if (dto.OkResult)
        {
            string message = Translation.GetTranslation(Translation.StringType.ActiveLobbies);
            //lobbyCountText.text = $"{dto.Object.Lobbies.Count} salon(s) actif(s)";
            lobbyCountText.text = $"{dto.Object.Lobbies.Count} {message}";
            CreateLobbiesUi(dto);
            DeleteNonExistingLobbies(dto);
        }
        else
        {
            string message = Translation.GetTranslation(Translation.StringType.GettingLobbiesError);
            //popupLobbyPanel.ShowMessage("error while getting all lobbies", Color.red);
            popupLobbyPanel.ShowMessage(message, Color.red);
        }
    }

    private void GetLobbiesWithNameCallback(ResponseObjectDto<LobbiesInfoDto> dto)
    {
        if (dto.OkResult)
        {
            string message = Translation.GetTranslation(Translation.StringType.ActiveLobbies);
            //lobbyCountText.text = $"{dto.Object.Lobbies.Count} salon(s) actif(s)";
            lobbyCountText.text = $"{dto.Object.Lobbies.Count} {message}";
            CreateLobbiesUi(dto);
            DeleteNonExistingLobbies(dto);
        }
        else
        {
            string message = Translation.GetTranslation(Translation.StringType.GettingLobbiesError);
            //popupLobbyPanel.ShowMessage("error while getting all lobbies", Color.red);
            popupLobbyPanel.ShowMessage(message, Color.red);
        }
    }

    private void CreateLobbyCallback(ResponseObjectDto<LobbyDto> dto)
    {
        if (dto.OkResult)
        {
            gameObject.SetActive(false);
            inLobby.gameObject.SetActive(true);
            inLobby.SetUp(dto.Object);
        }
        else
        {
            string message = Translation.GetTranslation(Translation.StringType.CreatingLobbyErrorMessage);
            string error = Translation.GetTranslationError(dto.ErrorType!.Value);
            //popupLobbyPanel.ShowMessage($"error while creating lobby : {dto.ErrorType}", Color.red);
            popupLobbyPanel.ShowMessage($"{message} : {error}", Color.red);
        }
    }

    private void JoinLobbyWithCodeCallback(ResponseDto dto)
    {
        if (!dto.OkResult)
        {
            // Translation
            string message = Translation.GetTranslation(Translation.StringType.ErrorJoiningLobbyCode);
            //string message = "error while joining lobby with code";
            string error = Translation.GetTranslationError(dto.ErrorType!.Value);
            //popupLobbyPanel.ShowMessage($"error while joining lobby with code : {dto.ErrorType}", Color.red);
            popupLobbyPanel.ShowMessage($"{message} : {error}", Color.red);
        }
    }

    private void CreateLobbyButtonCallback()
    {
        int numberOfPlayer = int.Parse(createLobbyForm.LobbyNumberOfPlayerDropDown.options[createLobbyForm.LobbyNumberOfPlayerDropDown.value].text);
        client.AddToSendQueue(MessageFrameBuilder.CreateLobbyFrame(new CreateLobbyDto
        {
            MaxNumberOfPlayer = numberOfPlayer,
            Name = createLobbyForm.LobbyNameInput.text,
            Public = !createLobbyForm.LobbyPrivate.isOn
        }));
    }

    private void JoinLobbyWithCodeButtonCallback()
    {
        client.AddToSendQueue(MessageFrameBuilder.JoinLobbyWithCodeFrame(new LobbyCodeDto
        {
            Code = joinLobbyWithCodeForm.CodeInput.text
        }));
    }

    private void CreateLobbiesUi(ResponseObjectDto<LobbiesInfoDto> dto)
    {
        foreach (LobbyInfoDto lobbyDto in dto.Object.Lobbies)
        {
            LobbyListItem lobbyListItem = lobbyPanels.FirstOrDefault(lp => lp.GetLobbyId() == lobbyDto.Id);
            if (lobbyListItem == null)
            {
                GameObject lobbyUi = Instantiate(lobbyPrefab, lobbyContainer.transform);
                LobbyListItem lli = lobbyUi.GetComponent<LobbyListItem>();
                lli.LobbySelected += LobbySelectedCallback;
                lli.SetUpLobby(lobbyDto);
                lobbyPanels.Add(lli);
            }
            else
            {
                lobbyListItem.SetUpLobby(lobbyDto);
            }
        }
    }

    private void DeleteNonExistingLobbies(ResponseObjectDto<LobbiesInfoDto> dto)
    {
        foreach (LobbyListItem lobbyPanel in lobbyPanels.ToList()) // Copy of list here
        {
            if (dto.Object.Lobbies.Any(l => l.Id == lobbyPanel.GetLobbyId())) continue;
            lobbyPanel.LobbySelected -= LobbySelectedCallback;
            lobbyPanels.Remove(lobbyPanel);
            Destroy(lobbyPanel.gameObject);
        }
    }

    private void LobbySelectedCallback(LobbyInfoDto lobbyInfoDto)
    {
        joinButton.interactable = true;
        lobbySelected = lobbyInfoDto;
    }

    private void JoinLobbyCallback(ResponseObjectDto<LobbyDto> dto)
    {
        if (dto.OkResult)
        {
            gameObject.SetActive(false);
            inLobby.gameObject.SetActive(true);
            inLobby.SetUp(dto.Object);
        }
        else
        {
            string message = Translation.GetTranslation(Translation.StringType.FailedToJoinLobby);
            string error = Translation.GetTranslationError(dto.ErrorType!.Value);
            //popupLobbyPanel.ShowMessage($"failed to joined lobby : {dto.ErrorType}", Color.red);
            popupLobbyPanel.ShowMessage($"{message} : {error}", Color.red);
        }
    }

    public void JoinLobby()
    {
        client.AddToSendQueue(MessageFrameBuilder.JoinLobbyFrame(new JoinLobbyDto()
        {
            LobbyIdToJoin = lobbySelected.Id
        }));
    }

    private IEnumerator UpdateLobbies()
    {
        while (string.IsNullOrWhiteSpace(searchLobbyName.text))
        {
            yield return new WaitForSeconds(5f);
            client.AddToSendQueue(MessageFrameBuilder.GetAllLobbyFrame());
        }
    }
}
