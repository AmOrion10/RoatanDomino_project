using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;
using System.Linq;

public class Parameters : MonoBehaviour
{
    public TMP_Dropdown Resolution;
    public TMP_Dropdown Window;
    public TMP_Dropdown Langue;
    public Slider SliderSons;
    public Slider SliderMusiques;

    public int ResolutionLastAplliedValue = 0;
    public int FenetreLastAplliedValue = 0;
    public int LangueLastAplliedValue = MenuPrincipal.IdLanguageUsed;

    private int ResolutionPreviousAppliedValue = 0;
    private int FenetrePreviousAppliedValue = 0;
    private int LanguePreviousAppliedValue = MenuPrincipal.IdLanguageUsed;

    private Resolution[] SupportedResolutions;
    private const int MaxSliderValue = 100;
    private readonly float ScrollSpeed = 50.0f;

    private AudioSource musicSource;
    private AudioSource soundEffectsSource;

    public void Start()
    {
        SupportedResolutions = Screen.resolutions;
        // Remove the double resolutions
        SupportedResolutions = SupportedResolutions.Distinct(new ResolutionComparer()).ToArray();
        // Keep only the resolutions that are 16:9
        SupportedResolutions = SupportedResolutions.Where(res => res.width / 16 == res.height / 9).ToArray();
        // Reverse the resolutions
        SupportedResolutions = SupportedResolutions.Reverse().ToArray();

        // Pour avoir un défilement plus rapide
        Resolution.transform.Find("Template").GetComponent<ScrollRect>().scrollSensitivity = ScrollSpeed;
        Window.transform.Find("Template").GetComponent<ScrollRect>().scrollSensitivity = ScrollSpeed;
        Langue.transform.Find("Template").GetComponent<ScrollRect>().scrollSensitivity = ScrollSpeed;

        foreach (var res in SupportedResolutions)
            Resolution.options.Add(new TMP_Dropdown.OptionData(res.width + "x" + res.height));

        Resolution.RefreshShownValue();

        // Find audio sources
        musicSource = GameObject.FindGameObjectWithTag("MusicSource").GetComponent<AudioSource>();
        soundEffectsSource = GameObject.FindGameObjectWithTag("EffectSource").GetComponent<AudioSource>();

        // Load saved volume settings
        SliderSons.value = PlayerPrefs.GetFloat("SoundEffectsVolume", MaxSliderValue);
        SliderMusiques.value = PlayerPrefs.GetFloat("MusicVolume", MaxSliderValue);

        // Set initial volume
        UpdateMusicVolume(SliderMusiques.value);
        UpdateSoundEffectsVolume(SliderSons.value);

        // Add listeners for sliders
        SliderSons.onValueChanged.AddListener(UpdateSoundEffectsVolume);
        SliderMusiques.onValueChanged.AddListener(UpdateMusicVolume);
    }

    public void AppliquerParametres()
    {
        Screen.SetResolution(SupportedResolutions[Resolution.value].width, SupportedResolutions[Resolution.value].height, true);
        switch (Window.value)
        {
            case 0:
                Screen.fullScreen = true;
                break;
            case 1:
            case 2:
                Screen.fullScreen = false;
                break;
        }

        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[Langue.value];
        MenuPrincipal.IdLanguageUsed = Langue.value;

        if ((ResolutionPreviousAppliedValue != ResolutionLastAplliedValue) && (ResolutionLastAplliedValue != Resolution.value))
        {
            ResolutionPreviousAppliedValue = ResolutionLastAplliedValue;
        }
        if ((FenetrePreviousAppliedValue != FenetreLastAplliedValue) && (FenetreLastAplliedValue != Window.value))
        {
            FenetrePreviousAppliedValue = FenetreLastAplliedValue;
        }
        if ((LanguePreviousAppliedValue != LangueLastAplliedValue) && (LangueLastAplliedValue != Langue.value))
        {
            LanguePreviousAppliedValue = LangueLastAplliedValue;
        }

        ResolutionLastAplliedValue = Resolution.value;
        FenetreLastAplliedValue = Window.value;
        LangueLastAplliedValue = Langue.value;

        // Save volume settings
        PlayerPrefs.SetFloat("SoundEffectsVolume", SliderSons.value);
        PlayerPrefs.SetFloat("MusicVolume", SliderMusiques.value);
        PlayerPrefs.Save();
    }

    public void DefaultParametres()
    {
        Resolution.value = 0;
        Window.value = 0;
        Langue.value = 1;
        SliderSons.value = MaxSliderValue;
        SliderMusiques.value = MaxSliderValue;

        UpdateSoundEffectsVolume(SliderSons.value);
        UpdateMusicVolume(SliderMusiques.value);
    }

    public void UndoParametres()
    {
        Resolution.value = ResolutionPreviousAppliedValue;
        Window.value = FenetrePreviousAppliedValue;
        Langue.value = LanguePreviousAppliedValue;

        // Reload saved volume settings
        SliderSons.value = PlayerPrefs.GetFloat("SoundEffectsVolume", MaxSliderValue);
        SliderMusiques.value = PlayerPrefs.GetFloat("MusicVolume", MaxSliderValue);

        UpdateSoundEffectsVolume(SliderSons.value);
        UpdateMusicVolume(SliderMusiques.value);
    }

    private void UpdateSoundEffectsVolume(float value)
    {
        if (soundEffectsSource != null)
        {
            soundEffectsSource.volume = value;
            Debug.Log("Sound Effects Volume Updated: " + soundEffectsSource.volume);
        }
    }

    private void UpdateMusicVolume(float value)
    {
        if (musicSource != null)
        {
            musicSource.volume = value;
            Debug.Log("Music Volume Updated: " + musicSource.volume);
        }
    }

    // Comparateur custom pour comparer les resolutions basé sur la largeur et la hauteur uniquement pour pouvoir retirer les doublons
    private class ResolutionComparer : IEqualityComparer<Resolution>
    {
        public bool Equals(Resolution x, Resolution y)
        {
            return x.width == y.width && x.height == y.height;
        }
        public int GetHashCode(Resolution obj)
        {
            return obj.width.GetHashCode() ^ obj.height.GetHashCode();
        }
    }

}
