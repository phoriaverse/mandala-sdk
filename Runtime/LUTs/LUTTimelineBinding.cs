using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PHORIA.Studios.Mandala
{
    public class LUTTimelineBinding : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Will search for it if not assigned")]
        private OVRPassthroughLayer passthroughLayer;
        
        [SerializeField]
        private Volume postProcessVolume;
        
        private Material lutBlendMaterial;
        
        public OVRPassthroughLayer PassthroughLayer
        {
            get
            {
                if (passthroughLayer == null)
                {
                    passthroughLayer = FindAnyObjectByType<OVRPassthroughLayer>();
                }
                return passthroughLayer;
            }
        }
        public Volume PostProcessVolume => postProcessVolume;
        public Material LutBlendMaterial
        {
            get
            {
                if (lutBlendMaterial == null)
                {
                    Shader lutBlendShader = Shader.Find("Custom/LUTBlend");
                    if (lutBlendShader != null)
                    {
                        lutBlendMaterial = new Material(lutBlendShader);
                    }
                    else
                    {
                        Debug.LogError("LUTTimelineBinding: Could not find shader 'Custom/LUTBlend'");
                    }
                }
                return lutBlendMaterial;
            }
        }
        
        private ColorLookup colorLookup;
        private RenderTexture blendedLutTexture;
        private Texture originalVolumeLut;
        private float originalContribution;
        private bool originalCaptured;
        
        public ColorLookup ColorLookup
        {
            get
            {
                if (colorLookup == null && postProcessVolume != null)
                {
                    postProcessVolume.profile.TryGet(out colorLookup);
                }
                return colorLookup;
            }
        }
        
        public void CaptureOriginalState()
        {
            if (!originalCaptured && ColorLookup != null)
            {
                originalVolumeLut = ColorLookup.texture.value;
                originalContribution = ColorLookup.contribution.value;
                originalCaptured = true;
            }
        }
        
        public Texture OriginalVolumeLut => originalVolumeLut;
        public float OriginalContribution => originalContribution;
        
        public RenderTexture GetOrCreateBlendedLutTexture(int width, int height)
        {
            // Recreate if dimensions changed
            if (blendedLutTexture != null && 
                (blendedLutTexture.width != width || blendedLutTexture.height != height))
            {
                blendedLutTexture.Release();
                DestroyImmediate(blendedLutTexture);
                blendedLutTexture = null;
            }
            
            if (blendedLutTexture == null)
            {
                // Match the source LUT dimensions exactly
                // Use Linear color space (non-sRGB) via RenderTextureReadWrite.Linear
                blendedLutTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                blendedLutTexture.filterMode = FilterMode.Bilinear;
                blendedLutTexture.wrapMode = TextureWrapMode.Clamp;
                blendedLutTexture.Create();
            }
            return blendedLutTexture;
        }
        
        private void OnDestroy()
        {
            if (blendedLutTexture != null)
            {
                blendedLutTexture.Release();
                DestroyImmediate(blendedLutTexture);
            }

            if (lutBlendMaterial != null)
            {
                DestroyImmediate(lutBlendMaterial);
            }
        }
        
        public void RestoreOriginalState()
        {
            if (ColorLookup != null && originalCaptured)
            {
                ColorLookup.texture.value = originalVolumeLut;
                ColorLookup.contribution.value = originalContribution;
            }
        }
    }   
}
