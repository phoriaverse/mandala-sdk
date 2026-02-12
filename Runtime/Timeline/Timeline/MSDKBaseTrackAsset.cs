using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;


namespace PHORIA.Mandala.SDK.Timeline
{
	[TrackColor(0.7f, 0.2f, 0.9f)]
	[TrackClipType(typeof(VideoClipAsset))]
	[TrackBindingType(typeof(VideoTimelineLink))]
	public sealed class MSDKBaseTrackAsset : TrackAsset
	{
		public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
		{
			var playable = ScriptPlayable<MSDKBaseMixerBehaviour>.Create(graph, inputCount);
			var mixer = playable.GetBehaviour();

			VideoClipAsset clipAsset = null;
			foreach (var clip in GetClips())
			{
				clipAsset = clip.asset as VideoClipAsset;
				break;
			}

			mixer.SetVideoClip(clipAsset);
			return playable;
		}
	}
}
