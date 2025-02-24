using System.Collections;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;

using TMPro;

using UnityEngine;

public class GameStartTimer : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text timerValueText;
    [SerializeField] private TMP_Text stateText;

    private int timer;

    public void StartTimer(TimerDto timerDto)
    {
        timer = timerDto.Duration;
        timerValueText.text = $"{timer}";
        timerText.gameObject.SetActive(true);
        timerValueText.gameObject.SetActive(true);
        stateText.gameObject.SetActive(false);
        StartCoroutine(nameof(TimerCoroutine));
    }

    private IEnumerator TimerCoroutine()
    {
        while (timer > 0)
        {
            yield return new WaitForSeconds(1);
            timer--;
            timerValueText.text = $"{timer}";
        }
        gameObject.SetActive(false);
    }
}
