// ============================================================================
//  KINETICS 5 — Holo UI (panneaux holographiques sci-fi)
//  Pipeline : Universal Render Pipeline (URP) — Unity 6000.0 LTS
// ============================================================================
//  Shader pour panneaux holographiques (HUD spatial, menus PA, écrans bord).
//  Effets combinés :
//    • SCANLINES : sin vertical, fréquence ajustable, intensité pulsée.
//    • GLITCH : déplacement horizontal aléatoire par blocs (t = floor(time*N)).
//    • RGB SPLIT : décalage des canaux R/B selon intensité glitch.
//    • ALPHA PULSE : sin(t * speed) pour effet "respiration" holographique.
//    • Vignette interne (bords plus sombres).
//  Utilisation : Material UI avec Texture=panel, blend SrcAlpha OneMinusSrcAlpha.
// ============================================================================
Shader "KINETICS5/UI/Holo UI"
{
    Properties
    {
        [Header(Texture)]
        _MainTex         ("Texture panel (RGBA)", 2D)            = "white" {}
        _MainColor       ("Teinte", Color)                        = (0.102, 0.631, 0.808, 1.0)

        [Header(Scanlines)]
        _ScanlineFreq    ("Fréquence scanlines", Range(20, 800)) = 220
        _ScanlineIntensity ("Intensité scanlines", Range(0, 1))  = 0.45
        _ScrollSpeed     ("Vitesse défilement", Range(-5, 5))    = 1.2

        [Header(Glitch)]
        _GlitchIntensity ("Intensité glitch", Range(0, 0.05))    = 0.008
        _GlitchFrequency ("Fréquence glitch (Hz)", Range(0, 30)) = 6.0
        _GlitchSeed      ("Graine glitch", Range(0, 100))        = 7.0

        [Header(RGB Split)]
        _RGBSplitAmount  ("Quantité RGB split", Range(0, 0.02))  = 0.0035

        [Header(Pulse)]
        _PulseSpeed      ("Vitesse pulse alpha", Range(0, 10))   = 2.0
        _PulseAmount     ("Amplitude pulse", Range(0, 1))        = 0.25

        [Header(Vignette)]
        _VignettePower   ("Puissance vignette", Range(0.5, 6))   = 2.2
        _VignetteColor   ("Couleur vignette", Color)             = (0.02, 0.024, 0.05, 1.0)

        [Header(Stencil)]
        _StencilComp     ("Stencil Comparison", Float)           = 8
        _Stencil         ("Stencil ID", Float)                   = 0
        _StencilOp       ("Stencil Operation", Float)            = 0
        _StencilWriteMask("Stencil Write Mask", Float)           = 255
        _StencilReadMask ("Stencil Read Mask", Float)            = 255
        _ColorMask       ("Color Mask", Float)                   = 15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"    = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            Name "HoloUI"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _MainColor;
                half   _ScanlineFreq;
                half   _ScanlineIntensity;
                half   _ScrollSpeed;
                half   _GlitchIntensity;
                half   _GlitchFrequency;
                half   _GlitchSeed;
                half   _RGBSplitAmount;
                half   _PulseSpeed;
                half   _PulseAmount;
                half   _VignettePower;
                half4  _VignetteColor;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            // PRNG déterministe (hash → [0,1]).
            float hash(float n) { return frac(sin(n) * 43758.5453123); }

            Varyings vert (Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv   = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float2 uv = IN.uv;
                float  t  = _Time.y;

                // --- GLITCH : décalage horizontal par blocs ---
                float  blockY    = floor(uv.y * 30.0);
                float  glitchT   = floor(t * _GlitchFrequency + _GlitchSeed);
                float  blockRand = hash(blockY + glitchT);
                float  glitchOn  = step(0.65, blockRand); // ~35% des blocs touchés
                float  offset    = (blockRand - 0.5) * _GlitchIntensity * glitchOn * 8.0;
                uv.x += offset;

                // --- RGB SPLIT : échantillonnage décalé ---
                half4 cR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(_RGBSplitAmount, 0));
                half4 cG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 cB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(_RGBSplitAmount, 0));
                half3 rgb = half3(cR.r, cG.g, cB.b);
                half  a   = cG.a;

                // --- SCANLINES ---
                float scanY = uv.y * _ScanlineFreq + t * _ScrollSpeed;
                half  scan = 0.5 + 0.5 * cos(scanY * 6.2831853);
                half  scanMask = 1.0 - _ScanlineIntensity * (1.0 - scan);
                rgb *= scanMask;

                // --- PULSE ALPHA ---
                half pulse = 1.0 + _PulseAmount * sin(t * _PulseSpeed * 6.2831853);
                a *= pulse;

                // --- VIGNETTE INTERNE ---
                float2 d = uv - 0.5;
                half vig = saturate(1.0 - dot(d, d) * _VignettePower);
                rgb = lerp(_VignetteColor.rgb, rgb, vig);

                // Teinte holo + vertex color (UI).
                rgb *= _MainColor.rgb * IN.color.rgb;
                a   *= _MainColor.a * IN.color.a;

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
