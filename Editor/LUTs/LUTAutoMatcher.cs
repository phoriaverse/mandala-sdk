#if UNITY_EDITOR
using UnityEngine;
using PHORIA.Studios.Mandala;

namespace PHORIA.Studios.Mandala.Editor
{
    public static class LUTAutoMatcher
    {
        // Configuración de umbrales para separar sombras/medios/luces
        private const float LOW_THRESHOLD = 0.35f; 
        private const float HIGH_THRESHOLD = 0.65f;

        public static void ExtractFromTexture(Texture2D source, LUTPreset targetLUT)
        {
            if (source == null || targetLUT == null) return;

            // 1. Necesitamos leer los píxeles. 
            // NOTA: Si es en Runtime, usa AsyncGPUReadback como vimos antes. 
            // En Editor, asegúrate de que la textura tenga "Read/Write Enabled".
            Color[] pixels = GetReadablePixels(source); 
            
            // Acumuladores
            Vector3 accShadow = Vector3.zero; float countShadow = 0;
            Vector3 accMid = Vector3.zero;    float countMid = 0;
            Vector3 accHigh = Vector3.zero;   float countHigh = 0;

            float totalLum = 0;
            float totalSat = 0;

            // 2. Analizar píxeles (Para optimizar, podríamos saltarnos píxeles, ej: i+=10)
            int step = Mathf.Max(1, pixels.Length / 4096); // Analizamos máximo ~4000 muestras para velocidad
            
            for (int i = 0; i < pixels.Length; i += step)
            {
                Color c = pixels[i];
                
                // Convertir a HSV para análisis
                Color.RGBToHSV(c, out float h, out float s, out float v);
                
                totalLum += v;
                totalSat += s;

                // Clasificar en cubos (Shadow/Mid/High) para los Tints
                // Usamos colores lineales para acumular
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

            // 3. Calcular promedios
            int sampleCount = pixels.Length / step;
            float avgLum = totalLum / sampleCount;
            float avgSat = totalSat / sampleCount;

            // 4. Asignar valores al LUTobject
            
            // -- Tints --
            // Normalizamos el color: queremos el "matiz", no la oscuridad.
            // Si el promedio de sombras es (0.1, 0.05, 0.05), el tinte debería ser Rojo (1.0, 0.5, 0.5)
            targetLUT.shadowTint = NormalizeTint(accShadow, countShadow);
            targetLUT.midTint = NormalizeTint(accMid, countMid);
            targetLUT.highlightTint = NormalizeTint(accHigh, countHigh);

            // -- Saturation --
            // Si la imagen es muy saturada, subimos la saturación del LUT para igualar
            // Base 1.0 + un ajuste basado en la saturación de la imagen
            targetLUT.saturation = Mathf.Lerp(0.5f, 1.5f, avgSat); 

            // -- Exposure --
            // Si la imagen es oscura (avgLum < 0.5), bajamos exposición.
            targetLUT.exposure = (avgLum - 0.5f) * 0.05f; 

            // -- Contrast --
            // (Estimación simple: varianza). Aquí usamos un valor seguro por defecto
            // o podrías calcular la desviación estándar si quieres más precisión.
            targetLUT.contrast = 1.1f; 
            
            // Resetear valores que es mejor no tocar automáticamente
            targetLUT.shadowEnd = LOW_THRESHOLD;
            targetLUT.highlightStart = HIGH_THRESHOLD;
            targetLUT.gamma = 1.0f;
            targetLUT.pivot = 0.5f;
            targetLUT.strength = 1.0f;
        }

        // Normaliza el color promedio para que el canal más brillante sea 1.0
        // Esto evita que el tinte oscurezca la imagen innecesariamente.
        private static Color NormalizeTint(Vector3 acc, float count)
        {
            if (count == 0) return Color.white;
            
            Vector3 avg = acc / count;
            float maxChannel = Mathf.Max(avg.x, Mathf.Max(avg.y, avg.z));
            
            if (maxChannel <= 0.01f) return Color.white; // Evitar división por cero en negros puros

            return new Color(avg.x / maxChannel, avg.y / maxChannel, avg.z / maxChannel, 1f);
        }

        private static Color[] GetReadablePixels(Texture2D src)
        {
            // Truco para leer texturas que no son Read/Write en Editor
            // (Crea una copia temporal usando RenderTexture)
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