using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace PHORIA.Mandala.SDK.Timeline
{

	/// <summary>
	/// Links a video backend to a Unity Timeline, using the video time as the source of truth.
	///
	/// This component:
	/// - Holds references to a PlayableDirector and an <see cref="ITimelineVideoPlayer"/> implementation.
	/// - On each Update (play mode only), reads the current content time from the video backend and drives the Timeline to that time.
	///
	/// Notes:
	/// - Editor preview/scrubbing is handled elsewhere (e.g., Timeline clip preview backends).
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("Nocturne/Video Timeline Link")]
	public class VideoTimelineLink : MonoBehaviour, PHORIA.Mandala.SDK.Timeline.IVideoTimelineLink
	{
		private const double OffsetEpsilon = 0.0001;

		public Component AsComponent => this;

		[Header("Timeline")] [Tooltip("PlayableDirector that controls the Timeline to be synchronized to video time.")] [SerializeField]
		private PlayableDirector playableDirector;

		[SerializeField] private bool useUnityVideoPlayer = false;

		private MSDKVideoClipData _videoClipData;
		private bool _hasVideoClipData;

		private ITimelineVideoPlayer _videoPlayer;

		[Header("UI Binding")] [Tooltip("Optional slider to reflect playback progress (0..1). Not updated while paused or seeking.")] [SerializeField]
		private Slider playbackProgressSlider;

		private string _lastIssuedPlaybackUrl;
		private double _lastIssuedPlaybackOffsetSeconds = double.NaN;
		private double _activeClipOffsetSeconds;
		private bool _pendingInitialSeek;
		private double _pendingInitialSeekTimeSeconds;
		
		public bool IsReady => Application.isPlaying && _videoPlayer != null;
		
		private void Awake()
		{
			// At runtime we only want to fill missing refs, not overwrite inspector wiring.
			AutoWireIfMissing();
		}
		
		private void Reset()
		{
			// Auto-wire reasonable defaults when the component is first added.
			AutoWireIfMissing();
		}

		private void OnValidate()
		{
			// Keep PlayableDirector wired up when edited in the Inspector.
			if (playableDirector == null)
				playableDirector = GetComponent<PlayableDirector>();

			// Serialized references may change in the inspector.
			_videoPlayer = null;

		}

		private void AutoWireIfMissing()
		{
			if (playableDirector == null)
				playableDirector = GetComponent<PlayableDirector>();
			
			if (playableDirector)
			{
				playableDirector.timeUpdateMode = DirectorUpdateMode.Manual;
			}

			if (Application.isPlaying && _videoPlayer == null) 
			{
				if (useUnityVideoPlayer)
				{
					_videoPlayer = GetComponent<UnityTimelinePlayer>();
					if (_videoPlayer == null)
						_videoPlayer = gameObject.AddComponent<UnityTimelinePlayer>();
				}
				else
				{
					_videoPlayer = GetComponent<TiledTimelinePlayer>();
					if (_videoPlayer == null)
						_videoPlayer = gameObject.AddComponent<TiledTimelinePlayer>();
				}
			}
		}

		private void Update()
		{
			// This component is runtime-only.
			if (!Application.isPlaying)
				return;

			TryStartVideoPlayback();
			TryApplyPendingOffsetSeek();

			if (playableDirector == null || _videoPlayer == null)
				return;

			if (!_videoPlayer.TryGetTimeSeconds(out double videoTimeSeconds))
			{
				EvaluateTimelineWhileAwaitingVideoTime();
				return;
			}

			if (double.IsNaN(videoTimeSeconds) || double.IsInfinity(videoTimeSeconds) || videoTimeSeconds < 0)
				return;

			// Pause the Timeline when the video is not in Playing state.
			bool isVideoPlaying = _videoPlayer.IsPlaying;
			if (!isVideoPlaying)
			{
				UpdateProgressSlider(false);
				if (playableDirector.state == PlayState.Playing)
					playableDirector.Pause();
				return;
			}

			UpdateProgressSlider(true);

			// Ensure the Timeline is playing if the video is playing.
			if (playableDirector.state != PlayState.Playing)
				playableDirector.Play();

			// Child timelines can target a section of the master video using startOffsetSeconds.
			// Timeline local time should be videoTime minus that offset.
			var timelineTimeSeconds = Math.Max(0d, videoTimeSeconds - _activeClipOffsetSeconds);
			playableDirector.time = timelineTimeSeconds;
			playableDirector.Evaluate();
			
		}

		private void EvaluateTimelineWhileAwaitingVideoTime()
		{
			if (playableDirector == null || playableDirector.playableAsset == null)
				return;

			playableDirector.Evaluate();
		}

		private void UpdateProgressSlider(bool isVideoPlaying)
		{

			if (playbackProgressSlider == null || _videoPlayer == null)
				return;

			if (!isVideoPlaying || _videoPlayer.IsSeeking)
				return;

			if (_videoPlayer.TryGetNormalizedProgress(out float normalized))
				playbackProgressSlider.SetValueWithoutNotify(normalized);
		}

		private void TryStartVideoPlayback()
		{
			// Autoplay only makes sense in play mode.
			if (!Application.isPlaying)
				return;

			// Only auto-start once a clip has provided a valid asset.
			if (!_hasVideoClipData || string.IsNullOrEmpty(_videoClipData.playback_url))
				return;

			// Avoid re-issuing the same playback request every Update.
			if (string.Equals(_lastIssuedPlaybackUrl, _videoClipData.playback_url, StringComparison.Ordinal) &&
			    OffsetsMatch(_lastIssuedPlaybackOffsetSeconds, _videoClipData.startOffsetSeconds))
				return;

			if (IssuePlaybackRequest(_videoClipData))
			{
				_lastIssuedPlaybackUrl = _videoClipData.playback_url;
				_lastIssuedPlaybackOffsetSeconds = _videoClipData.startOffsetSeconds;
			}
		}

		public bool RequestPlayableClip(MSDKVideoClipData clipData)
		{
			if (string.IsNullOrEmpty(clipData.playback_url))
			{
				Debug.LogWarning($"[{nameof(VideoTimelineLink)}] Cannot start playback: playback_url is empty.");
				return false;
			}

			clipData.startOffsetSeconds = Math.Max(0d, clipData.startOffsetSeconds);

			if (_hasVideoClipData && !string.Equals(_videoClipData.playback_url, clipData.playback_url, StringComparison.Ordinal))
			{
				Debug.LogWarning(
					$"[{nameof(VideoTimelineLink)}] Requested URL changed from '{_videoClipData.playback_url}' to '{clipData.playback_url}'. " +
					"Master/child timeline setups are expected to use the same video URL.");
			}

			var isSameRequest =
				_hasVideoClipData &&
				string.Equals(_videoClipData.playback_url, clipData.playback_url, StringComparison.Ordinal) &&
				OffsetsMatch(_videoClipData.startOffsetSeconds, clipData.startOffsetSeconds);
			var alreadyIssuedSameRequest =
				string.Equals(_lastIssuedPlaybackUrl, clipData.playback_url, StringComparison.Ordinal) &&
				OffsetsMatch(_lastIssuedPlaybackOffsetSeconds, clipData.startOffsetSeconds);

			_videoClipData = clipData;
			_hasVideoClipData = true;
			_activeClipOffsetSeconds = Math.Max(0d, clipData.startOffsetSeconds);

			if (isSameRequest && alreadyIssuedSameRequest)
				return true;

			// Always issue immediately when requested (this is typically called from a playable clip).
			if (!IssuePlaybackRequest(clipData))
			{
				Debug.LogWarning($"[{nameof(VideoTimelineLink)}] RequestPlayableClip failed to issue playback for '{clipData.playback_url}'.");
				return false;
			}

			_lastIssuedPlaybackUrl = clipData.playback_url;
			_lastIssuedPlaybackOffsetSeconds = clipData.startOffsetSeconds;
			return true;
		}

		private bool IssuePlaybackRequest(MSDKVideoClipData clipData)
		{
			if (!Application.isPlaying)
			{
				Debug.LogError($"[{nameof(VideoTimelineLink)}] Cannot issue playback request when not in play mode.");
				return false;
			}


			if (string.IsNullOrEmpty(clipData.playback_url) || _videoPlayer == null)
			{
				if (_videoPlayer == null)
				{
					Debug.LogError($"[{nameof(VideoTimelineLink)}] Cannot issue playback request: missing ITimelineVideoPlayer implementation.");
				}

				Debug.LogError($"[{nameof(VideoTimelineLink)}] Cannot issue playback request: invalid parameters.");
				return false;
			}

			try
			{
				if (!_videoPlayer.Play(clipData))
					return false;

				var targetOffset = Math.Max(0d, clipData.startOffsetSeconds);
				_pendingInitialSeek = true;
				_pendingInitialSeekTimeSeconds = targetOffset;
				TryApplyPendingOffsetSeek();

				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{nameof(VideoTimelineLink)}] Failed to play URL '{clipData.playback_url}': {ex.Message}");
				return false;
			}
		}

		private void TryApplyPendingOffsetSeek()
		{
			if (!_pendingInitialSeek || _videoPlayer == null)
				return;

			if (_videoPlayer.Seek(_pendingInitialSeekTimeSeconds))
			{
				_pendingInitialSeek = false;
				_pendingInitialSeekTimeSeconds = 0d;
			}
		}

		private static bool OffsetsMatch(double a, double b) => Math.Abs(a - b) < OffsetEpsilon;
	}
}
