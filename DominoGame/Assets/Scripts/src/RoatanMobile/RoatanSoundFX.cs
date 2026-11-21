using System.Collections.Generic;

using UnityEngine;

public class RoatanSoundFX : MonoBehaviour
{
    private const string MutePrefKey = "RoatanDomino_Muted";
    private const int SampleRate = 44100;

    public static RoatanSoundFX Instance { get; private set; }
    public bool IsMuted { get; private set; }

    [SerializeField] private AudioClip[] tilePlaceClips;
    [SerializeField] private AudioClip[] tileSlamClips;
    [SerializeField] private AudioClip[] tileDealClips;

    private AudioSource source;
    private AudioClip[] heavySlamClips;
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

        LoadOrSynthesizeTileClips();

        clipTap = SynthesizeSoftTone(520f, 0.055f, 0.18f);
        clipDraw = tileDealClips != null && tileDealClips.Length > 0 ? tileDealClips[0] : SynthesizeResinTileHit(201, isDeal: true);
        clipPass = SynthesizeSoftTone(240f, 0.14f, 0.18f);
        clipWin = SynthesizeVictoryChord();
        clipEmote = SynthesizeSteelPanPing();
        clipBreakSting = SynthesizeBreakSting();
        clipSixLovSting = SynthesizeSixLovFanfare();
        clipCutThroatSting = SynthesizeCutThroatSting();
        clipPipTick = SynthesizeSoftTone(760f, 0.032f, 0.14f);
    }

    private void LoadOrSynthesizeTileClips()
    {
        // Attempt loading user-supplied wood/resin SFX from Resources/Audio/SFX/
        if (tilePlaceClips == null || tilePlaceClips.Length == 0)
        {
            tilePlaceClips = LoadClipsFromResources("Audio/SFX/TilePlace", "place");
        }
        if (tileSlamClips == null || tileSlamClips.Length == 0)
        {
            tileSlamClips = LoadClipsFromResources("Audio/SFX/TileSlam", "slam");
        }
        if (tileDealClips == null || tileDealClips.Length == 0)
        {
            tileDealClips = LoadClipsFromResources("Audio/SFX/TileDeal", "deal");
        }

        // Upgraded procedural resin-on-mahogany fallback (3 seeded variations per category)
        if (tilePlaceClips == null || tilePlaceClips.Length == 0)
        {
            tilePlaceClips = new[]
            {
                SynthesizeResinTileHit(41, isDeal: false),
                SynthesizeResinTileHit(73, isDeal: false),
                SynthesizeResinTileHit(109, isDeal: false)
            };
        }

        if (tileSlamClips == null || tileSlamClips.Length == 0)
        {
            tileSlamClips = new[]
            {
                SynthesizeHeavyResinSlam(131, heavy: false),
                SynthesizeHeavyResinSlam(157, heavy: false),
                SynthesizeHeavyResinSlam(193, heavy: false)
            };
        }

        heavySlamClips = new[]
        {
            SynthesizeHeavyResinSlam(223, heavy: true),
            SynthesizeHeavyResinSlam(251, heavy: true)
        };

        if (tileDealClips == null || tileDealClips.Length == 0)
        {
            tileDealClips = new[]
            {
                SynthesizeResinTileHit(307, isDeal: true),
                SynthesizeResinTileHit(331, isDeal: true),
                SynthesizeResinTileHit(359, isDeal: true)
            };
        }
    }

    private static AudioClip[] LoadClipsFromResources(string subfolderPath, string nameFilter)
    {
        AudioClip[] direct = Resources.LoadAll<AudioClip>(subfolderPath);
        if (direct != null && direct.Length > 0)
        {
            return direct;
        }

        AudioClip[] all = Resources.LoadAll<AudioClip>("Audio/SFX");
        if (all == null || all.Length == 0)
        {
            return null;
        }

        List<AudioClip> matched = new List<AudioClip>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name.ToLowerInvariant().Contains(nameFilter))
            {
                matched.Add(all[i]);
            }
        }
        return matched.Count > 0 ? matched.ToArray() : null;
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        PlayerPrefs.SetInt(MutePrefKey, IsMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void PlayPlace() => PlayRandomized(tilePlaceClips, 0.86f);
    public void PlaySlam(bool heavy = false) => PlayRandomized(heavy ? heavySlamClips : tileSlamClips, heavy ? 1.0f : 0.94f);
    public void PlayDeal() => PlayRandomized(tileDealClips, 0.72f);
    public void PlayDraw() => PlayRandomized(tileDealClips, 0.70f);
    public void PlayTap() => PlayFixed(clipTap, 0.52f);
    public void PlayPass() => PlayFixed(clipPass, 0.55f);
    public void PlayWin() => PlayFixed(clipWin, 0.72f);
    public void PlayEmoteCallout() => PlayFixed(clipEmote, 0.62f);
    public void PlayBreakSting() => PlayFixed(clipBreakSting, 0.85f);
    public void PlaySixLovSting() => PlayFixed(clipSixLovSting, 0.85f);
    public void PlayCutThroatSting() => PlayFixed(clipCutThroatSting, 0.85f);
    public void PlayPipTick() => PlayFixed(clipPipTick, 0.42f);

    private void PlayRandomized(AudioClip[] clips, float baseVolume)
    {
        if (IsMuted || clips == null || clips.Length == 0 || source == null) return;
        AudioClip chosen = clips[Random.Range(0, clips.Length)];
        if (chosen == null) return;

        source.pitch = Random.Range(0.94f, 1.06f);
        float vol = Mathf.Clamp01(baseVolume * Random.Range(0.90f, 1.00f));
        source.PlayOneShot(chosen, vol);
    }

    private void PlayFixed(AudioClip clip, float volume)
    {
        if (IsMuted || clip == null || source == null) return;
        source.pitch = 1.0f;
        source.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Synthesizes a realistic heavy resin domino hitting a solid mahogany table:
    /// - 3ms bandpass-filtered transient click (resin-on-wood impact)
    /// - 55ms exponentially decaying low-mid wooden body thump (190Hz -> 58Hz)
    /// - Subtle wood room decay tail
    /// </summary>
    private static AudioClip SynthesizeResinTileHit(int seed, bool isDeal)
    {
        float duration = isDeal ? 0.085f : 0.135f;
        int samples = (int)(SampleRate * duration);
        float[] data = new float[samples];
        System.Random rng = new System.Random(seed);

        // 2-pole biquad bandpass filter state for crisp resin transient (~2750Hz, Q=2.4)
        float bpCenter = isDeal ? 3100f : 2750f + (seed % 7) * 55f;
        float bpQ = 2.4f;
        ComputeBandpassCoefficients(bpCenter, bpQ, out float b0, out float b1, out float b2, out float a1, out float a2);
        float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;

        // 1-pole lowpass state for subtle wooden room tail
        float roomLp = 0f;
        float bodyPhase = 0f;
        float woodModePhase = 0f;
        float dt = 1f / SampleRate;

        float startFreq = isDeal ? 225f : 190f;
        float endFreq = isDeal ? 92f : 58f;
        float bodyDecay = isDeal ? 0.015f : 0.023f; // ~55ms audible exponential decay

        for (int i = 0; i < samples; i++)
        {
            float t = i * dt;

            // Layer 1: Sharp 3ms bandpass-filtered transient click (resin-on-wood impact)
            float white = (float)rng.NextDouble() * 2f - 1f;
            float clickEnv = t < 0.006f ? Mathf.Exp(-t / 0.0013f) : 0f;
            float bpIn = white * clickEnv;
            float bpOut = b0 * bpIn + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = bpIn;
            y2 = y1; y1 = bpOut;

            float resinSnap = bpOut * (isDeal ? 1.55f : 2.15f)
                            + Mathf.Sin(2f * Mathf.PI * (3650f + (seed % 5) * 90f) * t) * Mathf.Exp(-t / 0.0018f) * 0.22f;

            // Layer 2: 55ms exponentially decaying low-mid mahogany body thump (190Hz -> 58Hz)
            float sweep = Mathf.Clamp01(t / (isDeal ? 0.032f : 0.055f));
            float freq = Mathf.Lerp(startFreq, endFreq, sweep * (2f - sweep));
            bodyPhase += 2f * Mathf.PI * freq * dt;
            float bodyThump = Mathf.Sin(bodyPhase) * Mathf.Exp(-t / bodyDecay) * (isDeal ? 0.42f : 0.68f);

            // Secondary warm wooden cavity harmonic (~325Hz -> 185Hz)
            float modeFreq = Mathf.Lerp(325f, 185f, sweep);
            woodModePhase += 2f * Mathf.PI * modeFreq * dt;
            float woodBody = Mathf.Sin(woodModePhase) * Mathf.Exp(-t / (bodyDecay * 0.65f)) * 0.24f;

            // Layer 3: Subtle room decay (filtered early reflection tail)
            float roomEnv = t > 0.004f ? Mathf.Exp(-(t - 0.004f) / 0.042f) * 0.09f : 0f;
            roomLp = roomLp * 0.88f + white * roomEnv * 0.12f;

            float mixed = resinSnap + bodyThump + woodBody + roomLp;
            data[i] = Mathf.Clamp(mixed, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(isDeal ? "ResinDealHit" : "ResinTileHit", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Synthesizes a dramatic mahogany-table SLAM:
    /// - Crisp double-clack resin transient (impact + 13ms flat-slap bounce)
    /// - Deep sub-bass table resonance (110Hz -> 38Hz)
    /// - 55ms wooden body thump (190Hz -> 58Hz) + mahogany room decay
    /// </summary>
    private static AudioClip SynthesizeHeavyResinSlam(int seed, bool heavy)
    {
        float duration = heavy ? 0.30f : 0.23f;
        int samples = (int)(SampleRate * duration);
        float[] data = new float[samples];
        System.Random rng = new System.Random(seed);

        ComputeBandpassCoefficients(2550f, 2.2f, out float b0, out float b1, out float b2, out float a1, out float a2);
        float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;

        float subPhase = 0f;
        float bodyPhase = 0f;
        float cavityPhase = 0f;
        float roomLp = 0f;
        float dt = 1f / SampleRate;
        float secondClackTime = heavy ? 0.014f : 0.012f;

        for (int i = 0; i < samples; i++)
        {
            float t = i * dt;
            float white = (float)rng.NextDouble() * 2f - 1f;

            // Crisp double-clack transient (edge strike + flat domino face slap)
            float env1 = t < 0.007f ? Mathf.Exp(-t / 0.0014f) : 0f;
            float t2 = t - secondClackTime;
            float env2 = (t2 >= 0f && t2 < 0.006f) ? Mathf.Exp(-t2 / 0.0013f) * 0.78f : 0f;
            float bpIn = white * (env1 + env2);
            float bpOut = b0 * bpIn + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = bpIn;
            y2 = y1; y1 = bpOut;

            float doubleClack = bpOut * 2.4f;

            // Deep sub-bass table resonance (110Hz -> 38Hz)
            float subSweep = Mathf.Clamp01(t / (heavy ? 0.14f : 0.10f));
            float subFreq = Mathf.Lerp(110f, 38f, subSweep * (2f - subSweep));
            subPhase += 2f * Mathf.PI * subFreq * dt;
            float subResonance = Mathf.Sin(subPhase) * Mathf.Exp(-t / (heavy ? 0.085f : 0.062f)) * (heavy ? 0.85f : 0.68f);

            // Low-mid mahogany body thump (190Hz -> 58Hz)
            float bodySweep = Mathf.Clamp01(t / 0.055f);
            float bodyFreq = Mathf.Lerp(190f, 58f, bodySweep);
            bodyPhase += 2f * Mathf.PI * bodyFreq * dt;
            float bodyThump = Mathf.Sin(bodyPhase) * Mathf.Exp(-t / 0.028f) * 0.55f;

            // Hollow wood slab resonance (~245Hz)
            cavityPhase += 2f * Mathf.PI * 245f * dt;
            float woodSlab = Mathf.Sin(cavityPhase) * Mathf.Exp(-t / 0.036f) * 0.24f;

            // Room decay tail
            float roomEnv = t > 0.008f ? Mathf.Exp(-(t - 0.008f) / 0.075f) * 0.14f : 0f;
            roomLp = roomLp * 0.90f + white * roomEnv * 0.10f;

            float mixed = (doubleClack + subResonance + bodyThump + woodSlab + roomLp) * (heavy ? 1.12f : 1.0f);
            // Soft tape saturation for warm, punchy mahogany impact without harsh digital clipping
            data[i] = (float)System.Math.Tanh(mixed * 1.15f);
        }

        AudioClip clip = AudioClip.Create(heavy ? "HeavyResinSlam" : "ResinSlam", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static void ComputeBandpassCoefficients(float centerHz, float q, out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float omega = 2f * Mathf.PI * centerHz / SampleRate;
        float sinW = Mathf.Sin(omega);
        float cosW = Mathf.Cos(omega);
        float alpha = sinW / (2f * q);

        float a0 = 1f + alpha;
        b0 = alpha / a0;
        b1 = 0f;
        b2 = -alpha / a0;
        a1 = (-2f * cosW) / a0;
        a2 = (1f - alpha) / a0;
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
        float[] notes = { 392.00f, 349.23f, 293.66f, 220.00f };

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
