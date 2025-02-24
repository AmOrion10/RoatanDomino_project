using System.Collections;

using UnityEngine;
using UnityEngine.UI;

public class PlayerTimer : MonoBehaviour
{
    public Image PlayerTimerIndicator;

    private Coroutine autoClose;
    private float timerPerTurn;

    public void StartTimer(int timerDuration)
    {
        timerPerTurn = timerDuration;
        PlayerTimerIndicator.enabled = true;
        autoClose = StartCoroutine(AutoClosePopup(timerDuration));
        PlayerTimerIndicator.fillAmount = 1f;
    }

    private IEnumerator AutoClosePopup(float delay)
    {
        yield return new WaitForSeconds(delay);
        StopTimer();
    }

    public void StopTimer()
    {
        StopAllCoroutines();
        PlayerTimerIndicator.enabled = false;
    }

    private void Update()
    {
        if (autoClose != null && PlayerTimerIndicator.enabled)
        {
            PlayerTimerIndicator.fillAmount -= Time.deltaTime / timerPerTurn;
        }
    }
}
