Shader "UI/MonitorStatic"
{
    Properties
    {
        _Intensity ("Base Alpha", Range(0,1)) = 0.25
        _FlickerSpeed ("Flicker Speed", Float) = 10
        _ScrollSpeedX ("Scroll Speed X", Float) = 0.25
        _ScrollSpeedY ("Scroll Speed Y", Float) = 0.125
        _NoiseScale ("Noise Scale", Float) = 256
        _Brightness ("Brightness", Range(0,2)) = 1
        _Contrast ("Contrast", Range(0,3)) = 1
        _Tint ("Tint", Color) = (1,1,1,1)

        // Standard UI support
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("UI Color", Color) = (1,1,1,1)

        // Stencil / UI mask support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _Tint;
            float4 _ClipRect;

            float _Intensity;
            float _FlickerSpeed;
            float _ScrollSpeedX;
            float _ScrollSpeedY;
            float _NoiseScale;
            float _Brightness;
            float _Contrast;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPosition = v.vertex;
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;

                // --- 1) Subtle animated grain ---
                float2 grainUV = floor(uv * _NoiseScale) / max(_NoiseScale, 1.0);
                float grainA = hash21(grainUV + floor(t * 60.0));
                float grainB = hash21(grainUV * 1.73 + floor(t * 47.0) + 13.1);
                float grain = lerp(grainA, grainB, 0.5);

                // Center around 0 so it perturbs brightness instead of becoming white snow
                grain = (grain - 0.5) * 0.18;   // smaller amplitude = less snowy

                // --- 2) Scanlines ---
                float scan = sin(uv.y * 900.0);
                scan = 0.5 + 0.5 * scan;
                scan = lerp(0.88, 1.0, scan);   // subtle dark horizontal lines

                // --- 3) Soft vertical banding / monitor unevenness ---
                float band = sin(uv.x * 9.0 + t * 0.4) * 0.5 + 0.5;
                band = lerp(0.92, 1.03, band);

                // --- 4) Occasional horizontal interference roll ---
                float rollY = frac(uv.y + t * 0.03);
                float rollBand = smoothstep(0.45, 0.5, rollY) - smoothstep(0.5, 0.55, rollY);
                float roll = 1.0 + rollBand * 0.08;

                // --- 5) Flicker ---
                float flicker = 0.96 + 0.04 * sin(t * _FlickerSpeed);

                // Final grayscale overlay
                float v = 0.5 + grain;
                v *= scan;
                v *= band;
                v *= roll;
                v *= _Brightness;
                v = (v - 0.5) * _Contrast + 0.5;
                v = saturate(v);

                fixed4 col;
                col.rgb = v.xxx * _Tint.rgb * i.color.rgb;

                // More readable alpha than before
                col.a = _Intensity * flicker * _Tint.a * i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}