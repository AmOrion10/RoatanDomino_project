using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    private static MusicPlayer instance = null;
    public static MusicPlayer Instance
    {
        get { return instance; }
    }
    [SerializeField] private AudioSource audioSource;
    private const int MaxSliderValue = 100;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        else
        {
            instance = this;
        }
        DontDestroyOnLoad(this.gameObject);

        LoadVolumeSettings();
    }

    private void LoadVolumeSettings()
    {
        // Load volume preferences from the client
        float musicVolume = PlayerPrefs.GetFloat("MusicVolume", MaxSliderValue);

        audioSource.volume = musicVolume;
        Debug.Log("Music Volume set to: " + audioSource.volume);
    }
}
