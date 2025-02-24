using UnityEngine;

public class EffectPlayer : MonoBehaviour
{
    public static EffectPlayer Instance { get; private set; }
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    private const int MaxSliderValue = 100;

    private void Awake()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("EffectSource");
        if (objs.Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadVolumeSettings();
    }

    private void LoadVolumeSettings()
    {
        // Load volume preferences from the client
        float effectVolume = PlayerPrefs.GetFloat("SoundEffectsVolume", MaxSliderValue);

        Instance.GetComponent<AudioSource>().volume = effectVolume;
        Debug.Log("Effect Volume set to: " + Instance.GetComponent<AudioSource>().volume);
    }

    public void PlayHoverSoundEffect(float pitch)
    {
        Instance.GetComponent<AudioSource>().pitch = pitch;
        Instance.GetComponent<AudioSource>().PlayOneShot(hoverClip);
    }

    public void PlayClickSoundEffect(float pitch)
    {
        Instance.GetComponent<AudioSource>().pitch = pitch;
        Instance.GetComponent<AudioSource>().PlayOneShot(clickClip);
    }
}
