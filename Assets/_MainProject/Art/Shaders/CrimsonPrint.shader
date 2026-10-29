Shader "Tennis/Crimson Print"
{
    Properties
    {
        _ShadowTint ("Shadow Tint", Color) = (0.65, 0.08, 0.05, 1)
        _ShadowStrength ("Shadow Tint Strength", Range(0, 1)) = 0.3
        _GrainStrength ("Grain Strength", Range(0, 0.1)) = 0.004
        _PosterizeSteps ("Color Steps", Range(8, 128)) = 64
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Crimson Print"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _ShadowTint;
            float _ShadowStrength;
            float _GrainStrength;
            float _PosterizeSteps;
            float _DitherStrength;

            float HashPixel(float2 pixel)
            {
                return frac(sin(dot(pixel, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord * _BlitScaleBias.xy + _BlitScaleBias.zw;
                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // Restrict the red cast to darker values so bright ball and court markings stay legible.
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float shadowMask = 1.0 - smoothstep(0.04, 0.5, luminance);
                color += _ShadowTint.rgb * shadowMask * _ShadowStrength * 0.05;

                float noise = HashPixel(floor(input.positionCS.xy)) - 0.5;
                color += noise * _GrainStrength;
                float steps = max(_PosterizeSteps, 8.0);
                color = floor(saturate(color) * steps + 0.5 + noise * _DitherStrength) / steps;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
