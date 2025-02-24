using System;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;

using TMPro;

using UnityEngine;

public class GamePlayerItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerLoginText;
    [SerializeField] private TMP_Text dominoCountText;
    [SerializeField] private PlayerTimer playerTimer;

    public event Action<PlayerDto> ClickOnProfileCallback;
    public PlayerDto Player { get; private set; }
    public bool IsActive { get; set; }

    public void SetUpPlayer(PlayerDto player)
    {
        Player = player;
        playerLoginText.text = player.Name;
        IsActive = true;
    }

    public void SetNumberOfDomino(int numberOfDomino)
    {
        dominoCountText.text = $"{numberOfDomino}";
    }

    public void StartTurn(int timerDuration)
    {
        playerTimer.StartTimer(timerDuration);
    }

    public void StopTurn()
    {
        playerTimer.StopTimer();
    }

    public void ClickOnProfile()
    {
        ClickOnProfileCallback?.Invoke(Player);
    }
}
