using System.Collections.Generic;
using System.Linq;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;

using UnityEngine;

public class PlayerRankings : MonoBehaviour
{
    [SerializeField] private GameObject panelRankings;
    [SerializeField] private GameObject panelError;
    [SerializeField] private List<PlayerRank> playerRanks = new();

    public void ShowRankings(LobbyDto lobby, EndGameDto dto)
    {
        foreach (PlayerRank rank in playerRanks)
        {
            rank.gameObject.SetActive(false);
        }

        if (!dto.NormalFinish)
        {
            panelRankings.SetActive(false);
            panelError.SetActive(true);
            return;
        }

        for (int i = 0; i < dto.Rankings.Count && i < 3; i++)
        {
            playerRanks[i].gameObject.SetActive(true);
            playerRanks[i].SetPlayer(lobby.Players.First(p => p.Id == dto.Rankings[i].Id));
        }
    }
}
