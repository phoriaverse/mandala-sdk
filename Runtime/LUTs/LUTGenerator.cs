using UnityEngine;

namespace PHORIA.Studios.Mandala
{
    public static class LUTGenerator
    {
        public const int LutSize = 16;
        public const int LutWidth = 256;  // 16 * 16
        public const int LutHeight = 16;

        /// <summary>
        /// Generates a 256x16 LUT strip texture from a LUTobject preset.
        /// Output matches the format expected by OVRPassthroughColorLut (horizontal strip layout).
        /// </summary>
        public static Texture2D GenerateLutTexture(LUTPreset preset, bool flipVertical = true, bool linear = false)
        {
            if (preset == null)
                return null;

            var pixels = new Color32[LutWidth * LutHeight];
            
            // Pre-compute preset parameters
            float shadowEnd = Mathf.Clamp01(preset.shadowEnd);
            float highlightStart = Mathf.Clamp01(preset.highlightStart);
            if (highlightStart < shadowEnd) { float tmp = highlightStart; highlightStart = shadowEnd; shadowEnd = tmp; }

            float span = Mathf.Max(0.0001f, highlightStart - shadowEnd);
            float feather = Mathf.Clamp(span * 0.25f, 0.02f, 0.12f);

            float strength = Mathf.Clamp01(preset.strength);
            float contrast = Mathf.Max(0.0001f, preset.contrast);
            float pivot = Mathf.Clamp01(preset.pivot);
            float saturation = Mathf.Max(0f, preset.saturation);
            float exposure = preset.exposure;

            float gamma = Mathf.Max(0.0001f, preset.gamma);
            float invGamma = 1f / gamma;

            Color st = preset.shadowTint; st.a = 1f;
            Color mt = preset.midTint; mt.a = 1f;
            Color ht = preset.highlightTint; ht.a = 1f;

            Vector3 shadowTint = new Vector3(st.r, st.g, st.b);
            Vector3 midTint = new Vector3(mt.r, mt.g, mt.b);
            Vector3 highlightTint = new Vector3(ht.r, ht.g, ht.b);

            // Horizontal strip layout: 256x16
            // X axis: r (0-15) repeating for each blue slice (0-15), so x = r + b*16
            // Y axis: g (0-15)
            // When flipVertical=true: y=0 is g=15, y=15 is g=0 (black at top-left)
            // When flipVertical=false: y=0 is g=0, y=15 is g=15 (black at bottom-left)
            
            for (int y = 0; y < LutHeight; y++)
            {
                // Determine green value based on flip
                int g = flipVertical ? (LutSize - 1 - y) : y;
                float gf = g / 15f;
                
                for (int b = 0; b < LutSize; b++)
                {
                    float bf = b / 15f;
                    
                    for (int r = 0; r < LutSize; r++)
                    {
                        float rf = r / 15f;
                        
                        // Apply color grading
                        Vector3 c = new Vector3(rf, gf, bf);
                        float lum = c.x * 0.2126f + c.y * 0.7152f + c.z * 0.0722f;

                        float wShadow = 1f - Smoothstep(shadowEnd - feather, shadowEnd + feather, lum);
                        float wHighlight = Smoothstep(highlightStart - feather, highlightStart + feather, lum);
                        float wMid = Mathf.Clamp01(1f - wShadow - wHighlight);

                        Vector3 tint = shadowTint * wShadow + midTint * wMid + highlightTint * wHighlight;

                        Vector3 graded = new Vector3(c.x * tint.x, c.y * tint.y, c.z * tint.z);
                        graded += new Vector3(exposure, exposure, exposure);

                        Vector3 pv = new Vector3(pivot, pivot, pivot);
                        graded = (graded - pv) * contrast + pv;

                        float lum2 = graded.x * 0.2126f + graded.y * 0.7152f + graded.z * 0.0722f;
                        Vector3 grey = new Vector3(lum2, lum2, lum2);
                        graded = Vector3.Lerp(grey, graded, saturation);

                        graded.x = Mathf.Pow(Mathf.Max(0f, graded.x), invGamma);
                        graded.y = Mathf.Pow(Mathf.Max(0f, graded.y), invGamma);
                        graded.z = Mathf.Pow(Mathf.Max(0f, graded.z), invGamma);

                        Vector3 outC = Vector3.Lerp(c, graded, strength);

                        // Calculate pixel position: x = r + b*16, y = y
                        int x = r + b * LutSize;
                        int pixelIndex = y * LutWidth + x;
                        
                        pixels[pixelIndex] = new Color32(
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(outC.x) * 255f),
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(outC.y) * 255f),
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(outC.z) * 255f),
                            255
                        );
                    }
                }
            }

            // Create texture - 'linear' parameter: false = sRGB, true = linear
            var texture = new Texture2D(LutWidth, LutHeight, TextureFormat.RGBA32, false, linear);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = $"LUT_{preset.name}";
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            return texture;
        }

        private static float Smoothstep(float a, float b, float x)
        {
            float t = Mathf.InverseLerp(a, b, x);
            return t * t * (3f - 2f * t);
        }
    }
}
