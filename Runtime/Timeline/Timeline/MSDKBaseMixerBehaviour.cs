#if UNITY_EDITOR
using UnityEditor;
#endif
using System;

using UnityEngine;
using UnityEngine.Playables;


namespace PHORIA.Mandala.SDK.Timeline
{
    /// <summary>
    /// Mandala SDK Timeline mixer.
    ///
    /// This runs every time the Timeline graph evaluates (play mode + editor scrubbing).
    /// </summary>
    public sealed class MSDKBaseMixerBehaviour : PlayableBehaviour
    {
        private VideoClipAsset _videoClip;

        private readonly MSDKVideoDriver _videoDriver = new();


        public void SetVideoClip(VideoClipAsset clip) => _videoClip = clip;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var link = ResolveLinkFromPlayerData(playerData);
            var drivesVideo = _videoDriver.Drive(_videoClip, playable, info, link, out var effectiveTimeSeconds);
            if (!drivesVideo)
                return;
            

            var textures = MSDK.Video.Textures;
            if (textures == null || textures.Length == 0 || textures[0] == null)
	            return;
            Shader.SetGlobalFloat("_MSDK_VideoTime", (float)effectiveTimeSeconds);
            

        }

        private static IVideoTimelineLink ResolveLinkFromPlayerData(object playerData)
        {
            if (playerData is IVideoTimelineLink link)
                return link;

            if (playerData is Component component)
                return component as IVideoTimelineLink ?? component.GetComponent<IVideoTimelineLink>();

            if (playerData is GameObject gameObject)
                return gameObject.GetComponent<IVideoTimelineLink>();

            return null;
        }

        public override void OnGraphStop(Playable playable)
        {
            if (!Application.isPlaying)
            {
                // Avoid carrying stale edit-mode preview textures into the next graph lifecycle.
                MSDK.Video.SetTextures(Array.Empty<Texture>());
#if UNITY_EDITOR
                _videoDriver.DestroyPreviewBackend();
#endif
            }

            _videoDriver.ResetRuntimeState();
        }
    }
}
