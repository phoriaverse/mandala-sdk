using UnityEngine;
using UnityEngine.Playables;

namespace PHORIA.Studios.Mandala.Timeline
{
    public class LUTMixerBehaviour : PlayableBehaviour
    {
        public float FadeDuration { get; set; } = 0.25f;
        
        private LUTTimelineBinding binding;
        private bool wasPlaying;
        
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            binding = playerData as LUTTimelineBinding;
            if (binding == null)
                return;
            
            // Capture original state on first frame
            binding.CaptureOriginalState();
            
            int inputCount = playable.GetInputCount();
            
            LUTBehaviour behaviourA = null;
            LUTBehaviour behaviourB = null;
            float weightA = 0f;
            float weightB = 0f;
            
            // Collect active clips and their weights
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                if (inputWeight <= 0f)
                    continue;
                
                var inputPlayable = (ScriptPlayable<LUTBehaviour>)playable.GetInput(i);
                var behaviour = inputPlayable.GetBehaviour();
                
                if (behaviour.lutClip?.Lut == null)
                    continue;
                
                // Ensure Passthrough LUT is created (only in Play mode)
                if (Application.isPlaying && behaviour.passthroughColorLut == null)
                {
                    // Both texture and preset-generated textures use the same format (flipVertical=true)
                    // so OVR should flip them the same way
                    behaviour.passthroughColorLut = new OVRPassthroughColorLut(behaviour.lutClip.Lut, true);
                }
                
                if (behaviourA == null)
                {
                    behaviourA = behaviour;
                    weightA = inputWeight;
                }
                else if (behaviourB == null)
                {
                    behaviourB = behaviour;
                    weightB = inputWeight;
                }
            }
            
            // Apply LUTs based on active clips
            if (behaviourA != null && behaviourB != null)
            {
                // Crossfade between two LUTs
                float blendFactor = weightB / (weightA + weightB);
                
                ApplyPassthroughCrossfade(behaviourA, behaviourB, blendFactor);
                ApplyVolumeCrossfade(behaviourA, behaviourB, blendFactor);
                
                wasPlaying = true;
            }
            else if (behaviourA != null)
            {
                // Single LUT with fade in/out based on weight (contribution)
                ApplyPassthroughSingle(behaviourA, weightA);
                ApplyVolumeSingle(behaviourA, weightA);
                
                wasPlaying = true;
            }
            else if (wasPlaying)
            {
                // No active LUTs, disable color maps
                DisableAllLuts();
                wasPlaying = false;
            }
        }
        
        private void ApplyPassthroughCrossfade(LUTBehaviour a, LUTBehaviour b, float blend)
        {
            if (!Application.isPlaying)
                return;
            
            if (binding.PassthroughLayer == null)
                return;
            
            if (a.passthroughColorLut == null || b.passthroughColorLut == null)
                return;
            
            binding.PassthroughLayer.SetColorLut(a.passthroughColorLut, b.passthroughColorLut, blend);
        }
        
        private void ApplyPassthroughSingle(LUTBehaviour a, float weight)
        {
            if (!Application.isPlaying)
                return;
            
            if (binding.PassthroughLayer == null)
                return;
            
            if (a.passthroughColorLut == null)
                return;
            
            // Weight acts as contribution - 0 = no effect, 1 = full LUT
            binding.PassthroughLayer.SetColorLut(a.passthroughColorLut, weight);
        }
        
        private void ApplyVolumeCrossfade(LUTBehaviour a, LUTBehaviour b, float blend)
        {
            var colorLookup = binding.ColorLookup;
            if (colorLookup == null)
                return;
            
            var lutA = a.lutClip.GetVolumeLut();
            var lutB = b.lutClip.GetVolumeLut();
            var blendMaterial = binding.LutBlendMaterial;
            
            // Crossfade requires blend material
            if (lutA == null || lutB == null || blendMaterial == null)
            {
                // Fallback: switch at midpoint
                colorLookup.texture.value = blend < 0.5f ? lutA : lutB;
                colorLookup.contribution.value = 1f;
                return;
            }
            
            // Use dimensions from the first LUT
            var blendedTexture = binding.GetOrCreateBlendedLutTexture(lutA.width, lutA.height);
            
            // Blend between two LUTs
            blendMaterial.SetTexture("_MainTex", lutA);
            blendMaterial.SetTexture("_LUT", lutA);
            blendMaterial.SetTexture("_LUT2", lutB);
            blendMaterial.SetFloat("_Contribution", blend);
            
            Graphics.Blit(null, blendedTexture, blendMaterial);
            colorLookup.texture.value = blendedTexture;
            colorLookup.contribution.value = 1f;
        }
        
        private void ApplyVolumeSingle(LUTBehaviour a, float weight)
        {
            var colorLookup = binding.ColorLookup;
            if (colorLookup == null)
                return;
            
            var lutA = a.lutClip.GetVolumeLut();
            if (lutA == null)
                return;
            
            // Simply set the LUT and use contribution for fade in/out
            colorLookup.texture.value = lutA;
            colorLookup.contribution.value = weight;
        }
        
        private void DisableAllLuts()
        {
            if (Application.isPlaying && binding.PassthroughLayer != null)
            {
                binding.PassthroughLayer.DisableColorMap();
            }
            
            binding.RestoreOriginalState();
        }
        
        public override void OnPlayableDestroy(Playable playable)
        {
            if (binding != null && wasPlaying)
            {
                DisableAllLuts();
            }
        }
    }
}
