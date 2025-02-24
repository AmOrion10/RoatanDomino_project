using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using TMPro;

using UnityEngine;

public class PanelStats : MonoBehaviour
{
    [SerializeField] private TMP_Text gamePlayedText;
    [SerializeField] private TMP_Text gameWonText;
    [SerializeField] private TMP_Text gameLostText;
    [SerializeField] private TMP_Text eloText;

    private IClient client;

    private void OnEnable()
    {
        client = Networking.Instance.GetClient();
        client.EventHandlers.GetInfoProfileResponseEvent += GetInfoProfileCallback;
        client.AddToSendQueue(MessageFrameBuilder.GetInfoProfileFrame());
    }

    private void OnDisable()
    {
        client.EventHandlers.GetInfoProfileResponseEvent -= GetInfoProfileCallback;
    }

    private void GetInfoProfileCallback(ResponseObjectDto<InfoProfileDto> dto)
    {
        SetUpStats(dto.Object);
    }

    private void SetUpStats(InfoProfileDto infoProfileDto)
    {
        gamePlayedText.text = infoProfileDto.Statistic.NumberOfGamePlayed.ToString();
        gameWonText.text = infoProfileDto.Statistic.NumberOfGameWon.ToString();
        gameLostText.text = infoProfileDto.Statistic.NumberOfGameLost.ToString();
        eloText.text = infoProfileDto.User.Score.ToString();
    }
}
