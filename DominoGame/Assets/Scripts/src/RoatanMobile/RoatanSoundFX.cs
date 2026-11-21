using UnityEngine;

public class RoatanSoundFX : MonoBehaviour
{
    private const string MutePrefKey = "RoatanDomino_Muted";

    public static RoatanSoundFX Instance { get; private set; }
    public bool IsMuted { get; private set; }

    private AudioSource source;
    private AudioClip clipPlace;
    private AudioClip clipSlam;
    private AudioClip clipHeavySlam;
    private AudioClip clipTap;
    private AudioClip clipDraw;
    private AudioClip clipPass;
    private AudioClip clipWin;
    private AudioClip clipEmote;
    private AudioClip clipBreakSting;
    private AudioClip clipSixLovSting;
    private AudioClip clipCutThroatSting;
    private AudioClip clipPipTick;

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
        clipSlam = SynthesizeWoodSlam(heavy: false);
        clipHeavySlam = SynthesizeWoodSlam(heavy: true);
        clipTap = SynthesizeSoftTone(520f, 0.055f, 0.18f);
        clipDraw = SynthesizeSoftTone(340f, 0.09f, 0.22f);
        clipPass = SynthesizeSoftTone(240f, 0.14f, 0.18f);
        clipWin = SynthesizeVictoryChord();
        clipEmote = SynthesizeSteelPanPing();
        clipBreakSting = SynthesizeBreakSting();
        clipSixLovSting = SynthesizeSixLovFanfare();
        clipCutThroatSting = SynthesizeCutThroatSting();
        clipPipTick = SynthesizeSoftTone(760f, 0.032f, 0.14f);
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        PlayerPrefs.SetInt(MutePrefKey, IsMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void PlayPlace() => Play(clipPlace, 0.75f);
    public void PlaySlam(bool heavy = false) => Play(heavy ? clipHeavySlam : clipSlam, heavy ? 0.98f : 0.88f);
    public void PlayTap() => Play(clipTap, 0.55f);
    public void PlayDraw() => Play(clipDraw, 0.6f);
    public void PlayPass() => Play(clipPass, 0.55f);
    public void PlayWin() => Play(clipWin, 0.7f);
    public void PlayEmoteCallout() => Play(clipEmote, 0.62f);
    public void PlayBreakSting() => Play(clipBreakSting, 0.85f);
    public void PlaySixLovSting() => Play(clipSixLovSting, 0.85f);
    public void PlayCutThroatSting() => Play(clipCutThroatSting, 0.85f);
    public void PlayPipTick() => Play(clipPipTick, 0.42f);

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

    private static AudioClip SynthesizeWoodSlam(bool heavy)
    {
        int sampleRate = 22050;
        float dur = heavy ? 0.28f : 0.20f;
        int samples = (int)(sampleRate * dur);
        float[] data = new float[samples];
        System.Random rng = new System.Random(heavy ? 99 : 77);

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            // Heavy mahogany table thump (pitch-dropping low body)
            float thumpFreq = Mathf.Lerp(heavy ? 125f : 145f, heavy ? 52f : 68f, Mathf.Clamp01(t * 12f));
            float thump = Mathf.Sin(2f * Mathf.PI * thumpFreq * t) * Mathf.Exp(-t * (heavy ? 14f : 20f)) * (heavy ? 0.85f : 0.72f);

            // Wood table hollow resonance
            float woodRes = Mathf.Sin(2f * Mathf.PI * 290f * t) * Mathf.Exp(-t * 28f) * 0.36f
                          + Mathf.Sin(2f * Mathf.PI * 620f * t) * Mathf.Exp(-t * 44f) * 0.22f;

            // Crisp bone/ivory domino snap transient
            float snap = (((float)rng.NextDouble() * 2f - 1f) * 0.65f + Mathf.Sin(2f * Mathf.PI * 1980f * t) * 0.45f)
                       * Mathf.Exp(-t * 95f);

            float mixed = (thump + woodRes + snap) * (heavy ? 1.18f : 1.0f);
            data[i] = Mathf.Clamp(mixed, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(heavy ? "HeavyWoodSlam" : "WoodSlam", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip SynthesizeSteelPanPing()
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * 0.22f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 16f);
            float val = Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.42f
                      + Mathf.Sin(2f * Mathf.PI * 1318.5f * t) * 0.22f * Mathf.Exp(-t * 10f)
                      + Mathf.Sin(2f * Mathf.PI * 987.77f * t) * (t > 0.06f ? 0.35f * Mathf.Exp(-(t - 0.06f) * 18f) : 0f);
            data[i] = Mathf.Clamp(val * env, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("SteelPanEmote", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip SynthesizeBreakSting()
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * 0.52f);
        float[] data = new float[samples];
        float[] notes = { 392.00f, 349.23f, 293.66f, 220.00f }; // Dramatic descending G-F-D-A break

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float val = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 12f) * 0.45f;
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.085f;
                if (t < start) continue;
                float lt = t - start;
                val += Mathf.Sin(2f * Mathf.PI * notes[n] * lt) * Mathf.Exp(-lt * 8f) * 0.26f;
            }
            data[i] = Mathf.Clamp(val, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("BreakSting", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip SynthesizeSixLovFanfare()
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * 0.78f);
        float[] data = new float[samples];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f };

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float val = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.09f;
                if (t < start) continue;
                float lt = t - start;
                float env = Mathf.Exp(-lt * 5.2f);
                val += (Mathf.Sin(2f * Mathf.PI * notes[n] * lt) * 0.24f
                      + Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * lt) * 0.09f) * env;
            }
            data[i] = Mathf.Clamp(val, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("SixLovFanfare", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip SynthesizeCutThroatSting()
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * 0.62f);
        float[] data = new float[samples];
        float[] notes = { 440.00f, 554.37f, 659.25f, 880.00f };

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float val = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 14f) * 0.4f;
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.075f;
                if (t < start) continue;
                float lt = t - start;
                val += Mathf.Sin(2f * Mathf.PI * notes[n] * lt) * Mathf.Exp(-lt * 6.5f) * 0.25f;
            }
            data[i] = Mathf.Clamp(val, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("CutThroatSting", samples, 1, sampleRate, false);
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
