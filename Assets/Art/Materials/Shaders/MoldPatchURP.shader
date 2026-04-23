Shader "FNaS/MoldPatchGraphLikeURP"
{
    Properties
    {
        _MoldTex("Mold Tex", 2D) = "white" {}
        _BloodTex("Blood Tex", 2D) = "white" {}
        _NoiseTex("Noise Tex", 2D) = "gray" {}

        _MoldTint("Mold Tint", Color) = (0.35, 0.45, 0.35, 1)
        _BloodTint("Blood Tint", Color) = (0.45, 0.08, 0.08, 1)

        _Fill("Fill", Range(0,1)) = 1
        _BloodBlend("Blood Blend", Range(0,1)) = 0
        _GrowthSoftness("Growth Softness", Range(0.001,0.5)) = 0.12

        _NoiseTiling("Noise Tiling", Float) = 2
        _TextureTiling("Texture Tiling", Float) = 1
        _AlphaStrength("Alpha Strength", Range(0,4)) = 1.25

        _RoughnessMold("Roughness Mold", Range(0,1)) = 0.85
        _RoughnessBlood("Roughness Blood", Range(0,1)) = 0.55

        _EdgeFeather("Edge Feather", Range(0.001,0.25)) = 0.10
        _EdgeStrength("Edge Strength", Range(0,1)) = 1.0

        _BloodThresholdMin("Blood Threshold Min", Range(0,1)) = 0.10
        _BloodThresholdMax("Blood Threshold Max", Range(0,1)) = 0.70

        _UVShrink("UV Shrink", Range(0,0.1)) = 0.02
        _BloodClusterStrength("Blood Cluster Strength", Range(0,1)) = 0.45

        _BloodMoldSparsity("Blood Mold Sparsity", Range(0,1)) = 0.45
        _BloodNoiseBias("Blood Noise Bias", Range(0,1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            TEXTURE2D(_MoldTex);
            SAMPLER(sampler_MoldTex);

            TEXTURE2D(_BloodTex);
            SAMPLER(sampler_BloodTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MoldTint;
                float4 _BloodTint;
                float _Fill;
                float _BloodBlend;
                float _GrowthSoftness;
                float _NoiseTiling;
                float _TextureTiling;
                float _AlphaStrength;
                float _RoughnessMold;
                float _RoughnessBlood;
                float _EdgeFeather;
                float _EdgeStrength;
                float _BloodThresholdMin;
                float _BloodThresholdMax;
                float _UVShrink;
                float _BloodClusterStrength;
                float _BloodMoldSparsity;
                float _BloodNoiseBias;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = NormalizeNormalPerVertex(nrmInputs.normalWS);
                OUT.uv = IN.uv;
                OUT.shadowCoord = GetShadowCoord(posInputs);

                return OUT;
            }

            float ComputeEdgeMask(float2 uv, float feather, float strength)
            {
                float left   = smoothstep(0.0, feather, uv.x);
                float right  = 1.0 - smoothstep(1.0 - feather, 1.0, uv.x);
                float bottom = smoothstep(0.0, feather, uv.y);
                float top    = 1.0 - smoothstep(1.0 - feather, 1.0, uv.y);

                float mask = left * right * bottom * top;
                return lerp(1.0, mask, strength);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 baseUV = IN.uv * (1.0 - _UVShrink) + (_UVShrink * 0.5);

                float2 texUV   = baseUV * _TextureTiling;
                float2 noiseUV = baseUV * _NoiseTiling;

                float moldR  = SAMPLE_TEXTURE2D(_MoldTex, sampler_MoldTex, texUV).r;
                float bloodR = SAMPLE_TEXTURE2D(_BloodTex, sampler_BloodTex, texUV).r;
                float noiseR = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                float edge1 = 1.0 - _Fill;
                float edge2 = edge1 + _GrowthSoftness;
                float growthMask = smoothstep(edge1, edge2, noiseR);

                float edgeMask = ComputeEdgeMask(baseUV, _EdgeFeather, _EdgeStrength);

                float moldPresence = lerp(1.0, moldR, 0.35);

                float bloodMaskSoft = smoothstep(_BloodThresholdMin, _BloodThresholdMax, bloodR);
                float clusteredBlood = saturate(lerp(bloodMaskSoft, max(bloodMaskSoft, moldR), _BloodClusterStrength));

                // NEW: blood phase makes mold break up more using noise
                float bloodSparsityMask = 1.0 - (_BloodBlend * _BloodMoldSparsity);
                float bloodNoiseCull = smoothstep(
                    _BloodBlend * _BloodNoiseBias,
                    _BloodBlend * _BloodNoiseBias + 0.20,
                    noiseR
                );

                float visibleMoldMask = saturate(moldPresence * growthMask * bloodSparsityMask * bloodNoiseCull);

                float moldAlpha = lerp(moldR, moldPresence, 0.35);
                float bloodAlpha = clusteredBlood;

                float texturePresence = lerp(moldAlpha, bloodAlpha, _BloodBlend);
                float finalAlpha = saturate(texturePresence * growthMask * edgeMask * _AlphaStrength);

                float3 moldColor = moldR * _MoldTint.rgb;
                float3 bloodColor = clusteredBlood * _BloodTint.rgb;

                float bloodInfluence = saturate(_BloodBlend);

                float3 baseColor = lerp(moldColor, bloodColor, bloodInfluence);

                float3 normalWS = normalize(IN.normalWS);

                Light mainLight = GetMainLight(IN.shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse = baseColor * mainLight.color * NdotL * mainLight.shadowAttenuation;

                float3 ambient = SampleSH(normalWS) * baseColor;

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0; i < lightCount; i++)
                {
                    Light light = GetAdditionalLight(i, IN.positionWS);
                    float ndotl = saturate(dot(normalWS, light.direction));
                    diffuse += baseColor * light.color * ndotl * light.distanceAttenuation * light.shadowAttenuation;
                }
                #endif

                float3 finalColor = ambient + diffuse;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}