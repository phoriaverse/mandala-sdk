using System;
using UnityEngine;
using UnityEngine.Video;

namespace PHORIA.Mandala.SDK.Timeline
{

	/// <summary>
    /// Timeline video backend built on Unity's native <see cref="UnityEngine.Video.VideoPlayer"/>.
    ///
    /// Primary intention:
    /// - Editor scrubbing: decode frames while the Timeline is scrubbed (not playing).
    /// - Runtime usage is possible but not the goal here.
    ///
    /// Notes:
    /// - Projection/immersive metadata is ignored for now; we only provide the file->texture pipeline.
    /// - Uses URL-based playback (VideoPlayer.url).
    /// </summary>

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Video.VideoPlayer))]
    public sealed class UnityTimelinePlayer : MonoBehaviour, ITimelineVideoPlayer
    {
        [Header("Bindings")]
        [SerializeField]
        private UnityEngine.Video.VideoPlayer _videoPlayer;

        [Header("Settings")]
        [Tooltip("If enabled, this component will set VideoPlayer.audioOutputMode to None.")]
        [SerializeField]
        private bool disableAudio = true;

        [Tooltip("If enabled, we enable sendFrameReadyEvents so editor scrubbing helpers can wait for a decoded frame.")]
        [SerializeField]
        private bool sendFrameReadyEvents = true;

        private string _currentUrl;
        private bool _prepared;


        /// <summary>
        /// Fired when the VideoPlayer reports it has prepared the current URL.
        /// </summary>
        public event Action Prepared;

        /// <summary>
        /// Fired when a frame is ready (if frame-ready events are enabled).
        /// </summary>
        public event Action<long> FrameReady;

        public RenderTexture TargetTexture
        {
	        get => _videoPlayer != null ? _videoPlayer.targetTexture : null;
	        set
	        {
		        if (_videoPlayer != null)
		        {
			        _videoPlayer.targetTexture = value;
			        PushTextureToStore();
		        }
	        }
        }

        public bool IsPlaying => _videoPlayer != null && _videoPlayer.isPlaying;

        // Unity VideoPlayer doesn't have an explicit seeking flag; approximate based on Prepared state.
        public bool IsSeeking => _videoPlayer == null || !_prepared;

        private void Reset()
        {
            _videoPlayer = GetComponent<UnityEngine.Video.VideoPlayer>();
            ApplyDefaultVideoPlayerSettings();
        }

        private void Awake()
        {
            if (_videoPlayer == null)
                _videoPlayer = GetComponent<UnityEngine.Video.VideoPlayer>();

            ApplyDefaultVideoPlayerSettings();
            
            PushTextureToStore();

            if (_videoPlayer != null)
            {
                _videoPlayer.prepareCompleted -= OnPrepareCompleted;
                _videoPlayer.prepareCompleted += OnPrepareCompleted;

                _videoPlayer.errorReceived -= OnErrorReceived;
                _videoPlayer.errorReceived += OnErrorReceived;

                _videoPlayer.frameReady -= OnFrameReady;
                if (sendFrameReadyEvents)
                    _videoPlayer.frameReady += OnFrameReady;
            }

            //MSDK.SetVideoTextures(new Texture[] { _videoPlayer.targetTexture });
        }

        private void OnDestroy()
        {
            if (_videoPlayer == null) return;
            _videoPlayer.prepareCompleted -= OnPrepareCompleted;
            _videoPlayer.errorReceived -= OnErrorReceived;
            _videoPlayer.frameReady -= OnFrameReady;
            MSDK.Video.SetTextures(Array.Empty<Texture>());
        }

        private void ApplyDefaultVideoPlayerSettings()
        {
            if (_videoPlayer == null) return;

            _videoPlayer.playOnAwake = false;
            _videoPlayer.source = UnityEngine.Video.VideoSource.Url;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.aspectRatio = VideoAspectRatio.Stretch;

            // We want texture output.
            _videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;

            if (disableAudio)
            {
                _videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
            }

            // Needed for editor scrubbing helpers waiting for a decoded frame.
            _videoPlayer.sendFrameReadyEvents = sendFrameReadyEvents;

            // Don’t loop unless caller wants it.
            _videoPlayer.isLooping = false;
        }

#if UNITY_EDITOR
        private void Update()
        {
	        if (!Application.isPlaying && _videoPlayer != null && _videoPlayer.isPlaying)
		        Debug.LogWarning("[UnityTimelinePlayer] VideoPlayer is PLAYING in edit mode. Something called Play().", this);
        }
#endif
	    private void OnPrepareCompleted(UnityEngine.Video.VideoPlayer source)
	    {
		    _prepared = true;
		    PushTextureToStore();
		    MSDK.Video.SetColorSpace(ColorSpace.BT709);
		    MSDK.Video.SetPixelFormat(PixelFormat.RGBA);
		    Prepared?.Invoke();
	    }

        private void OnFrameReady(UnityEngine.Video.VideoPlayer source, long frameIdx)
        {
            // Re-publish in case another graph lifecycle path cleared global refs.
            PushTextureToStore();
            FrameReady?.Invoke(frameIdx);
        }

        private void OnErrorReceived(UnityEngine.Video.VideoPlayer source, string message)
        {
            _prepared = false;
            Debug.LogError($"[{nameof(UnityTimelinePlayer)}] VideoPlayer error: {message}");
        }

        public bool Play(MSDKVideoClipData clipData)
        {
            if (_videoPlayer == null)
            {
                Debug.LogError($"[{nameof(UnityTimelinePlayer)}] _videoPlayer is null.");
                return false;
            }

            if (string.IsNullOrEmpty(clipData.playback_url))
            {
                Debug.LogError($"[{nameof(UnityTimelinePlayer)}] playback_url is null or empty.");
                return false;
            }

            var url = ResolveVideoUrl.ResolveOrNull(clipData.playback_url);

            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError($"[{nameof(UnityTimelinePlayer)}] playback_url is null or empty.");
                return false;
            }

            // If URL changed, re-prepare.
            if (!string.Equals(_currentUrl, url, StringComparison.Ordinal))
            {
                _currentUrl = url;
                _prepared = false;

                _videoPlayer.url = _currentUrl;
                _videoPlayer.Prepare();

            }

            // If already prepared, start playback.
            if (_prepared)
            {
                _videoPlayer.Play();
            }
            else
            {
                // Start playing once prepared.
                _videoPlayer.prepareCompleted -= OnAutoPlayOnPrepared;
                _videoPlayer.prepareCompleted += OnAutoPlayOnPrepared;
            }


            return true;
        }
        
        public bool SetSource(MSDKVideoClipData clipData)
        {
	        if (_videoPlayer == null) return false;
	        if (string.IsNullOrEmpty(clipData.playback_url)) return false;

	        var url = ResolveVideoUrl.ResolveOrNull(clipData.playback_url);

            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError($"[{nameof(UnityTimelinePlayer)}] playback_url is null or empty.");
                return false;
            }

	        if (!string.Equals(_currentUrl, url, StringComparison.Ordinal))
	        {
		        _currentUrl = url;
		        _prepared = false;

		        _videoPlayer.Stop();              // important: stop any current playback
		        _videoPlayer.url = _currentUrl;
	        }

	        return true;
        }
        
        public void Pause()
        {
	        if (_videoPlayer == null) return;
	        if (_videoPlayer.isPlaying) _videoPlayer.Pause();
        }

        private void OnAutoPlayOnPrepared(UnityEngine.Video.VideoPlayer source)
        {
            _videoPlayer.prepareCompleted -= OnAutoPlayOnPrepared;
            _prepared = true;
            
            // Only auto-play in runtime play mode
            if (!Application.isPlaying)
	            return;

            try
            {
                _videoPlayer.Play();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{nameof(UnityTimelinePlayer)}] Failed to auto-play after prepare: {ex.Message}");
            }
        }

        public bool TryGetTimeSeconds(out double timeSeconds)
        {
            timeSeconds = 0;
            if (_videoPlayer == null) return false;
            if (!_prepared) return false;

            timeSeconds = _videoPlayer.time;
            return timeSeconds >= 0;
        }

        public bool TryGetNormalizedProgress(out float normalized)
        {
            normalized = 0f;
            if (_videoPlayer == null) return false;
            if (!_prepared) return false;

            var len = (double)_videoPlayer.length;
            if (len <= 0.0001) return false;

            normalized = Mathf.Clamp01((float)(_videoPlayer.time / len));
            return true;
        }

        /// <summary>
        /// Seek to time (seconds). Intended for editor scrubbing helpers.
        /// Returns false if the player isn't prepared yet.
        /// </summary>
        public bool TrySetTimeSeconds(double timeSeconds)
        {
            if (_videoPlayer == null) return false;
            if (!_prepared) return false;

            if (timeSeconds < 0) timeSeconds = 0;
            _videoPlayer.frame = (long)(_videoPlayer.frameRate * timeSeconds);
            _videoPlayer.Pause();
            return true;
        }


        public bool PreviewFrame(double timeSeconds)
        {
	        if (_videoPlayer == null) return false;

	        // If not prepared yet, request prepare and return false this frame.
	        if (!_prepared)
	        {
		        if (!string.IsNullOrEmpty(_videoPlayer.url) && !_videoPlayer.isPrepared)
			        _videoPlayer.Prepare();
		        return false;
	        }

	        
	        _videoPlayer.time = Math.Max(0, timeSeconds);
	        _videoPlayer.Pause();
	        return true;
        }
        
        private void PushTextureToStore()
        {
	        if (_videoPlayer == null)
	        {
		        MSDK.Video.SetTextures(Array.Empty<Texture>());
		        return;
	        }

	        var tex = _videoPlayer.targetTexture;
	        if (tex == null)
	        {
		        // TODO - we'll want configurable sizes here if unity video player sticks around
		        _videoPlayer.targetTexture = new RenderTexture(8192, 4096, 0, RenderTextureFormat.ARGB32);
		        tex = _videoPlayer.targetTexture;
	        }
	        
	        MSDK.Video.SetTextures(new Texture[] { tex });
	      
        }
        
        

        /// <summary>
        /// Request prepare for the currently set URL. Useful for editor workflows where we don't want to auto-play.
        /// </summary>
        public bool Prepare()
        {
            if (_videoPlayer == null) return false;
            if (string.IsNullOrEmpty(_videoPlayer.url)) return false;

            _prepared = false;
            _videoPlayer.Prepare();
            return true;
        }
        

        public bool Seek(double time)
        {
            if (_videoPlayer == null) return false;
            if (!_prepared) return false;

            try
            {
                _videoPlayer.time = time;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(UnityTimelinePlayer)}] Failed to seek: {e.Message}");
                return false;
            }
        }

        public bool IsPrepared => _prepared;

        public string CurrentUrl => _currentUrl;
    }
}
