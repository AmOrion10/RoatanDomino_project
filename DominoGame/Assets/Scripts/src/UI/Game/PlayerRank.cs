using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using TMPro;

using UnityEngine;

public class PlayerRank : MonoBehaviour
{
    [SerializeField] private TMP_Text playerName;

    public void SetPlayer(PlayerDto player)
    {
        playerName.text = player.Name;
    }
}
