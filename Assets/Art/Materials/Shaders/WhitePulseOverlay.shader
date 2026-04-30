Shader "FNaS/White Pulse Overlay Transparent" {
    Properties {
        _PulseColor ("Pulse Color", Color) = (1,1,1,1)
        _PulseStrength ("Pulse Strength", Range(0,1)) = 0
    }

    SubShader {
        Tags {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
        }

        Pass {
            Name "WhitePulseOverlay"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _PulseColor;
                float _PulseStrength;
            CBUFFER_END

            Varyings vert(Attributes IN) {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                float strength = saturate(_PulseStrength);

                // Softer curve: low values stay subtle longer.
                strength = strength * strength;

                // Cap opacity so it never becomes a full white wash.
                strength *= 0.35;

                return half4(_PulseColor.rgb, strength);
            }

            ENDHLSL
        }
    }
}