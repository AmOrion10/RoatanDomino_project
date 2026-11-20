using UnityEngine;

public class RoatanSoundFX : MonoBehaviour
{
    private const string MutePrefKey = "RoatanDomino_Muted";

    public static RoatanSoundFX Instance { get; private set; }
    public bool IsMuted { get; private set; }

    private AudioSource source;
    private AudioClip clipPlace;
    private AudioClip clipTap;
    private AudioClip clipDraw;
    private AudioClip clipPass;
    private AudioClip clipWin;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        IsMuted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;

        clipPlace = SynthesizeDominoClack();
        clipTap = SynthesizeSoftTone(520f, 0.055f, 0.18f);
        clipDraw = SynthesizeSoftTone(340f, 0.09f, 0.22f);
        clipPass = SynthesizeSoftTone(240f, 0.14f, 0.18f);
        clipWin = SynthesizeVictoryChord();
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        PlayerPrefs.SetInt(MutePrefKey, IsMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void PlayPlace() => Play(clipPlace, 0.75f);
    public void PlayTap() => Play(clipTap, 0.55f);
    public void PlayDraw() => Play(clipDraw, 0.6f);
    public void PlayPass() => Play(clipPass, 0.55f);
    public void PlayWin() => Play(clipWin, 0.7f);

    private void Play(AudioClip clip, float volume)
    {
        if (IsMuted || clip == null || source == null) return;
        source.PlayOneShot(clip, volume);
    }

    private static AudioClip SynthesizeDominoClack()
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * 0.09f);
        float[] data = new float[samples];
        System.Random rng = new System.Random(42);

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 58f);
            float click = Mathf.Sin(2f * Mathf.PI * 1150f * t) * 0.55f
                        + Mathf.Sin(2f * Mathf.PI * 2450f * t) * 0.30f
                        + ((float)rng.NextDouble() * 2f - 1f) * 0.35f * Mathf.Exp(-t * 110f);
            data[i] = Mathf.Clamp(click * env, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("DominoClack", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip SynthesizeSoftTone(float freq, float duration, float amp)
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Sin(Mathf.PI * (t / duration));
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * amp;
        }

        AudioClip clip = AudioClip.Create("SoftTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip SynthesizeVictoryChord()
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * 0.55f);
        float[] data = new float[samples];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float val = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.08f;
                if (t < start) continue;
                float lt = t - start;
                float env = Mathf.Exp(-lt * 6.5f);
                val += Mathf.Sin(2f * Mathf.PI * notes[n] * lt) * env * 0.22f;
            }
            data[i] = Mathf.Clamp(val, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("VictoryChord", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
