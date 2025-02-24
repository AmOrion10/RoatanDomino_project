using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using UnityEngine;

public class PanelHistory : MonoBehaviour
{
    [SerializeField] private GameObject historyGameContainer;
    [SerializeField] private HistoryGame historyGamePrefab;

    private readonly List<HistoryGame> historyGames = new();
    private IClient client;

    private void OnEnable()
    {
        client = Networking.Instance.GetClient();
        client.EventHandlers.GetAllHistoryGamesResponseEvent += GetAllHistoryGamesResponseCallback;
        client.AddToSendQueue(MessageFrameBuilder.GetAllHistoryGames());
    }

    private void OnDisable()
    {
        client.EventHandlers.GetAllHistoryGamesResponseEvent -= GetAllHistoryGamesResponseCallback;
    }

    private void GetAllHistoryGamesResponseCallback(ResponseObjectDto<AllHistoryGamesDto> dto)
    {
        if (dto.OkResult)
        {
            ClearHistoryGames();
            foreach (ShortHistoryGameDto shortHistoryGameDto in dto.Object.HistoryGames)
            {
                HistoryGame newItem = Instantiate(historyGamePrefab, historyGameContainer.transform);
                newItem.SetUp(shortHistoryGameDto);
                historyGames.Add(newItem);
            }
        }
    }

    private void ClearHistoryGames()
    {
        foreach (HistoryGame historyGame in historyGames.ToList())
        {
            Destroy(historyGame.gameObject);
            historyGames.Remove(historyGame);
        }
    }
}
