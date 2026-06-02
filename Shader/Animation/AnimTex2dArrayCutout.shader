//在序列帧动画读取Texture2DArray中的动画帧,计算光照
Shader "WarField/AnimTex2dArrayCutout"
{
    Properties
    {
        [Header(Texture Arrays)]
        _MainTexArray ("Color Sprite Array (RGB)", 2DArray) = "" {}
        _NormalTexArray ("Normal Map Array (RGB)", 2DArray) = "" {}

        [Header(Render Settings)]
        _Cutoff ("Alpha Cutoff (Keep low for baked shadows)", Range(0.0, 1.0)) = 0.05
        _NormalStrength ("Normal Map Strength", Range(0.0, 2.0)) = 1.0
        _Smoothness ("Specular Smoothness", Range(1.0, 128.0)) = 32.0
        _SpecularIntensity ("Specular Intensity", Range(0.0, 2.0)) = 0.2

        // 兜底自发光: 当场景未烘焙 Lighting / 无 Skybox 时 SampleSH 返回 ≈ 0,
        // 单靠主灯方向 + 俯视角会出现大面积过暗. 这个值给环境光做下限,
        // 让 RTS 视角下的兵种在任何光照配置都保持基础可见度.
        _AmbientFloor ("Ambient Floor (Self-Illumination)", Range(0.0, 1.0)) = 0.4

        // 同时声明为材质属性 + 实例化属性.
        // 1) 运行时 MPB 注入每个士兵实时数值, 显卡按 InstanceID 隔离取用.
        // 2) 编辑器/Prefab 预览态 MPB 未设置时, 硬件自动回落到 Properties 默认值
        _FinalSliceIndex ("Slice Index (Editor Preview)", Float) = 0
        _Alpha ("Alpha (Editor Preview)", Range(0.0, 1.0)) = 1.0

        // 设定该兵种期望在游戏里的真实世界尺寸基础（以脚底中心对齐按固定八角 Mesh 缩放）
        _TotalWorldSize ("Total World Size (Meters)", Float) = 2.0
    }

    SubShader
    {
        // 核心防线: AlphaTest 队列 + ZWrite On, 遮挡完全交给硬件 Z-Buffer
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off      // 双面渲染, 防 mesh 反转穿帮
        ZWrite On     // 深度写入开启，完全不依赖 CPU 排序
        ZTest LEqual

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5 // Texture2DArray 硬件级底层采样最低要求

            #pragma vertex vert
            #pragma fragment frag

            // GPU Instancing: 1 兵种 = 1 Draw Call
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_MainTexArray);
            TEXTURE2D_ARRAY(_NormalTexArray);
            SAMPLER(sampler_MainTexArray);

            // 2. 材质常量缓冲区 (兼容 SRP Batcher)
            CBUFFER_START(UnityPerMaterial)
                half _Cutoff;
                half _NormalStrength;
                half _Smoothness;
                half _SpecularIntensity;
                half _AmbientFloor;
                float _TotalWorldSize;
            CBUFFER_END

            // 3. GPU Instancing 每个士兵独立的个性化实例流
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _FinalSliceIndex)
                UNITY_DEFINE_INSTANCED_PROP(float, _Alpha)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 tangentWS    : TEXCOORD3;
                float3 bitangentWS  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // 此处只需直接乘以兵种期望的物理尺寸（_TotalWorldSize）完成世界矩阵映射即可，几何体纯净稳定。
                float3 displacedPosOS = input.positionOS.xyz * _TotalWorldSize;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(displacedPosOS);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                // UV 在烘焙时已完成画布空间到 Alpha 身体像素的静态对齐，直接透传片元，零运行时消耗。
                output.uv = input.uv;

                // 变换法线/切线/副法线, 为片元 TBN 矩阵提供物理基础
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;

                return output;
            }

            // 4x4 Bayer 屏幕空间抖动隐身控制
            void ApplyDitherStealth(float2 screenPos, float alpha)
            {
                const float ditherValues[16] = {
                    1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0,  11.0 / 17.0,
                    13.0 / 17.0, 5.0 / 17.0,  15.0 / 17.0, 7.0 / 17.0,
                    4.0 / 17.0,  12.0 / 17.0, 2.0 / 17.0,  10.0 / 17.0,
                    16.0 / 17.0, 8.0 / 17.0,  14.0 / 17.0, 6.0 / 17.0
                };

                int x = int(fmod(screenPos.x, 4.0));
                int y = int(fmod(screenPos.y, 4.0));
                clip(alpha - ditherValues[x + y * 4]);
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                // 1. 动态读取当前个体由 C# 传入的动画帧页码与不透明度
                float sliceIndexF = UNITY_ACCESS_INSTANCED_PROP(Props, _FinalSliceIndex);
                float customAlpha = UNITY_ACCESS_INSTANCED_PROP(Props, _Alpha);
                int sliceIndex = max((int)round(sliceIndexF), 0);

                if (customAlpha < 0.01 || customAlpha > 1.01)
		    customAlpha = 1.0;

                ApplyDitherStealth(input.positionCS.xy, customAlpha);

                // 此时采样的 input.uv 刚好就是 Frame2TextureArray 烘焙时按 Alpha 边界收紧后的精准像素区间, 避免 Overdraw
                // 注意: 烘焙管线走的是【直 Alpha + Alpha Bleed (最近身体色外扩, alpha=0)】, 不是预乘 Alpha,
                // 也不是黑色描边 Matte. 透明区的 RGB 已经在烘焙端被拷成最近身体像素颜色, 所以 bilinear / mip
                // 在身体边缘读到的 RGB 始终接近身体色, 不会出现暗环; alpha 只在身体外圈做 255→0 的羽化,
                // 硬切 alpha test (clip) 只会去掉 alpha 不够的羽边.
                // 所以这里【绝对不能】再做 1/alpha 的反预乘补偿. 历史上曾经写过
                //     rgbBoost = clamp(1/alpha, 1.0, 1.8); rgb *= rgbBoost;
                // 但它会把 BC7 在 alpha 0.2~0.56 区间的块压缩噪声放大成黑白毛刺,
                // 并把所有半透边缘像素强行提亮 1.8 倍 → Prefab 静态画面整圈白边.
                float4 albedoColor = SAMPLE_TEXTURE2D_ARRAY(_MainTexArray, sampler_MainTexArray, input.uv, sliceIndex);
                clip(albedoColor.a - _Cutoff);

                float4 normalSample = SAMPLE_TEXTURE2D_ARRAY(_NormalTexArray, sampler_MainTexArray, input.uv, sliceIndex);
                float3 tangentNormal = UnpackNormalScale(normalSample, _NormalStrength);

                float3 transformNormal = normalize(input.normalWS);
                float3 transformTangent = normalize(input.tangentWS);
                float3 transformBitangent = normalize(input.bitangentWS);
                float3x3 tangentToWorld = float3x3(transformTangent, transformBitangent, transformNormal);
                float3 worldNormal = normalize(mul(tangentNormal, tangentToWorld));

                // 6. 适配竖直 Quad + 俯视角 RTS 的光照模型
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);

                // (a) 环境光: SampleSH 兜底取 max(_, _AmbientFloor),
                //     防止场景没烘 SH / 没 Skybox 时角色直接黑成一团.
                half3 ambient = max(SampleSH(worldNormal), _AmbientFloor.xxx);

                // (b) Half-Lambert 漫反射: 把 [-1,1] 重映射到 [0,1], 取代原本 saturate(dot)
                //     的硬截断. 竖直 Quad 大量像素的 N·L 是负值 (俯视角主灯打向背面),
                //     原始 Lambert 把这部分直接掐成 0 形成大片死黑;
                //     Half-Lambert 让背面也保留 ~0.5 的亮度过渡, 更适合 2D 卡通/序列帧.
                half nDotL = dot(worldNormal, lightDir);
                half diffuseTerm = nDotL * 0.5 + 0.5;
                half3 diffuseLight = mainLight.color * diffuseTerm;

                // (c) Specular 仅按真正的正面 saturate, 避免背面高光穿帮
                float3 halfDir = normalize(lightDir + viewDir);
                half specularTerm = pow(saturate(dot(worldNormal, halfDir)), _Smoothness) * _SpecularIntensity * saturate(nDotL);
                half3 specularLight = mainLight.color * specularTerm;

                half3 finalRGB = albedoColor.rgb * (ambient + diffuseLight) + specularLight;
                return float4(finalRGB, albedoColor.a);
            }
            ENDHLSL
        }
    }
}
