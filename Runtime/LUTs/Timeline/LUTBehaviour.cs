using System;
using UnityEngine;
using UnityEngine.Playables;

namespace PHORIA.Studios.Mandala.Timeline
{
    [Serializable]
    public class LUTBehaviour : PlayableBehaviour
    {
        [HideInInspector]
        public LUTClip lutClip;
        
        [HideInInspector]
        public OVRPassthroughColorLut passthroughColorLut;
        
        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (!Application.isPlaying)
                return;
            
            if (lutClip?.Lut != null && passthroughColorLut == null)
            {
                // Both texture and preset-generated textures use the same format
                passthroughColorLut = new OVRPassthroughColorLut(lutClip.Lut, true);
            }
        }
        
        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // Cleanup handled by mixer
        }
        
        public override void OnPlayableDestroy(Playable playable)
        {
            lutClip?.Cleanup();
        }
    }
}
