#if UNITY_EDITOR
using UnityEngine;
using PHORIA.Studios.Mandala;

namespace PHORIA.Studios.Mandala.Editor
{
    public static class LUTAutoMatcher
    {
        // Threshold configuration to separate shadows/midtones/highlights
        private const float LOW_THRESHOLD = 0.35f; 
        private const float HIGH_THRESHOLD = 0.65f;

        public static void ExtractFromTexture(Texture2D source, LUTPreset targetLUT)
        {
            if (source == null || targetLUT == null) return;

            // 1. We need to read the pixels. 
            // NOTE: If it's in Runtime, use AsyncGPUReadback as we saw before. 
            // In Editor, make sure the texture has "Read/Write Enabled".
            Color[] pixels = GetReadablePixels(source); 
            
            // Accumulators
            Vector3 accShadow = Vector3.zero; float countShadow = 0;
            Vector3 accMid = Vector3.zero;    float countMid = 0;
            Vector3 accHigh = Vector3.zero;   float countHigh = 0;

            float totalLum = 0;
            float totalSat = 0;

            // 2. Analyze pixels (For optimization, we could skip pixels, e.g. i+=10)
            int step = Mathf.Max(1, pixels.Length / 4096); // We analyze up to ~4000 samples for speed
            
            for (int i = 0; i < pixels.Length; i += step)
            {
                Color c = pixels[i];
                
                // Convert to HSV for analysis
                Color.RGBToHSV(c, out float h, out float s, out float v);
                
                totalLum += v;
                totalSat += s;

                // Classify in cubes (Shadow/Mid/High) for the Tints
                // We use linear colors to accumulate
                Vector3 rgb = new Vector3(c.r, c.g, c.b);

                if (v < LOW_THRESHOLD)
                {
                    accShadow += rgb;
                    countShadow++;
                }
                else if (v > HIGH_THRESHOLD)
                {
                    accHigh += rgb;
                    countHigh++;
                }
                else
                {
                    accMid += rgb;
                    countMid++;
                }
            }

            // 3. Calculate averages
            int sampleCount = pixels.Length / step;
            float avgLum = totalLum / sampleCount;
            float avgSat = totalSat / sampleCount;

            // 4. Assign values to the LUTobject
            
            // -- Tints --
            // Normalize the color: we want the "hue", not the darkness.
            // If the average shadows are (0.1, 0.05, 0.05), the tint should be Red (1.0, 0.5, 0.5)
            targetLUT.shadowTint = NormalizeTint(accShadow, countShadow);
            targetLUT.midTint = NormalizeTint(accMid, countMid);
            targetLUT.highlightTint = NormalizeTint(accHigh, countHigh);

            // -- Saturation --
            // If the image is very saturated, we increase the LUT saturation to match
            // Base 1.0 + an adjustment based on the image saturation
            targetLUT.saturation = Mathf.Lerp(0.5f, 1.5f, avgSat); 

            // -- Exposure --
            // If the image is dark (avgLum < 0.5), we lower exposure.
            targetLUT.exposure = (avgLum - 0.5f) * 0.05f; 

            // -- Contrast --
            // (Simple estimation: variance). We use a safe default value
            // or you could calculate the standard deviation for more precision.
            targetLUT.contrast = 1.1f; 
            
            // Reset values that are better not to touch automatically
            targetLUT.shadowEnd = LOW_THRESHOLD;
            targetLUT.highlightStart = HIGH_THRESHOLD;
            targetLUT.gamma = 1.0f;
            targetLUT.pivot = 0.5f;
            targetLUT.strength = 1.0f;
        }

        // Normalizes the average color so that the brightest channel is 1.0
        // This prevents the tint from unnecessarily darkening the image.
        private static Color NormalizeTint(Vector3 acc, float count)
        {
            if (count == 0) return Color.white;
            
            Vector3 avg = acc / count;
            float maxChannel = Mathf.Max(avg.x, Mathf.Max(avg.y, avg.z));
            
            if (maxChannel <= 0.01f) return Color.white; // Avoid division by zero in pure blacks

            return new Color(avg.x / maxChannel, avg.y / maxChannel, avg.z / maxChannel, 1f);
        }

        private static Color[] GetReadablePixels(Texture2D src)
        {
            // Trick to read textures that are not Read/Write in Editor
            // (Creates a temporary copy using RenderTexture)
            RenderTexture tmp = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, tmp);
            
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = tmp;
            
            Texture2D myTexture2D = new Texture2D(src.width, src.height);
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();
            
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tmp);
            
            return myTexture2D.GetPixels();
        }
    }
}
#endif