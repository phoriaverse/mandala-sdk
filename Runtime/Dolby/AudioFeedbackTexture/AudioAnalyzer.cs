using System;
using UnityEngine;
using UnityEngine.Events;


public class AudioAnalyzer : MonoBehaviour
{
    [Serializable]
    public class AudioFloat3Event : UnityEvent<float, float, float,float> { }

    [Header("Audio Output Event")]
    [Tooltip("Invoked every frame after analysis. Args: (volume,low, mid, high).")]
    public AudioFloat3Event OnAudioAnalyzed;

    /// <summary>
    /// Optional C# event for code-driven subscriptions.
    /// Args: (volume, mid, high)
    /// </summary>
    public event Action<float, float, float, float> AudioAnalyzed;

    [Header("Amplitude Gains")]
    public float lowGain = 70f;
    public float midGain = 60f;
    public float highGain = 100f;
    public float volumeGain = 10f;

    [Header("Smoothing")]
    [Tooltip("Higher = faster response. 0 disables smoothing (raw).")]
    public float smoothfactor = 2f;
    

    [Header("Band Configuration")]
    [Tooltip("Spectrum indices for low band (inclusive start, exclusive end).")]
    public Vector2Int lowBand = new Vector2Int(0, 5);

    [Tooltip("Spectrum indices for mid band (inclusive start, exclusive end).")]
    public Vector2Int midBand = new Vector2Int(5, 50);

    [Tooltip("Spectrum indices for high band (inclusive start, exclusive end).")]
    public Vector2Int highBand = new Vector2Int(50, 512);

    [Header("Band Boost")]

    [Tooltip("Extra boost applied after smoothing/normalization.")]
    public float lowBoost = 2.5f;

    [Tooltip("Extra boost applied after smoothing/normalization.")]
    public float midBoost = 2.5f;

    [Tooltip("Extra boost applied after smoothing/normalization.")]
    public float highBoost = 3.0f;

    private AudioSource audioSource;
    private readonly float[] spectrum = new float[1024];

    private float smoothedLow;
    private float smoothedMid;
    private float smoothedHigh;
    private float smoothedVolume;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogError("AudioAnalyzer requires an AudioSource on the same GameObject.");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        if (audioSource == null) return;

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        float low = GetAverageAmplitude(lowBand.x, lowBand.y);
        float mid = GetAverageAmplitude(midBand.x, midBand.y);
        float high = GetAverageAmplitude(highBand.x, highBand.y);
        float volume = GetVolumeAmplitude();

        // Normalize (adjust multipliers to taste)
        float normalizedLow = Mathf.Clamp01(low * lowGain);
        float normalizedMid = Mathf.Clamp01(mid * midGain);
        float normalizedHigh = Mathf.Clamp01(high * highGain);
        float normalizedVolume = Mathf.Clamp01(volume * volumeGain);

        // Smooth using Lerp (exponential smoothing)
        if (smoothfactor <= 0f)
        {
            smoothedLow = normalizedLow;
            smoothedMid = normalizedMid;
            smoothedHigh = normalizedHigh;
            smoothedVolume = normalizedVolume;
        }
        else
        {
            float t = Time.deltaTime * smoothfactor;
            smoothedLow = Mathf.Lerp(smoothedLow, normalizedLow, t);
            smoothedMid = Mathf.Lerp(smoothedMid, normalizedMid, t);
            smoothedHigh = Mathf.Lerp(smoothedHigh, normalizedHigh, t);
            smoothedVolume = Mathf.Lerp(smoothedVolume, normalizedVolume, t);
        }

        float boostedLow = Mathf.Clamp01(smoothedLow * lowBoost);
        float boostedMid = Mathf.Clamp01(smoothedMid * midBoost);
        float boostedHigh = Mathf.Clamp01(smoothedHigh * highBoost);

        // Output (volume, mid, high)
        OnAudioAnalyzed?.Invoke(smoothedVolume,boostedLow,  boostedMid, boostedHigh);
        AudioAnalyzed?.Invoke(smoothedVolume,boostedLow, boostedMid, boostedHigh);
    }

    float GetVolumeAmplitude()
    {
        // waveform data
        float[] samples = new float[1024];
        audioSource.GetOutputData(samples, 0);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        return Mathf.Sqrt(sum / samples.Length);
    }

    float GetAverageAmplitude(int start, int end)
    {
        start = Mathf.Clamp(start, 0, spectrum.Length);
        end = Mathf.Clamp(end, 0, spectrum.Length);
        if (end <= start) return 0f;

        float sum = 0f;
        for (int i = start; i < end; i++)
            sum += spectrum[i];

        return sum / (end - start);
    }
}