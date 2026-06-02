Shader "Unlit/Circle"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        [HDR]_Color ("Tint Color", Color) = (1,1,1,1)
        [HDR]_FresnelColor ("Fresnel Color", Color) = (0,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0,5)) = 1
        _FresnelPower ("Fresnel Power", Range(0.1,8)) = 3

        _FadePower ("Fade Power", Range(0.1, 8)) = 2

        _BreathSpeedX ("Breath Speed X", Range(0.1, 5)) = 1
        _BreathSpeedY ("Breath Speed Y", Range(0.1, 5)) = 1
        _BreathAmountX ("Breath Amount X", Range(0,0.2)) = 0.05
        _BreathAmountY ("Breath Amount Y", Range(0,0.2)) = 0.05
    }

    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            float4 _FresnelColor;
            float _GlowIntensity;
            float _FresnelPower;
            float _FadePower;
            float _BreathSpeedX;
            float _BreathAmountX;
            float _BreathSpeedY;
            float _BreathAmountY;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            // half4 frag(Varyings IN) : SV_Target
            // {
            //     float4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
            //
            //     // 用UV模拟Fresnel：圆心距越远越亮
            //     float2 center = float2(0.5, 0.5);
            //     float dist = distance(IN.uv, center);
            //     float fresnel = pow(saturate(dist * 2.0), _FresnelPower); // 边缘亮
            //
            //     float3 glow = fresnel * _FresnelColor.rgb * _GlowIntensity;
            //
            //     // 上下透明渐变
            //     float fade = saturate(1.0 - IN.uv.y); // 上透明，下不透明
            //     float alpha = baseCol.a * fade;
            //
            //     float3 finalColor = baseCol.rgb + glow;
            //     return float4(finalColor, alpha);
            // }
            // half4 frag(Varyings IN) : SV_Target
            // {
            //     float4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
            //
            //     // 用 UV 模拟 Fresnel：圆心距越远越亮
            //     float2 center = float2(0.5, 0.5);
            //     float dist = distance(IN.uv, center);
            //     float fresnel = pow(saturate(dist * 2.0), _FresnelPower);
            //
            //     float3 glow = fresnel * _FresnelColor.rgb * _GlowIntensity;
            //
            //     // ✅ 上下透明渐变（非线性加速）
            //     float fade = pow(saturate(1.0 - IN.uv.y), _FadePower);
            //
            //     float alpha = baseCol.a * fade;
            //     float3 finalColor = baseCol.rgb + glow;
            //
            //     return float4(finalColor, alpha);
            // }

            half4 frag(Varyings IN) : SV_Target
            {
                // -------------------
                // ✅ 呼吸缩放效果
                // -------------------
                float t = _Time.y;
                float scaleX = 1.0 + sin(t * _BreathSpeedX) * _BreathAmountX;
                float scaleY = 1.0 + sin(t * _BreathSpeedY) * _BreathAmountY;
                float2 center = float2(0.5, 0.5);
                float2 uv = (IN.uv - center) / float2(scaleX, scaleY) + center;

                // -------------------
                // Fresnel 边缘发光
                // -------------------
                float dist = distance(uv, center);
                float fresnel = pow(saturate(dist * 2.0), _FresnelPower);
                float3 glow = fresnel * _FresnelColor.rgb * _GlowIntensity;

                // -------------------
                // 上下透明渐变（非线性）
                // -------------------
                float fade = pow(saturate(1.0 - uv.y), _FadePower);

                float4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
                float alpha = baseCol.a * fade;

                float3 finalColor = baseCol.rgb + glow;
                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
