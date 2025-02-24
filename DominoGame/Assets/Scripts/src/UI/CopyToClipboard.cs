using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class CopyToClipboard : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;

    private Button button;

    private void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ButtonOnClick);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(ButtonOnClick);
    }

    private void ButtonOnClick()
    {
        GUIUtility.systemCopyBuffer = targetText.text;
    }
}
