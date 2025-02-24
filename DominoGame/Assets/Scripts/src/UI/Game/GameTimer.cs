using TMPro;

using UnityEngine;

public class GameTimer : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;

    private bool timerStarted;
    private int timerDuration;
    private float timerTime;

    private void Update()
    {
        if (!timerStarted)
            return;

        timerTime = Mathf.Max(timerTime - Time.deltaTime, 0);
        int minutes = Mathf.FloorToInt(timerTime / 60f);
        int seconds = Mathf.FloorToInt(timerTime) % 60;
        timerText.text = $"{minutes:00} : {seconds:00}";
    }

    public void StartTimer(int newTimerDuration)
    {
        timerStarted = true;
        timerDuration = newTimerDuration;
        timerTime = timerDuration;
    }
}
