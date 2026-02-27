using UnityEngine;

public class AudioTextureMask : MonoBehaviour
{
    [Header("Analyzer")]
    [Tooltip("Optional. If null, will try to find one in the scene on Enable.")]
    public AudioAnalyzer analyzer;

    [Tooltip("If true, auto-subscribe/unsubscribe to analyzer.AudioAnalyzed.")]
    public bool subscribeToAnalyzerEvent = true;

    [Header("Output")]
    [Tooltip("Optional: set the output as a global shader texture each frame.")]
    public bool setAsGlobalTexture = true;

    [Tooltip("Global shader texture name.")]
    public string globalTextureName = "_FeedbackTex";

    [Header("Feedback Update Shader")]
    [Tooltip("Shader that updates the feedback buffer (prev -> next).")]
    public Shader feedbackUpdateShader;

    [Tooltip("Feedback resolution (square). 256-1024 typical.")]
    [Range(64, 2048)] public int size = 512;

    [Header("Audio Inputs (fed by analyzer event)")]
    [Range(0, 1)] public float low;
    [Range(0, 1)] public float mid;
    [Range(0, 1)] public float high;
    [Range(0, 1)] public float volume;

    [Header("Feedback Tuning")]
    [Range(0.0f, 0.999f)] public float decay = 0.95f;
    [Range(0.0f, 0.2f)] public float noiseAmount = 0.04f;
    [Range(0.0f, 10.0f)] public float noiseScale = 1.0f;
    [Range(0.0f, 0.5f)] public float driftStrength = 0.09f;
    [Range(0.0f, 1.0f)] public float injectStrength = 0.2f;

    [Header("Performance")]
    [Tooltip("If true, updates in Update(). If false, call Step() manually.")]
    public bool autoUpdate = true;

    [Tooltip("Update rate limit (0 = every frame). Useful on mobile.")]
    [Range(0, 120)] public int maxFps = 30;

    public RenderTexture Output => _curr;

    Material _mat;
    RenderTexture _a, _b;
    RenderTexture _curr;
    bool _inited;
    float _accum;

    static readonly int ID_Decay = Shader.PropertyToID("_Decay");
    static readonly int ID_NoiseA = Shader.PropertyToID("_NoiseAmount");
    static readonly int ID_NoiseS = Shader.PropertyToID("_NoiseScale");
    static readonly int ID_Drift = Shader.PropertyToID("_DriftStrength");
    static readonly int ID_Inject = Shader.PropertyToID("_InjectStrength");
    static readonly int ID_Bands = Shader.PropertyToID("_AudioBands");
    static readonly int ID_TimeSec = Shader.PropertyToID("_TimeSec");

    void OnEnable()
    {
        EnsureResources();
        Clear(Color.black);

        if (subscribeToAnalyzerEvent)
            Subscribe();
    }

    void OnDisable()
    {
        if (subscribeToAnalyzerEvent)
            Unsubscribe();

        ReleaseResources();
    }

    void Subscribe()
    {
        if (analyzer == null)
            analyzer = FindFirstObjectByType<AudioAnalyzer>();

        if (analyzer != null)
            analyzer.AudioAnalyzed += OnAnalyzerAudioAnalyzed;
    }

    void Unsubscribe()
    {
        if (analyzer != null)
            analyzer.AudioAnalyzed -= OnAnalyzerAudioAnalyzed;
    }

    // Analyzer invokes: (volume, low, mid, high)
    void OnAnalyzerAudioAnalyzed(float vol, float lowIn, float midIn, float highIn)
    {
        volume = Mathf.Clamp01(vol);
        low = Mathf.Clamp01(lowIn);
        mid = Mathf.Clamp01(midIn);
        high = Mathf.Clamp01(highIn);
    }

    void Update()
    {
        if (!autoUpdate) return;

        if (maxFps <= 0)
        {
            Step(Time.unscaledTime);
            return;
        }

        _accum += Time.unscaledDeltaTime;
        float step = 1.0f / maxFps;
        if (_accum >= step)
        {
            _accum -= step;
            Step(Time.unscaledTime);
        }
    }

    public void Step(float timeSec)
    {
        EnsureResources();
        if (_mat == null || _a == null || _b == null) return;

        if (!_inited)
            Clear(Color.black);

        var src = _curr;
        var dst = (_curr == _a) ? _b : _a;

        _mat.SetFloat(ID_Decay, decay);
        _mat.SetFloat(ID_NoiseA, noiseAmount);
        _mat.SetFloat(ID_NoiseS, noiseScale);
        _mat.SetFloat(ID_Drift, driftStrength);
        _mat.SetFloat(ID_Inject, injectStrength);
        _mat.SetVector(ID_Bands, new Vector4(low, mid, high, volume)); // NOTE order for shader
        _mat.SetFloat(ID_TimeSec, timeSec);

        Graphics.Blit(src, dst, _mat, 0);
        _curr = dst;

        if (setAsGlobalTexture && !string.IsNullOrEmpty(globalTextureName))
            Shader.SetGlobalTexture(globalTextureName, _curr);
    }

    public void Clear(Color c)
    {
        EnsureResources();
        if (_a == null || _b == null) return;

        var prev = RenderTexture.active;
        RenderTexture.active = _a; GL.Clear(true, true, c);
        RenderTexture.active = _b; GL.Clear(true, true, c);
        RenderTexture.active = prev;

        _curr = _a;
        _inited = true;

        if (setAsGlobalTexture && !string.IsNullOrEmpty(globalTextureName))
            Shader.SetGlobalTexture(globalTextureName, _curr);
    }

    void EnsureResources()
    {
        if (feedbackUpdateShader == null) return;

        if (_mat == null)
            _mat = new Material(feedbackUpdateShader) { name = "AudioFeedbackBuffer_Mat" };

        size = Mathf.Clamp(size, 64, 4096);

        if (_a == null || !_a.IsCreated() || _a.width != size)
        {
            ReleaseRTs();
            _a = NewRT("AudioFeedback_A");
            _b = NewRT("AudioFeedback_B");
            _curr = _a;
            _inited = false;
        }
    }

    RenderTexture NewRT(string name)
    {
        var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        rt.name = name;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.Create();
        return rt;
    }

    void ReleaseResources()
    {
        if (_mat != null) { Destroy(_mat); _mat = null; }
        ReleaseRTs();
        _curr = null;
        _inited = false;
    }

    void ReleaseRTs()
    {
        if (_a != null) { _a.Release(); Destroy(_a); _a = null; }
        if (_b != null) { _b.Release(); Destroy(_b); _b = null; }
    }
}
