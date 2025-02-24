using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using TMPro;

using UnityEngine;

public class HistoryGame : MonoBehaviour
{
    [SerializeField] private TMP_Text historyGameText;

    public long gameId { get; private set; }

    public void SetUp(ShortHistoryGameDto dto)
    {
        gameId = dto.Id;
        string type = dto.Ranked ? "Ranked" : "Normal";
        historyGameText.text = $"{type} {dto.EndTime}";
    }
}
