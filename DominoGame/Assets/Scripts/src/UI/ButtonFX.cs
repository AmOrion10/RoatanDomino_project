using UnityEngine;
using UnityEngine.UI;

public class ButtonFX : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private EffectPlayer audioPlayer;
    [SerializeField] private float hoverFxPitch;
    [SerializeField] private float clickFxPitch;

    public void HoverSound()
    {
        if (!button.interactable) return;
        audioPlayer.PlayHoverSoundEffect(hoverFxPitch);
    }

    public void ClickSound()
    {
        Debug.Log(button.name);
        if (!button.interactable) return;
        audioPlayer.PlayClickSoundEffect(clickFxPitch);
    }
}
