Shader "Tennis/BonusZone"
{
    Properties { _Visibility ("Visibility", Range(0,1)) = 1 _HitFlash ("Hit Flash", Range(0,1)) = 0 _ZoneColor ("Zone Color", Color) = (1,.08,.025,1) _ClipRect ("Table bounds", Vector) = (-1000, -1000, 1000, 1000) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 worldPos : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            float _Visibility;
            float _HitFlash;
            float4 _ZoneColor;
            float4 _ClipRect;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; o.worldPos = TransformObjectToWorld(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target
            {
                clip(min(min(i.worldPos.x - _ClipRect.x, _ClipRect.z - i.worldPos.x),
                    min(i.worldPos.z - _ClipRect.y, _ClipRect.w - i.worldPos.z)));
                float2 p = i.uv;
                float edge = min(min(p.x, 1-p.x), min(p.y, 1-p.y));
                float border = 1-smoothstep(0.004, 0.012, edge);
                float sweep = pow(saturate(1-abs(frac((p.x + p.y)*.9-_Time.y*.55)-.5)*3), 2);
                float bands = .5 + .5*sin((p.x - p.y)*16 - _Time.y*4.5);
                float pulse = .7 + .3*sin(_Time.y*4.5);
                float glow = border;
                float3 fill = _ZoneColor.rgb * lerp(.35, 1.5, saturate(sweep*.8 + bands*.25));
                float3 color = lerp(fill, _ZoneColor.rgb * 2.2 + .16, glow);
                color = lerp(color, _ZoneColor.rgb * 2.5 + 1.0, _HitFlash);
                float alpha = saturate(.22 + sweep*.35 + bands*.12 + glow*.65 + _HitFlash*.5) * pulse;
                alpha = max(alpha, _HitFlash * .9) * _Visibility;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

