Shader "Hidden/AudioFeedbackUpdate"
{
    Properties
    {
        _MainTex ("Prev", 2D) = "black" {}
        _Decay ("Decay", Float) = 0.95
        _NoiseAmount ("Noise Amount", Float) = 0.04
        _NoiseScale ("Noise Scale", Float) = 1.0
        _DriftStrength ("Drift", Float) = 0.09
        _InjectStrength ("Inject", Float) = 0.2
        _AudioBands ("AudioBands (Low Mid High Vol)", Vector) = (0,0,0,0)
        _TimeSec ("Time", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Decay, _NoiseAmount, _NoiseScale, _DriftStrength, _InjectStrength, _TimeSec;
            float4 _AudioBands;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 noise2(float2 p)
            {
                return float2(hash21(p), hash21(p + 17.13));
            }

            float3 palette(float t)
            {
                float3 a = float3(0.5, 0.5, 0.5);
                float3 b = float3(0.5, 0.5, 0.5);
                float3 c = float3(1.0, 1.0, 1.0);
                float3 d = float3(0.00, 0.10, 0.20);
                return a + b * cos(6.2831853 * (c * t + d));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // centered coords (-1..1) assuming square RT
                float2 st = uv * 2.0 - 1.0;
                float dist = length(st);

                float low  = _AudioBands.x;
                float mid  = _AudioBands.y;
                float high = _AudioBands.z;
                float vol  = _AudioBands.w;

                float lowMask  = saturate(1.0 - dist * 1.2);
                float midMask  = saturate(1.0 - abs(dist - 0.6) * 3.0);
                float highMask = saturate(dist * 1.2);

                float audioSignal = saturate(vol + low * lowMask + mid * midMask + high * highMask);
                audioSignal = pow(audioSignal, 4.0);

                float2 n = noise2(st * _NoiseScale + _TimeSec * 0.3) - 0.5;
                float2 offsetUV = uv + n * _NoiseAmount;
                offsetUV -= st * (_DriftStrength * 0.1);

                float4 prev = tex2D(_MainTex, offsetUV);
                prev.rgb *= _Decay;

                float t = saturate(1.0 - dist);
                float3 injectCol = palette(t) * (audioSignal * _InjectStrength);

                float3 outRGB = prev.rgb + injectCol;
                return float4(outRGB, audioSignal);
            }
            ENDCG
        }
    }
}
