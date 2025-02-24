using TMPro;

using UnityEngine;
using UnityEngine.UI;

public class PasteFromClipboard : MonoBehaviour
{
    [SerializeField] private TMP_InputField targetInput;

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
        targetInput.text = GUIUtility.systemCopyBuffer;
        targetInput.Select();
    }
}
