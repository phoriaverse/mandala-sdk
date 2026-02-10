using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace PHORIA.Studios.Mandala.Timeline
{
    [Serializable]
    public class LUTClip : PlayableAsset, ITimelineClipAsset
    {
        public enum LutSourceType
        {
            Texture,
            Preset
        }

        [Tooltip("Choose whether to use a texture or a LUTobject preset")]
        [SerializeField]
        private LutSourceType sourceType = LutSourceType.Texture;

        [Tooltip("LUT texture following Passthrough LUT settings (sRGB, not flipped)")]
        [SerializeField]
        private Texture2D lut;

        [Tooltip("LUT preset to generate texture from at runtime")]
        [SerializeField]
        private LUTPreset lutPreset;

        public LutSourceType SourceType => sourceType;
        public LUTPreset LutPreset => lutPreset;

        /// <summary>
        /// /// Gets the effective LUT texture (either assigned or generated from preset)
        /// /// </summary>
        public Texture2D Lut
        {
            get
            {
                if (sourceType == LutSourceType.Preset)
                {
                    return GetOrGeneratePassthroughPresetLut();
                }
                return lut;
            }
        }

        public ClipCaps clipCaps => ClipCaps.Blending;

        [NonSerialized]
        private Texture2D generatedVolumeLut;

        [NonSerialized]
        private Texture2D generatedPresetLut;

        private Texture2D GetOrGeneratePassthroughPresetLut()
        {
            if (lutPreset == null)
                return null;

            if (generatedPresetLut == null)
            {
                // For passthrough: flipVertical=true to match exported texture format
                // (exported with flipVerticalLut=true which is the default in PresetPreviewWindow)
                generatedPresetLut = LUTGenerator.GenerateLutTexture(lutPreset, flipVertical: true, linear: false);
            }

            return generatedPresetLut;
        }

        public Texture2D GetVolumeLut()
        {
            if (sourceType == LutSourceType.Preset)
            {
                return GetOrGenerateVolumePresetLut();
            }

            // Original texture-based logic
            if (lut == null)
                return null;

            if (generatedVolumeLut == null)
            {
                generatedVolumeLut = CreateVolumeLut(lut);
            }

            return generatedVolumeLut;
        }

        private Texture2D GetOrGenerateVolumePresetLut()
        {
            if (lutPreset == null)
                return null;

            if (generatedVolumeLut == null)
            {
                // Volume needs the texture flipped compared to passthrough input
                // Since passthrough input is flipVertical=true, and CreateVolumeLut flips it,
                // we need flipVertical=false for volume to end up correct after NOT going through CreateVolumeLut
                generatedVolumeLut = LUTGenerator.GenerateLutTexture(lutPreset, flipVertical: false, linear: true);
            }

            return generatedVolumeLut;
        }

        private static Texture2D CreateVolumeLut(Texture2D source)
        {
            int sourceWidth = source.width;
            int sourceHeight = source.height;

            // Create texture with linear color space (non-sRGB) for URP Color Lookup
            // Last parameter 'true' = linear (non-sRGB)
            var converted = new Texture2D(sourceWidth, sourceHeight, TextureFormat.RGBA32, false, true);
            converted.filterMode = FilterMode.Bilinear;
            converted.wrapMode = TextureWrapMode.Clamp;

            Color[] sourcePixels = source.GetPixels();
            Color[] flippedPixels = new Color[sourcePixels.Length];

            // Always flip vertically for URP
            for (int y = 0; y < sourceHeight; y++)
            {
                for (int x = 0; x < sourceWidth; x++)
                {
                    int sourceIndex = x + y * sourceWidth;
                    int destIndex = x + (sourceHeight - 1 - y) * sourceWidth;
                    flippedPixels[destIndex] = sourcePixels[sourceIndex];
                }
            }

            converted.SetPixels(flippedPixels);
            converted.Apply();

            return converted;
        }

        public void Cleanup()
        {
            if (generatedVolumeLut != null)
            {
                DestroyImmediate(generatedVolumeLut);
                generatedVolumeLut = null;
            }

            if (generatedPresetLut != null)
            {
                DestroyImmediate(generatedPresetLut);
                generatedPresetLut = null;
            }
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<LUTBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.lutClip = this;
            return playable;
        }
    }
}
