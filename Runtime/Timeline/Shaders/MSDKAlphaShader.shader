// TEMP SHADER FOR DEMO PURPOSES ONLY

Shader "MSDK/AlphaShader"
{
    Properties
    {
        [Header(General)]
        _Color("Tint", Color) = (1,1,1,1)
        [Toggle] _MSDK_UseCustomColorMatrix("Use Custom Color Matrix", Float) = 0
    	[Toggle] _MSDK_DebugRgbOpaque("RGB Only (No Alpha)", Float) = 0
        [Toggle] _MSDK_DebugAlpha("Debug Alpha", Float) = 0
        
        [Toggle] _MSDK_FlipV("Flip Vertical", Float) = 1
    	

        [Header(RGB)]
    	[Space(8)]
        [Enum(Mono,0, LeftRight,1, TopBottom,2, CustomUV,3)] _MSDK_RgbStereoMode("Stereo Mode", Float) = 0
        _MSDK_RgbUvScaleX("UV Scale X", Float) = 1
        _MSDK_RgbUvScaleY("UV Scale Y", Float) = 1
        _MSDK_RgbUvOffsetX("UV Offset X", Float) = 0
        _MSDK_RgbUvOffsetY("UV Offset Y", Float) = 0
    	[Space(8)]

        [Header(Alpha)]
    	[Space(8)]
        [Enum(Mono,0, LeftRight,1, TopBottom,2, CustomUV,3)] _MSDK_AlphaStereoMode("Stereo Mode", Float) = 0
        _MSDK_AlphaUvScaleX("UV Scale X", Float) = 1
        _MSDK_AlphaUvScaleY("UV Scale Y", Float) = 1
        _MSDK_AlphaUvOffsetX("UV Offset X", Float) = 0
        _MSDK_AlphaUvOffsetY("UV Offset Y", Float) = 0
    	[Space(8)]

        [HideInInspector] _SrcBlend ("__src", Float) = 5
        [HideInInspector] _DstBlend ("__dst", Float) = 10
        [HideInInspector] _ZWrite ("__zw", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Lighting Off
        Cull Off
        ZWrite [_ZWrite]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "GLSL"
            GLSLPROGRAM
                #include "UnityCG.glslinc"
                #define SHADERLAB_GLSL
                #include "GLSLSupport.glslinc"

                // ---- Minimal ClearVR utilities (inlined) -----------------
                #if defined (SHADERLAB_GLSL)
                    #define CONST_FLOAT float
                    #define FLOAT4 vec4
                    #define FLOAT3 vec3
                    #define FLOAT2 vec2
                    #define FLOAT4X4 mat4
                    #define FLOAT3X3 mat3
                    #define FIXED4 vec4
                    #define FIXED3 vec3
                    #define INLINE
                    #define ATAN atan
                    #if defined(USE_OES_FAST_PATH_ON)
                        #define SAMPLER2D samplerExternalOES
                    #else
                        #define SAMPLER2D sampler2D
                    #endif
                    #if __VERSION__ >= 300
                        #define TEX2D texture
                    #else
                        #define TEX2D texture2D
                    #endif
                #endif

                INLINE int GetStereoEyeIndex() {
                #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED)
                    return int(unity_StereoEyeIndex);
                #elif defined (STEREO_MULTIVIEW_ON)
                #   if defined(SHADER_API_VULKAN)
                        return int(unity_StereoEyeIndex);
                #   else
                        return int(gl_ViewID_OVR);
                #   endif
                #elif defined(UNITY_DECLARE_MULTIVIEW)
                    return int(UNITY_VIEWID);
                #else
                    return 0;
                #endif
                }

                INLINE FLOAT2 ApplyTextureTransformMatrix(FLOAT2 uv, FLOAT4X4 textureTransformMatrix) {
                    FLOAT4 newHomogeneousUV = textureTransformMatrix * FLOAT4(uv.x, uv.y, 0.0, 1.0);
                    return newHomogeneousUV.xy / newHomogeneousUV.w;
                }

                INLINE FLOAT2 ApplyScaleOffset(FLOAT2 uv, FLOAT4 scaleOffset) {
                    return uv * scaleOffset.xy + scaleOffset.zw;
                }

                INLINE FLOAT3 ConvertYpCbCrToRGB(FLOAT3 yuv, FLOAT4X4 colorSpaceTextureTransformMatrix)
                {
                    return clamp(FLOAT3X3(colorSpaceTextureTransformMatrix) * (yuv + colorSpaceTextureTransformMatrix[3].xyz), 0.0, 1.0);
                }

                INLINE float RemapLimitedRangeToFull(float y)
                {
                    const float kBlack = 16.0 / 255.0;
                    const float kWhite = 235.0 / 255.0;
                    return clamp((y - kBlack) * (255.0 / (235.0 - 16.0)), 0.0, 1.0);
                }

                INLINE FLOAT2 ApplyStereoMode(FLOAT2 uv0, FLOAT2 uv1, float eyeIndex, int mode, FLOAT4 scaleOffset, float flipV)
                {
                    if (flipV > 0.5) {
                        uv0.y = 1.0 - uv0.y;
                        uv1.y = 1.0 - uv1.y;
                    }
                    FLOAT2 uv = (mode == 3 && eyeIndex > 0.5) ? uv1 : uv0;
                    if (mode == 1) {
                        uv.x = uv.x * 0.5 + eyeIndex * 0.5;
                    } else if (mode == 2) {
                        uv.y = uv.y * 0.5 + (1.0 - eyeIndex) * 0.5;
                    }
                    return uv * scaleOffset.xy + scaleOffset.zw;
                }

                INLINE FIXED4 GammaCorrection(FIXED4 x) {return x;}
                // ----------------------------------------------------------

                #pragma only_renderers gles gles3
                #pragma multi_compile __ USE_NV12 USE_YUV420P

                #extension GL_OES_EGL_image_external       : enable
                #extension GL_OES_EGL_image_external_essl3 : enable
                #extension GL_OVR_multiview                : enable
                #extension GL_OVR_multiview2               : enable

                precision highp float;

                uniform FLOAT4X4 _ColorSpaceTransformMatrix;
                uniform FLOAT4X4 _TextureTransformMatrix;
                uniform FLOAT4X4 _AlphaTextureTransformMatrix;
                uniform float _MSDK_UseCustomColorMatrix;
                uniform float _MSDK_RgbUvScaleX;
                uniform float _MSDK_RgbUvScaleY;
                uniform float _MSDK_RgbUvOffsetX;
                uniform float _MSDK_RgbUvOffsetY;
                uniform float _MSDK_AlphaUvScaleX;
                uniform float _MSDK_AlphaUvScaleY;
                uniform float _MSDK_AlphaUvOffsetX;
                uniform float _MSDK_AlphaUvOffsetY;
                uniform float _MSDK_RgbStereoMode;
                uniform float _MSDK_AlphaStereoMode;
                uniform float _MSDK_DebugAlpha;
                uniform float _MSDK_DebugRgbOpaque;
                uniform float _MSDK_FlipV;
                uniform vec4 _Color;

                const FLOAT4X4 MSDK_IDENTITY = FLOAT4X4(
                    1.0, 0.0, 0.0, 0.0,
                    0.0, 1.0, 0.0, 0.0,
                    0.0, 0.0, 1.0, 0.0,
                    0.0, 0.0, 0.0, 1.0
                );

                INLINE float AbsSum4(FLOAT4 v) { return abs(v.x) + abs(v.y) + abs(v.z) + abs(v.w); }
                INLINE float MatrixAbsSum(FLOAT4X4 m) { return AbsSum4(m[0]) + AbsSum4(m[1]) + AbsSum4(m[2]) + AbsSum4(m[3]); }

                INLINE float ShouldUseCustomColorMatrix()
                {
                    if (_MSDK_UseCustomColorMatrix > 0.5) return 1.0;
                    float sum = MatrixAbsSum(_ColorSpaceTransformMatrix);
                    float identDiff = MatrixAbsSum(_ColorSpaceTransformMatrix - MSDK_IDENTITY);
                    if (sum < 0.0001 || identDiff < 0.0001) return 0.0;
                    return 1.0;
                }

                INLINE FLOAT4X4 ResolveTextureMatrix(FLOAT4X4 m)
                {
                    if (MatrixAbsSum(m) < 0.0001) return MSDK_IDENTITY;
                    return m;
                }

                INLINE FLOAT4X4 ResolveAlphaTextureMatrix()
                {
                    if (MatrixAbsSum(_AlphaTextureTransformMatrix) < 0.0001)
                        return ResolveTextureMatrix(_TextureTransformMatrix);
                    return _AlphaTextureTransformMatrix;
                }

                INLINE FLOAT3 SaturateFloat3(FLOAT3 v)
                {
                    #if defined(SHADERLAB_GLSL)
                        return clamp(v, 0.0, 1.0);
                    #else
                        return saturate(v);
                    #endif
                }

                INLINE FLOAT3 ConvertYpCbCrToRGB_BT709(FLOAT3 yuv)
                {
                    CONST_FLOAT kYScale = 1.1643835616;
                    CONST_FLOAT kYBias  = -0.0627450980;
                    CONST_FLOAT kUBias  = -0.5019607843;
                    CONST_FLOAT kVBias  = -0.5019607843;
                    CONST_FLOAT kR_V    = 1.8336712329;
                    CONST_FLOAT kG_U    = -0.2181173041;
                    CONST_FLOAT kG_V    = -0.5450762082;
                    CONST_FLOAT kB_U    = 2.1606301370;

                    FLOAT3 yuvAdj = FLOAT3(yuv.x + kYBias, yuv.y + kUBias, yuv.z + kVBias);
                    FLOAT3 rgb;
                    rgb.r = kYScale * yuvAdj.x + kR_V * yuvAdj.z;
                    rgb.g = kYScale * yuvAdj.x + kG_U * yuvAdj.y + kG_V * yuvAdj.z;
                    rgb.b = kYScale * yuvAdj.x + kB_U * yuvAdj.y;
                    return SaturateFloat3(rgb);
                }

                #ifdef VERTEX
                    #if __VERSION__ >= 300
                        layout(std140) uniform UnityStereoGlobals {
                            mat4 unity_StereoMatrixP[2];
                            mat4 unity_StereoMatrixV[2];
                            mat4 unity_StereoMatrixInvV[2];
                            mat4 unity_StereoMatrixVP[2];
                            mat4 unity_StereoCameraProjection[2];
                            mat4 unity_StereoCameraInvProjection[2];
                            mat4 unity_StereoWorldToCamera[2];
                            mat4 unity_StereoCameraToWorld[2];
                            vec3 unity_StereoWorldSpaceCameraPos[2];
                            vec4 unity_StereoScaleOffset[2];
                        };

                        layout(std140) uniform UnityStereoEyeIndices {
                            vec4 unity_StereoEyeIndices[2];
                        };

                        #if defined(STEREO_MULTIVIEW_ON)
                            layout(num_views = 2) in;
                        #endif
                    #endif

                    varying vec2 vUv0;
                    varying vec2 vUv1;
                    varying float vEye;

                    void main()
                    {
                        int eyeIndex = GetStereoEyeIndex();
                        #if defined (STEREO_MULTIVIEW_ON)
                            gl_Position = unity_StereoMatrixVP[eyeIndex] * unity_ObjectToWorld * gl_Vertex;
                        #else
                            gl_Position = unity_MatrixVP * unity_ObjectToWorld * gl_Vertex;
                        #endif

                        vUv0 = gl_MultiTexCoord0.xy;
                        vUv1 = gl_MultiTexCoord1.xy;
                        vEye = float(eyeIndex);
                    }
                #endif

                #ifdef FRAGMENT
                    varying vec2 vUv0;
                    varying vec2 vUv1;
                    varying float vEye;

                    uniform SAMPLER2D _MSDK_VideoTex0;
                    #if defined(USE_NV12) || defined(USE_YUV420P)
                        uniform SAMPLER2D _MSDK_VideoTex1;
                    #endif
                    #if defined(USE_YUV420P)
                        uniform SAMPLER2D _MSDK_VideoTex2;
                    #endif

                INLINE FIXED4 SampleVideoPixel(FLOAT2 uv, float useCustomMatrix)
                {
                    #if defined(USE_NV12)
                        FLOAT3 yuv = FLOAT3(TEX2D(_MSDK_VideoTex0, uv).r, TEX2D(_MSDK_VideoTex1, uv).rg);
                        FLOAT3 rgb = (useCustomMatrix > 0.5)
                            ? ConvertYpCbCrToRGB(yuv, _ColorSpaceTransformMatrix)
                            : ConvertYpCbCrToRGB_BT709(yuv);
                        return GammaCorrection(FIXED4(rgb, 1.0));
                    #elif defined(USE_YUV420P)
                        FLOAT3 yuv = FLOAT3(TEX2D(_MSDK_VideoTex0, uv).r, TEX2D(_MSDK_VideoTex1, uv).r, TEX2D(_MSDK_VideoTex2, uv).r);
                        FLOAT3 rgb = (useCustomMatrix > 0.5)
                            ? ConvertYpCbCrToRGB(yuv, _ColorSpaceTransformMatrix)
                            : ConvertYpCbCrToRGB_BT709(yuv);
                        return GammaCorrection(FIXED4(rgb, 1.0));
                    #else
                        return GammaCorrection(TEX2D(_MSDK_VideoTex0, uv));
                    #endif
                }

                INLINE float SampleAlphaLuma(FLOAT2 uv)
                {
                    #if defined(USE_NV12) || defined(USE_YUV420P)
                        // Alpha mask is authored in the original RGB image, so luma is stored in the Y plane.
                        return RemapLimitedRangeToFull(TEX2D(_MSDK_VideoTex0, uv).r);
                    #else
                        FIXED4 rgba = TEX2D(_MSDK_VideoTex0, uv);
                        return clamp(dot(rgba.rgb, FLOAT3(0.2126, 0.7152, 0.0722)), 0.0, 1.0);
                    #endif
                }

                void main()
                {
                    FLOAT4X4 rgbMatrix = ResolveTextureMatrix(_TextureTransformMatrix);
                    FLOAT4X4 alphaMatrix = ResolveAlphaTextureMatrix();
                    float useCustomMatrix = ShouldUseCustomColorMatrix();

                    int rgbMode = int(_MSDK_RgbStereoMode + 0.5);
                    int alphaMode = int(_MSDK_AlphaStereoMode + 0.5);
                    FLOAT4 rgbScaleOffset = FLOAT4(_MSDK_RgbUvScaleX, _MSDK_RgbUvScaleY, _MSDK_RgbUvOffsetX, _MSDK_RgbUvOffsetY);
                    FLOAT4 alphaScaleOffset = FLOAT4(_MSDK_AlphaUvScaleX, _MSDK_AlphaUvScaleY, _MSDK_AlphaUvOffsetX, _MSDK_AlphaUvOffsetY);
                    FLOAT2 rgbUv = ApplyStereoMode(vUv0, vUv1, vEye, rgbMode, rgbScaleOffset, _MSDK_FlipV);
                    FLOAT2 alphaUv = ApplyStereoMode(vUv0, vUv1, vEye, alphaMode, alphaScaleOffset, _MSDK_FlipV);
                    rgbUv = ApplyTextureTransformMatrix(rgbUv, rgbMatrix);
                    alphaUv = ApplyTextureTransformMatrix(alphaUv, alphaMatrix);

                    FIXED4 col = SampleVideoPixel(rgbUv, useCustomMatrix);
                    if (_MSDK_DebugRgbOpaque > 0.5)
                        col.a = 1.0;
                    else
                        col.a = clamp(SampleAlphaLuma(alphaUv), 0.0, 1.0);

                    col *= _Color;
                    if (_MSDK_DebugAlpha > 0.5) {
                        gl_FragColor = vec4(col.a, col.a, col.a, 1.0);
                    } else {
                    gl_FragColor = col;
                    }
                }
                #endif
            ENDGLSL
        }
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Lighting Off
        Cull Off
        ZWrite [_ZWrite]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Default"
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma only_renderers metal d3d11 glcore vulkan
                #pragma multi_compile __ USE_NV12 USE_YUV420P

                #include "UnityCG.cginc"
                // ---- Minimal ClearVR utilities (inlined) -----------------
                #define CONST_FLOAT const float
                #define FLOAT4 float4
                #define FLOAT3 float3
                #define FLOAT2 float2
                #define FLOAT4X4 float4x4
                #define FLOAT3X3 float3x3
                #define INLINE inline
                #define ATAN atan2
                #define FIXED4 fixed4
                #define FIXED3 fixed3
                #define TEX2D tex2D
                #define SAMPLER2D sampler2D

                INLINE int GetStereoEyeIndex() {
                #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED)
                    return int(unity_StereoEyeIndex);
                #elif defined (STEREO_MULTIVIEW_ON)
                #   if defined(SHADER_API_VULKAN)
                        return int(unity_StereoEyeIndex);
                #   else
                        return int(gl_ViewID_OVR);
                #   endif
                #elif defined(UNITY_DECLARE_MULTIVIEW)
                    return int(UNITY_VIEWID);
                #else
                    return 0;
                #endif
                }

                INLINE FLOAT2 ApplyTextureTransformMatrix(FLOAT2 uv, FLOAT4X4 textureTransformMatrix) {
                    FLOAT4 newHomogeneousUV = mul(textureTransformMatrix, FLOAT4(uv.x, uv.y, 0.0, 1.0));
                    return newHomogeneousUV.xy / newHomogeneousUV.w;
                }

                INLINE FLOAT2 ApplyScaleOffset(FLOAT2 uv, FLOAT4 scaleOffset) {
                    return uv * scaleOffset.xy + scaleOffset.zw;
                }

                INLINE FLOAT3 ConvertYpCbCrToRGB(FLOAT3 yuv, FLOAT4X4 colorSpaceTextureTransformMatrix)
                {
                    return saturate(mul((FLOAT3X3)colorSpaceTextureTransformMatrix, yuv + colorSpaceTextureTransformMatrix[3].xyz));
                }

                INLINE float RemapLimitedRangeToFull(float y)
                {
                    const float kBlack = 16.0 / 255.0;
                    const float kWhite = 235.0 / 255.0;
                    return saturate((y - kBlack) * (255.0 / (235.0 - 16.0)));
                }

                INLINE FLOAT2 ApplyStereoMode(FLOAT2 uv0, FLOAT2 uv1, float eyeIndex, int mode, FLOAT4 scaleOffset, float flipV)
                {
                    if (flipV > 0.5) {
                        uv0.y = 1.0 - uv0.y;
                        uv1.y = 1.0 - uv1.y;
                    }
                    FLOAT2 uv = (mode == 3 && eyeIndex > 0.5) ? uv1 : uv0;
                    if (mode == 1) {
                        uv.x = uv.x * 0.5 + eyeIndex * 0.5;
                    } else if (mode == 2) {
                        uv.y = uv.y * 0.5 + (1.0 - eyeIndex) * 0.5;
                    }
                    return uv * scaleOffset.xy + scaleOffset.zw;
                }

                INLINE FIXED4 GammaCorrection(FIXED4 x) {return x;}
                // ----------------------------------------------------------
                
                uniform SAMPLER2D _MSDK_VideoTex0;
                #if defined(USE_NV12) || defined(USE_YUV420P)
                    uniform SAMPLER2D _MSDK_VideoTex1;
                #endif
                #if defined(USE_YUV420P)
                    uniform SAMPLER2D _MSDK_VideoTex2;
                #endif
                uniform FLOAT4X4 _ColorSpaceTransformMatrix;

                uniform FLOAT4X4 _TextureTransformMatrix;
                uniform FLOAT4X4 _AlphaTextureTransformMatrix;
                uniform float _MSDK_UseCustomColorMatrix;
                uniform float _MSDK_RgbUvScaleX;
                uniform float _MSDK_RgbUvScaleY;
                uniform float _MSDK_RgbUvOffsetX;
                uniform float _MSDK_RgbUvOffsetY;
                uniform float _MSDK_AlphaUvScaleX;
                uniform float _MSDK_AlphaUvScaleY;
                uniform float _MSDK_AlphaUvOffsetX;
                uniform float _MSDK_AlphaUvOffsetY;
                uniform float _MSDK_RgbStereoMode;
                uniform float _MSDK_AlphaStereoMode;
                uniform float _MSDK_DebugAlpha;
                uniform float _MSDK_DebugRgbOpaque;
                uniform float _MSDK_FlipV;
                uniform float4 _Color;


                const FLOAT4X4 MSDK_IDENTITY = FLOAT4X4(
                    1.0, 0.0, 0.0, 0.0,
                    0.0, 1.0, 0.0, 0.0,
                    0.0, 0.0, 1.0, 0.0,
                    0.0, 0.0, 0.0, 1.0
                );

                INLINE float AbsSum4(FLOAT4 v) { return abs(v.x) + abs(v.y) + abs(v.z) + abs(v.w); }
                INLINE float MatrixAbsSum(FLOAT4X4 m) { return AbsSum4(m[0]) + AbsSum4(m[1]) + AbsSum4(m[2]) + AbsSum4(m[3]); }

                INLINE float ShouldUseCustomColorMatrix()
                {
                    if (_MSDK_UseCustomColorMatrix > 0.5) return 1.0;
                    float sum = MatrixAbsSum(_ColorSpaceTransformMatrix);
                    float identDiff = MatrixAbsSum(_ColorSpaceTransformMatrix - MSDK_IDENTITY);
                    if (sum < 0.0001 || identDiff < 0.0001) return 0.0;
                    return 1.0;
                }

                INLINE FLOAT4X4 ResolveTextureMatrix(FLOAT4X4 m)
                {
                    if (MatrixAbsSum(m) < 0.0001) return MSDK_IDENTITY;
                    return m;
                }

                INLINE FLOAT4X4 ResolveAlphaTextureMatrix()
                {
                    if (MatrixAbsSum(_AlphaTextureTransformMatrix) < 0.0001)
                        return ResolveTextureMatrix(_TextureTransformMatrix);
                    return _AlphaTextureTransformMatrix;
                }

                INLINE FLOAT3 SaturateFloat3(FLOAT3 v)
                {
                    #if defined(SHADERLAB_GLSL)
                        return clamp(v, 0.0, 1.0);
                    #else
                        return saturate(v);
                    #endif
                }

                INLINE FLOAT3 ConvertYpCbCrToRGB_BT709(FLOAT3 yuv)
                {
                    CONST_FLOAT kYScale = 1.1643835616;
                    CONST_FLOAT kYBias  = -0.0627450980;
                    CONST_FLOAT kUBias  = -0.5019607843;
                    CONST_FLOAT kVBias  = -0.5019607843;
                    CONST_FLOAT kR_V    = 1.8336712329;
                    CONST_FLOAT kG_U    = -0.2181173041;
                    CONST_FLOAT kG_V    = -0.5450762082;
                    CONST_FLOAT kB_U    = 2.1606301370;

                    FLOAT3 yuvAdj = FLOAT3(yuv.x + kYBias, yuv.y + kUBias, yuv.z + kVBias);
                    FLOAT3 rgb;
                    rgb.r = kYScale * yuvAdj.x + kR_V * yuvAdj.z;
                    rgb.g = kYScale * yuvAdj.x + kG_U * yuvAdj.y + kG_V * yuvAdj.z;
                    rgb.b = kYScale * yuvAdj.x + kB_U * yuvAdj.y;
                    return SaturateFloat3(rgb);
                }

                struct appdata
                {
                    float4 vertex : POSITION;
                    fixed4 color  : COLOR;
                    float2 uv     : TEXCOORD0;
                    float2 uv2    : TEXCOORD1;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    fixed4 color  : COLOR;
                    float2 uv0    : TEXCOORD0;
                    float2 uv1    : TEXCOORD1;
                    float eye     : TEXCOORD2;
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                v2f vert (appdata appData)
                {
                    v2f output;
                    UNITY_SETUP_INSTANCE_ID(appData);
                    UNITY_INITIALIZE_OUTPUT(v2f, output);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                    output.vertex = UnityObjectToClipPos(appData.vertex);
                    output.color = appData.color * _Color;

                    int eyeIndex = GetStereoEyeIndex();
                    output.uv0 = appData.uv;
                    output.uv1 = appData.uv2;
                    output.eye = eyeIndex;
                    return output;
                }

                INLINE FIXED4 SampleVideoPixel(FLOAT2 uv, float useCustomMatrix)
                {
                    #if defined(USE_NV12)
                        FLOAT3 yuv = FLOAT3(TEX2D(_MSDK_VideoTex0, uv).r, TEX2D(_MSDK_VideoTex1, uv).rg);
                        FLOAT3 rgb = (useCustomMatrix > 0.5)
                            ? ConvertYpCbCrToRGB(yuv, _ColorSpaceTransformMatrix)
                            : ConvertYpCbCrToRGB_BT709(yuv);
                        return GammaCorrection(FIXED4(rgb, 1.0));
                    #elif defined(USE_YUV420P)
                        FLOAT3 yuv = FLOAT3(TEX2D(_MSDK_VideoTex0, uv).r, TEX2D(_MSDK_VideoTex1, uv).r, TEX2D(_MSDK_VideoTex2, uv).r);
                        FLOAT3 rgb = (useCustomMatrix > 0.5)
                            ? ConvertYpCbCrToRGB(yuv, _ColorSpaceTransformMatrix)
                            : ConvertYpCbCrToRGB_BT709(yuv);
                        return GammaCorrection(FIXED4(rgb, 1.0));
                    #else
                        return GammaCorrection(TEX2D(_MSDK_VideoTex0, uv));
                    #endif
                }

                INLINE float SampleAlphaLuma(FLOAT2 uv)
                {
                    #if defined(USE_NV12) || defined(USE_YUV420P)
                        // Alpha mask is authored in the original RGB image, so luma is stored in the Y plane.
                        return RemapLimitedRangeToFull(TEX2D(_MSDK_VideoTex0, uv).r);
                    #else
                        FIXED4 rgba = TEX2D(_MSDK_VideoTex0, uv);
                        return saturate(dot(rgba.rgb, FLOAT3(0.2126, 0.7152, 0.0722)));
                    #endif
                }

                fixed4 frag (v2f input) : SV_Target
                {
                    FLOAT4X4 rgbMatrix = ResolveTextureMatrix(_TextureTransformMatrix);
                    FLOAT4X4 alphaMatrix = ResolveAlphaTextureMatrix();
                    float useCustomMatrix = ShouldUseCustomColorMatrix();

                    int rgbMode = (int)(_MSDK_RgbStereoMode + 0.5);
                    int alphaMode = (int)(_MSDK_AlphaStereoMode + 0.5);
                    FLOAT4 rgbScaleOffset = FLOAT4(_MSDK_RgbUvScaleX, _MSDK_RgbUvScaleY, _MSDK_RgbUvOffsetX, _MSDK_RgbUvOffsetY);
                    FLOAT4 alphaScaleOffset = FLOAT4(_MSDK_AlphaUvScaleX, _MSDK_AlphaUvScaleY, _MSDK_AlphaUvOffsetX, _MSDK_AlphaUvOffsetY);
                    FLOAT2 rgbUv = ApplyStereoMode(input.uv0, input.uv1, input.eye, rgbMode, rgbScaleOffset, _MSDK_FlipV);
                    FLOAT2 alphaUv = ApplyStereoMode(input.uv0, input.uv1, input.eye, alphaMode, alphaScaleOffset, _MSDK_FlipV);
                    rgbUv = ApplyTextureTransformMatrix(rgbUv, rgbMatrix);
                    alphaUv = ApplyTextureTransformMatrix(alphaUv, alphaMatrix);

                    FIXED4 col = SampleVideoPixel(rgbUv, useCustomMatrix);
                    if (_MSDK_DebugRgbOpaque > 0.5)
                        col.a = 1.0;
                    else
                        col.a = saturate(SampleAlphaLuma(alphaUv));

                    col *= input.color;
                    if (_MSDK_DebugAlpha > 0.5)
                        return FIXED4(col.a, col.a, col.a, 1.0);
                    return col;
                }
            ENDCG
        }
    }
}
