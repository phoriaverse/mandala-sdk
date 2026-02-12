namespace PHORIA.Mandala.SDK.Timeline
{
	using System;
#if UNITY_EDITOR
	using System.Collections.Generic;
	using UnityEditor;
#endif
	using UnityEngine;
	using UnityEngine.Playables;
#if UNITY_EDITOR
	using UnityEngine.Timeline;
#endif

	public sealed class MSDKVideoDriver
	{
		private const double TimeEpsilon = 0.0001;

		private IVideoTimelineLink _cachedLink;
		private bool _runtimePlaybackIssued;
		private string _runtimeIssuedUrl;
		private double _runtimeIssuedOffsetSeconds = double.NaN;

#if UNITY_EDITOR
		private static bool s_previewPlaybackActive;
		private static readonly HashSet<MSDKVideoDriver> s_livePreviewDrivers = new();

		private GameObject _previewRoot;
		private UnityEngine.Video.VideoPlayer _previewVideoPlayer;
		private double _lastPreviewTime = double.NaN;
		private string _lastPreviewUrl;
		private RenderTexture _tempPreviewRT;
		private bool _previewPlaybackRunning;
#endif

		public bool Drive(VideoClipAsset clip, Playable playable, FrameData info, IVideoTimelineLink linkFromBinding, out double effectiveTimeSeconds)
		{
			effectiveTimeSeconds = 0d;
			if (clip == null) return false;

			var clipData = clip.VideoClipData;
			var url = ResolveVideoUrl.ResolveOrNull(clipData.playback_url);
			if (string.IsNullOrEmpty(url)) return false;

			effectiveTimeSeconds = clipData.startOffsetSeconds + playable.GetTime();

			if (Application.isPlaying)
			{
				DriveRuntime(clipData, url, linkFromBinding);
				return linkFromBinding != null;
			}

#if UNITY_EDITOR
			return DriveEditorPreview(playable, clip, url, effectiveTimeSeconds, linkFromBinding);
#else
			return false;
#endif
		}

		public void ResetRuntimeState()
		{
			_runtimePlaybackIssued = false;
			_runtimeIssuedUrl = null;
			_runtimeIssuedOffsetSeconds = double.NaN;
			_cachedLink = null;
		}

		private void DriveRuntime(MSDKVideoClipData clipData, string url, IVideoTimelineLink linkFromBinding)
		{
			// Runtime authority comes only from explicit timeline binding.
			if (linkFromBinding == null) return;

			if (!IsSameLinkInstance(_cachedLink, linkFromBinding))
			{
				_cachedLink = linkFromBinding;
				_runtimePlaybackIssued = false;
				_runtimeIssuedUrl = null;
				_runtimeIssuedOffsetSeconds = double.NaN;
			}

			if (_runtimePlaybackIssued &&
			    string.Equals(_runtimeIssuedUrl, url, StringComparison.Ordinal) &&
			    IsSameOffset(_runtimeIssuedOffsetSeconds, clipData.startOffsetSeconds))
			{
				return;
			}

			_runtimePlaybackIssued = _cachedLink.RequestPlayableClip(clipData);
			if (_runtimePlaybackIssued)
			{
				_runtimeIssuedUrl = url;
				_runtimeIssuedOffsetSeconds = clipData.startOffsetSeconds;
			}
		}

		private static bool IsSameOffset(double a, double b) => Math.Abs(a - b) < TimeEpsilon;

		private static bool IsSameLinkInstance(IVideoTimelineLink a, IVideoTimelineLink b)
		{
			if (ReferenceEquals(a, b))
				return true;

			var aObject = a as UnityEngine.Object;
			var bObject = b as UnityEngine.Object;
			if (aObject != null && bObject != null)
				return aObject == bObject;

			return false;
		}

#if UNITY_EDITOR
		public static void NotifyEditorTimelinePlayed(PlayableDirector director)
		{
			s_previewPlaybackActive = director != null;
			StartAllPreviewPlayback();
		}

		public static void NotifyEditorTimelinePaused(PlayableDirector director)
		{
			s_previewPlaybackActive = false;
			StopAllPreviewPlayback();
		}

		public static void NotifyEditorTimelineStopped(PlayableDirector director)
		{
			s_previewPlaybackActive = false;
			StopAllPreviewPlayback();
		}

		private static bool IsPreviewPlaybackActive()
		{
			return s_previewPlaybackActive;
		}

		private static void StopAllPreviewPlayback()
		{
			if (s_livePreviewDrivers.Count == 0)
				return;

			var liveDrivers = new List<MSDKVideoDriver>(s_livePreviewDrivers);
			foreach (var driver in liveDrivers)
				driver?.StopPreviewPlayback();
		}

		private static void StartAllPreviewPlayback()
		{
			if (s_livePreviewDrivers.Count == 0)
				return;

			var liveDrivers = new List<MSDKVideoDriver>(s_livePreviewDrivers);
			foreach (var driver in liveDrivers)
				driver?.StartPreviewPlayback();
		}

		private bool DriveEditorPreview(Playable playable, VideoClipAsset clip, string url, double effectiveTimeSeconds, IVideoTimelineLink linkFromBinding)
		{
			// In master preview mode, unbound nested tracks should not publish video frames.
			if (ShouldSkipUnboundEditorPreview(playable, linkFromBinding))
				return false;

			EnsurePreviewBackend(url, clip.PreviewTargetTexture);

			var director = playable.GetGraph().GetResolver() as PlayableDirector;
			if (IsPreviewPlaybackActive())
				return DriveEditorPreviewPlayback(director, clip.VideoClipData.startOffsetSeconds, effectiveTimeSeconds);

			StopPreviewPlayback();
			if (!double.IsNaN(_lastPreviewTime) && Math.Abs(effectiveTimeSeconds - _lastPreviewTime) < TimeEpsilon)
				return true;

			if (TryPreviewFrame(effectiveTimeSeconds))
				_lastPreviewTime = effectiveTimeSeconds;

			return true;
		}

		private bool DriveEditorPreviewPlayback(PlayableDirector director, double clipOffsetSeconds, double effectiveTimeSeconds)
		{
			if (_previewVideoPlayer == null)
				return false;

			if (!_previewVideoPlayer.isPrepared)
			{
				if (!string.IsNullOrEmpty(_previewVideoPlayer.url))
					_previewVideoPlayer.Prepare();
				return true;
			}

			if (!_previewPlaybackRunning)
			{
				_previewVideoPlayer.time = Math.Max(0d, effectiveTimeSeconds);
				_previewPlaybackRunning = true;
			}

			if (!_previewVideoPlayer.isPlaying)
				_previewVideoPlayer.Play();

			var videoTimeSeconds = Math.Max(0d, _previewVideoPlayer.time);
			var timelineTimeSeconds = Math.Max(0d, videoTimeSeconds - clipOffsetSeconds);

			if (director != null)
				director.time = timelineTimeSeconds;

			_lastPreviewTime = videoTimeSeconds;

			EditorApplication.QueuePlayerLoopUpdate();
			SceneView.RepaintAll();

			return true;
		}

		private static bool ShouldSkipUnboundEditorPreview(Playable playable, IVideoTimelineLink linkFromBinding)
		{
			if (linkFromBinding != null)
				return false;

			var resolver = playable.GetGraph().GetResolver();
			if (resolver is not PlayableDirector director)
				return false;

			return DirectorHasBoundVideoTrack(director);
		}

		private static bool DirectorHasBoundVideoTrack(PlayableDirector director)
		{
			if (director == null || director.playableAsset is not TimelineAsset timeline)
				return false;

			foreach (var track in timeline.GetOutputTracks())
			{
				if (track is not MSDKBaseTrackAsset)
					continue;

				if (director.GetGenericBinding(track) is IVideoTimelineLink)
					return true;
			}

			return false;
		}

		private void EnsurePreviewBackend(string url, RenderTexture target)
		{
			// Keep a lightweight local VideoPlayer for timeline scrubbing in edit mode.
			if (_previewRoot == null)
			{
				_previewRoot = new GameObject("MSDK_TimelinePreviewVideo")
				{
					hideFlags = HideFlags.HideAndDontSave
				};
				s_livePreviewDrivers.Add(this);

				_previewVideoPlayer = _previewRoot.AddComponent<UnityEngine.Video.VideoPlayer>();
				_previewVideoPlayer.playOnAwake = false;
				_previewVideoPlayer.source = UnityEngine.Video.VideoSource.Url;
				_previewVideoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
				_previewVideoPlayer.waitForFirstFrame = true;
				_previewVideoPlayer.isLooping = false;
				_previewVideoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
				_previewVideoPlayer.sendFrameReadyEvents = true;
			}

			// If clip didn’t provide an RT, create one (otherwise nothing can update).
			if (target == null)
			{
				if (_tempPreviewRT == null)
				{
					_tempPreviewRT = new RenderTexture(8192, 4096, 0, RenderTextureFormat.ARGB32)
					{
						name = "MSDK_PreviewRT",
						hideFlags = HideFlags.HideAndDontSave
					};
					_tempPreviewRT.Create();
				}
				target = _tempPreviewRT;
			}

			if (_previewVideoPlayer.targetTexture != target)
				_previewVideoPlayer.targetTexture = target;

			MSDK.Video.SetTextures(new Texture[] { target });

			if (!string.Equals(_lastPreviewUrl, url, StringComparison.Ordinal))
			{
				_previewVideoPlayer.Stop();
				_previewVideoPlayer.url = url;
				_previewVideoPlayer.Prepare();

				_lastPreviewUrl = url;
				_lastPreviewTime = double.NaN;
				_previewPlaybackRunning = false;
			}
		}

		private bool TryPreviewFrame(double timeSeconds)
		{
			if (_previewVideoPlayer == null) return false;

			if (!_previewVideoPlayer.isPrepared)
			{
				if (!string.IsNullOrEmpty(_previewVideoPlayer.url))
					_previewVideoPlayer.Prepare();
				return false;
			}

			_previewVideoPlayer.time = Math.Max(0, timeSeconds);

			// In edit mode, a short play/pause tick is the most reliable texture refresh.
			_previewVideoPlayer.Play();
			_previewVideoPlayer.Pause();

			EditorApplication.QueuePlayerLoopUpdate();
			SceneView.RepaintAll();

			return true;
		}

		private void StopPreviewPlayback()
		{
			if (_previewVideoPlayer != null && _previewVideoPlayer.isPlaying)
				_previewVideoPlayer.Pause();

			_previewPlaybackRunning = false;
		}

		private void StartPreviewPlayback()
		{
			_previewPlaybackRunning = false;

			if (_previewVideoPlayer == null)
				return;

			if (!_previewVideoPlayer.isPrepared)
			{
				if (!string.IsNullOrEmpty(_previewVideoPlayer.url))
					_previewVideoPlayer.Prepare();
				return;
			}

			if (!_previewVideoPlayer.isPlaying)
				_previewVideoPlayer.Play();
		}

		public void DestroyPreviewBackend()
		{
			StopPreviewPlayback();
			s_livePreviewDrivers.Remove(this);

			if (_previewRoot != null)
				UnityEngine.Object.DestroyImmediate(_previewRoot);

			_previewRoot = null;
			_previewVideoPlayer = null;
			_lastPreviewUrl = null;
			_lastPreviewTime = double.NaN;

			if (_tempPreviewRT != null)
			{
				_tempPreviewRT.Release();
				UnityEngine.Object.DestroyImmediate(_tempPreviewRT);
				_tempPreviewRT = null;
			}
		}
#endif
	}
}
