using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PasswordInfo : MonoBehaviour
{
    public Button ToggleShow;
    private bool Show;
    public TMP_InputField InputPassword;
    public Image ShowSprite;
    public Sprite SpriteShow;
    public Sprite SpriteHide;

    private void Start()
    {
        Show = false;
        ToggleShow.onClick.AddListener(ToggleVisibility);
        ShowSprite = ToggleShow.GetComponent<Image>();
    }

    public void ToggleVisibility()
    {
        Show = !Show;
        if (Show)
        {
            InputPassword.contentType = TMP_InputField.ContentType.Standard;
            ShowSprite.sprite = SpriteShow;
        }
        else
        {
            InputPassword.contentType = TMP_InputField.ContentType.Password;
            ShowSprite.sprite = SpriteHide;
        }
        InputPassword.ForceLabelUpdate();
    }
}
