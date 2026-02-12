using System;
using UnityEngine;
using com.tiledmedia.player;

namespace PHORIA.Mandala.SDK.Timeline
{
	
	[DisallowMultipleComponent]
	public sealed class TiledTimelinePlayer : MonoBehaviour, ITimelineVideoPlayer, IDisplayObjectEventListener
	{
		
		private string _rigName = "TiledTimelineRig";
		private TiledmediaPlayer _tiledmediaPlayer;
		private ClearVRDisplayObjectControllerBase _unmanagedDisplayObject;
		
		private bool _isSeeking;
		private bool _listenerAdded;
		private bool _displayObjectRegistered;
		private byte[] _licenseFile;

		private ContentItem _activeContentItem;

		private byte[] LicenseFile => _licenseFile ??= (Resources.Load("license") as TextAsset)?.bytes;

		public bool IsPlaying => _tiledmediaPlayer != null && _tiledmediaPlayer.GetIsInPlayingState() && !_isSeeking;

		public bool IsSeeking => _isSeeking;

		private void Reset()
		{
		}

		private void Awake()
		{
			if (_tiledmediaPlayer == null)
				_tiledmediaPlayer = FindAnyObjectByType<TiledmediaPlayer>();

			EnsureRig();
			EnsurePlayerConfigured();
		}

		public bool TryGetTimeSeconds(out double timeSeconds)
		{
			timeSeconds = 0;
			if (_tiledmediaPlayer == null) return false;

			TimingReport report;
			try
			{
				report = _tiledmediaPlayer.GetTimingReport(TimingType.ContentTime);
			}
			catch
			{
				return false;
			}

			if (report == null || !report.GetIsSuccess())
				return false;

			var ms = report.currentPositionInMilliseconds;
			if (ms < 0) return false;

			timeSeconds = ms / 1000.0;
			return true;
		}

		public bool TryGetNormalizedProgress(out float normalized)
		{
			normalized = 0f;
			if (_tiledmediaPlayer == null) return false;
			if (_isSeeking) return false;

			try
			{
				normalized = GetNormalizedProgressInternal();
				return true;
			}
			catch
			{
				return false;
			}
		}

		public bool Play(MSDKVideoClipData clipData)
		{
			if (!Application.isPlaying) return false;
			if (_tiledmediaPlayer == null) return false;

			try
			{
				PlayFromClipData(clipData);
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{nameof(TiledTimelinePlayer)}] Failed to play: {ex.Message}");
				return false;
			}
		}

		public bool PreviewFrame(double timeSeconds)
		{
			// Preview is crashy in tiled for now
			return false;
		}

		public bool Seek(double timeSeconds)
		{
			if (_tiledmediaPlayer == null) return false;

			long targetMs = (long)(Math.Max(0, timeSeconds) * 1000);
			try
			{
				_isSeeking = true;
				var seekCfg = SeekConfig.ContentTime(targetMs);
				_tiledmediaPlayer.Seek(
					seekCfg,
					new PlayerRequestHandler(
						onSuccess: () => { _isSeeking = false; },
						onInterrupted: () =>
						{
							_isSeeking = false;
							Debug.LogError($"[{nameof(TiledTimelinePlayer)}] Seek request was interrupted.");
						},
						onFailure: failure =>
						{
							_isSeeking = false;
							Debug.LogError($"[{nameof(TiledTimelinePlayer)}] Seek request failed: {failure.code} {failure.message}");
						}
					)
				);

				return true;
			}
			catch (Exception e)
			{
				_isSeeking = false;
				Debug.LogError($"[{nameof(TiledTimelinePlayer)}] Failed to seek: {e.Message}");
				return false;
			}
		}

		private void OnDestroy()
		{
			if (_unmanagedDisplayObject != null && _listenerAdded)
			{
				_unmanagedDisplayObject.RemoveEventListener(this); // unregister to avoid leaks
				_listenerAdded = false;
			}
		}
		

		public void FirstFrameRendered(ClearVRDisplayObjectControllerBase displayObject)
		{
			// Only now it's valid to grab the textures
			// (note: may be null with TextureBlitMode.OVROverlayZeroCopy)
			var textures = displayObject.GetTextures();
			//displayObject.EnableOrDisableMeshRenderer(false);
			if (textures == null || textures.Length == 0)
			{
				Debug.Log("[TiledTimelinePlayer] FirstFrameRendered, but no accessible textures (e.g. OVROverlayZeroCopy).");
				return;
			}
			
			var pixelFormat =
				textures.Length == 1 ? PHORIA.Mandala.SDK.Timeline.PixelFormat.RGBA
				: textures.Length == 2 ? PHORIA.Mandala.SDK.Timeline.PixelFormat.NV12
				: textures.Length == 3 ? PHORIA.Mandala.SDK.Timeline.PixelFormat.YUV420P
				: PHORIA.Mandala.SDK.Timeline.PixelFormat.RGBA; 

			
			// TODO - HAX - we don't currently react to colorspace. We need tiled to expose the info.
			MSDK.Video.SetColorSpace(ColorSpace.BT709);
			MSDK.Video.SetPixelFormat(pixelFormat);
			MSDK.Video.SetTextures(textures);
			

		}
		

		public int IntervalForTimeUpdate() => 250;

		public void OnTimeUpdate(TimedEvent timedEvent, ClearVRDisplayObjectControllerBase displayObject)
		{
		}

		public void OnRenderModeChanged(RenderModeEvent renderModeEvent, ClearVRDisplayObjectControllerBase displayObject)
		{
		}

		public void OnTimedMetadata(TimedMetadataEvent metaDataEvent, ClearVRDisplayObjectControllerBase displayObject)
		{
		}

		public void OnVideoQualityChanged(VideoQualityChangedEvent videoQualityChangedEvent, ClearVRDisplayObjectControllerBase displayObject)
		{
		}

		public void ProjectionChanged(ClearVRDisplayObjectControllerBase displayObject)
		{
		}

		private void EnsurePlayerConfigured()
		{
			if (_tiledmediaPlayer == null) return;
			if (_tiledmediaPlayer.playerConfig != null) return;

			try
			{
				var cfg = _tiledmediaPlayer.GetDefaultPlayerConfig();
				// TODO: migrate to cfg.license once the project adopts the new API.
				cfg.licenseFileBytes = LicenseFile;
				
				var cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
				if (cam != null)
					cfg.trackingTransform = cam.transform;
				
				cfg.loopContent = false;
				cfg.vodResumeMode = VODResumeModes.MaintainState;
				_tiledmediaPlayer.Configure(cfg);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{nameof(TiledTimelinePlayer)}] Failed to configure TiledmediaPlayer: {ex.Message}");
			}
		}

		private void EnsureRig()
		{
			if (_tiledmediaPlayer != null && _unmanagedDisplayObject != null)
				return;
			
			Transform rig = transform.Find(_rigName);
			GameObject rigGo;
			if (rig == null)
			{
				rigGo = new GameObject(_rigName);
				rigGo.transform.SetParent(transform, false);
			}
			else rigGo = rig.gameObject;

			// Add/Get TiledmediaPlayer
			_tiledmediaPlayer = rigGo.GetComponent<TiledmediaPlayer>();
			if (_tiledmediaPlayer == null)
				_tiledmediaPlayer = rigGo.AddComponent<TiledmediaPlayer>();

			// Add/Get display object
			_unmanagedDisplayObject = rigGo.GetComponent<ClearVRDisplayObjectControllerBase>();
			if (_unmanagedDisplayObject == null)
			{
				_unmanagedDisplayObject = rigGo.AddComponent<ClearVRDisplayObjectControllerUnmanagedMesh>();
			}
		}

		private void RegisterDisplayObjectOnce(ContentItem contentItem)
		{
			if (_tiledmediaPlayer == null || _unmanagedDisplayObject == null || _displayObjectRegistered)
				return;

			_unmanagedDisplayObject.SetDesiredVideoSelection(contentItem);
			_tiledmediaPlayer.AddDisplayObject(_unmanagedDisplayObject); // register DO on the player
			if (!_listenerAdded)
			{
				_unmanagedDisplayObject.AddEventListener(this); // <-- listen for FirstFrameRendered
				_listenerAdded = true;
			}

			_displayObjectRegistered = true;
		}

		private void ApplyDesiredStartState()
		{
			if (_tiledmediaPlayer == null) return;

			var desired = DesiredStateConfig.StartFrom(new StartConfig(timingType: TimingType.ContentTime));
			_tiledmediaPlayer.ApplyDesiredState(
				desired,
				new PlayerRequestHandler(
					onSuccess: () => { },
					onInterrupted: () => Debug.LogError($"[{nameof(TiledTimelinePlayer)}] ApplyDesiredState interrupted."),
					onFailure: failure => Debug.LogError($"[{nameof(TiledTimelinePlayer)}] ApplyDesiredState failed: {failure.code} {failure.message}")
				)
			);
		}

		private void PlayFromClipData(MSDKVideoClipData clipData)
		{
			if (string.IsNullOrEmpty(clipData.playback_url))
			{
				Debug.LogError($"[{nameof(TiledTimelinePlayer)}] PlayFromClipData called with null/empty playback URL.");
				return;
			}

			EnsurePlayerConfigured();
			
			var resolvedUrl = ResolveVideoUrl.ResolveOrNull(clipData.playback_url);
			if (string.IsNullOrEmpty(resolvedUrl))
			{
				Debug.LogError($"[{nameof(TiledTimelinePlayer)}] PlayFromClipData called with invalid playback URL.");
				return;
			}

			var videoItem = new VideoItem
			{
				title = clipData.playback_url,
				url = resolvedUrl,
				projection = Projection.StereoscopicERP180SBS,
				position = VideoItem.Position.Omnidirectional,
				fisheyePreset = FisheyePreset.DomeMaster,
				drmConfig = null
			};

			// Projection defaults remain as configured above.

			var contentConfig = new ContentItemConfig(videoItem.url)
			{
				overrideProjection = videoItem.projection,
				fishEyeSettings = new FishEyeSettings(videoItem.fisheyePreset),
				drmConfig = videoItem.drmConfig
			};

			_tiledmediaPlayer.AddContent(
				contentConfig,
				new AddContentCompletionHandler(
					onSuccess: contentItem =>
					{
						_activeContentItem = contentItem;
						RegisterDisplayObjectOnce(_activeContentItem);
						ApplyDesiredStartState();
					},
					onInterrupted: () => Debug.LogError($"[{nameof(TiledTimelinePlayer)}] AddContent interrupted."),
					onFailure: failure => Debug.LogError($"[{nameof(TiledTimelinePlayer)}] AddContent failed: {failure.code} {failure.message}")
				)
			);
		}


		private float GetNormalizedProgressInternal()
		{
			if (_tiledmediaPlayer == null) return 0f;

			var report = _tiledmediaPlayer.GetTimingReport(TimingType.ContentTime);
			if (report == null || !report.GetIsSuccess()) return 0f;

			var range = report.seekRangeInMilliseconds;
			long lower = range.Lower;
			long upper = range.Upper;

			if (upper <= lower)
			{
				long dur = report.contentDurationInMilliseconds;
				lower = 0;
				upper = Math.Max(0, dur);
			}

			if (upper <= lower) return 0f;

			long pos = report.currentPositionInMilliseconds;
			return Mathf.InverseLerp(lower, upper, pos);
		}
	}
}
