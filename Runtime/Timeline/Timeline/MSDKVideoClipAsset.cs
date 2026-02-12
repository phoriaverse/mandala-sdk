using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using PHORIA.Mandala.SDK.Timeline;

namespace PHORIA.Mandala.SDK.Timeline
{
	[Serializable]
	public sealed class VideoClipAsset : PlayableAsset, ITimelineClipAsset, ITimelineVideoClipAsset
	{
		[Tooltip("Video URL used by the timeline video backend.")]
		[SerializeField] private string playback_url;
		[Tooltip("Optional metadata for clip duration in seconds.")]
		[SerializeField] private float duration;
		[Tooltip("Optional metadata for source frame rate.")]
		[SerializeField] private float fps;
		[Tooltip("Master-video time (in seconds) where this scene starts.")]
		[SerializeField] private double startOffsetSeconds;
		[SerializeField] private RenderTexture previewTargetTexture;

		public MSDKVideoClipData VideoClipData => new MSDKVideoClipData
		{
			playback_url = playback_url,
			duration = duration,
			fps = fps,
			startOffsetSeconds = startOffsetSeconds
		};
		public RenderTexture PreviewTargetTexture => previewTargetTexture;

		public ClipCaps clipCaps => ClipCaps.ClipIn;

		public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
			=> Playable.Create(graph); 
	}
}
