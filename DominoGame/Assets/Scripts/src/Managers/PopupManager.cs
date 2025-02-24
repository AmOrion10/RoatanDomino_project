using System.Collections;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class PopupManager : MonoBehaviour
{
    private const float DelayBeforeClosing = 3f;
    public GameObject MessagePanel;
    public TMP_Text MessageText;
    public Image WhiteLine;

    private Coroutine autoClose;

    public void ShowMessage(string message, Color messageColor)
    {
        MessagePanel.SetActive(true);
        MessageText.color = messageColor;
        MessageText.text = message;

        autoClose = StartCoroutine(AutoClosePopup(DelayBeforeClosing));

        WhiteLine.fillAmount = 1f;
    }

    private IEnumerator AutoClosePopup(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClosePopup();
    }

    private void ClosePopup()
    {
        MessagePanel.SetActive(false);
    }

    private void Update()
    {
        if (MessagePanel.activeSelf && autoClose != null)
        {
            WhiteLine.fillAmount -= Time.deltaTime / DelayBeforeClosing;
        }
    }
}