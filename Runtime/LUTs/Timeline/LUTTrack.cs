using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace PHORIA.Studios.Mandala.Timeline
{
    [TrackColor(0.5f, 0.2f, 0.8f)]
    [TrackClipType(typeof(LUTClip))]
    [TrackBindingType(typeof(LUTTimelineBinding))]
    public class LUTTrack : TrackAsset
    {
        [Tooltip("Duration in seconds for fading in/out when transitioning from/to no LUT")]
        [SerializeField]
        private float fadeDuration = 0.25f;
        
        public float FadeDuration => fadeDuration;
        
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<LUTMixerBehaviour>.Create(graph, inputCount);
            var mixerBehaviour = mixer.GetBehaviour();
            mixerBehaviour.FadeDuration = fadeDuration;
            return mixer;
        }
    }
}
