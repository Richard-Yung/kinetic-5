// ============================================================================
//  KINETICS 5 — Ship Interior (surfaces métalliques sci-fi)
//  Pipeline : Universal Render Pipeline (URP) — Unity 6000.0 LTS
// ============================================================================
//  Shader PBR-lite pour niveaux vaisseaux (MV Tarnhelm, Hexgrid-9, etc.).
//  Caractéristiques :
//    • Albedo + Metallic + Smoothness (PBR simplifié, 1 seule directional).
//    • NORMAL GRID procédural : rainures en quadrillage (faux normal map).
//    • EMISSIVE PANELS : masque R = panneaux lumineux (cyan/vert selon _EmissionColor).
//    • FOG : brouillard URP (distance) + teinte ambiance scène (_AmbientColor).
//    • DIRT/WEAR : mask R2 = saleté qui assombrit localement.
//  Mobile-first : pas de PBR coûteux, juste N·L + spec Blinn-Phong + fog.
// ============================================================================
Shader "KINETICS5/Environment/Ship Interior"
{
    Properties
    {
        [Header(Albedo)]
        _MainTex         ("Albedo (RGB)", 2D)                   = "white" {}
        _MainColor       ("Teinte albedo", Color)               = (0.5, 0.5, 0.5, 1.0)
        _DirtTex         ("Saleté (R)", 2D)                      = "black" {}
        _DirtAmount      ("Quantité saleté", Range(0, 1))       = 0.4

        [Header(Metallic / Specular)]
        _MetallicTex     ("Metallic (R) Smoothness (A)", 2D)    = "white" {}
        _Metallic        ("Force metallic", Range(0, 1))        = 0.85
        _Smoothness      ("Lissage", Range(0, 1))               = 0.55
        _SpecColor       ("Couleur specular", Color)            = (1, 1, 1, 1)
        _SpecPower       ("Puissance specular", Range(8, 256))  = 64

        [Header(Normal Grid Procedural)]
        _GridScale       ("Échelle grille", Range(2, 80))       = 20
        _GridDepth       ("Profondeur rainures", Range(0, 1))   = 0.6
        _GridLineWidth   ("Largeur lignes", Range(0.01, 0.3))   = 0.08

        [Header(Emissive Panels)]
        _EmissionTex     ("Masque émission (R)", 2D)            = "black" {}
        _EmissionColor   ("Couleur panneaux", Color)            = (0.10, 0.78, 0.93, 1.0)
        _EmissionIntensity ("Intensité émission", Range(0, 8))  = 1.8
        _EmissionPulse   ("Pulse émission", Range(0, 1))        = 0.25

        [Header(Ambient / Fog)]
        _AmbientColor    ("Couleur ambiance", Color)            = (0.06, 0.08, 0.16, 1.0)
        _FogColor        ("Couleur brouillard", Color)          = (0.02, 0.024, 0.05, 1.0)
        _FogStart        ("Début brouillard", Float)            = 10.0
        _FogEnd          ("Fin brouillard", Float)              = 60.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _MainColor;
                float4 _DirtTex_ST;
                half   _DirtAmount;
                float4 _MetallicTex_ST;
                half   _Metallic;
                half   _Smoothness;
                half4  _SpecColor;
                half   _SpecPower;
                half   _GridScale;
                half   _GridDepth;
                half   _GridLineWidth;
                float4 _EmissionTex_ST;
                half4  _EmissionColor;
                half   _EmissionIntensity;
                half   _EmissionPulse;
                half4  _AmbientColor;
                half4  _FogColor;
                half   _FogStart;
                half   _FogEnd;
            CBUFFER_END

            TEXTURE2D(_MainTex);      SAMPLER(sampler_MainTex);
            TEXTURE2D(_DirtTex);      SAMPLER(sampler_DirtTex);
            TEXTURE2D(_MetallicTex);  SAMPLER(sampler_MetallicTex);
            TEXTURE2D(_EmissionTex);  SAMPLER(sampler_EmissionTex);

            // Calcule une normale procédurale pour un quadrillage de lignes.
            half3 ComputeGridNormal(float2 uv, float scale, float lineWidth, float depth)
            {
                uv *= scale;
                float2 g = abs(frac(uv) - 0.5);
                float gx = smoothstep(0.5 - lineWidth, 0.5, g.x);
                float gy = smoothstep(0.5 - lineWidth, 0.5, g.y);
                float gridMask = max(gx, gy);
                // Approxime la normale : sur les bords, on tire vers (dx, dy).
                half3 n = half3(lerp(0, g.x * 2.0 - 1.0, gridMask) * depth,
                                lerp(0, g.y * 2.0 - 1.0, gridMask) * depth,
                                1.0 - gridMask * depth * 0.5);
                return normalize(n);
            }

            Varyings vert (Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = n.normalWS;
                OUT.viewDirWS  = GetCameraPositionWS() - p.positionWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogFactor  = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 albedo   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _MainColor;
                half4 metalA   = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MetallicTex, IN.uv);
                half  metallic = metalA.r * _Metallic;
                half  smooth   = metalA.a * _Smoothness;

                half3 N = normalize(IN.normalWS);
                // Perturbe la normale avec le motif grille.
                half3 gridN = ComputeGridNormal(IN.uv, _GridScale, _GridLineWidth, _GridDepth);
                // Combine (tangent-space approximatif : on suppose N aligné avec up pour simplifier).
                half3 finalN = normalize(N + gridN * 0.35);

                Light mainLight = GetMainLight();
                half3 L = normalize(mainLight.direction);
                half  NdotL = max(0.0, dot(finalN, L));

                // Diffuse + ambient.
                half3 diffuse = albedo.rgb * mainLight.color * NdotL;
                diffuse += albedo.rgb * _AmbientColor.rgb;

                // Specular Blinn-Phong (métal).
                half3 V = normalize(IN.viewDirWS);
                half3 H = normalize(L + V);
                half spec = pow(max(0.0, dot(finalN, H)), _SpecPower) * smooth * metallic;
                diffuse += _SpecColor.rgb * spec * mainLight.color;

                // Saleté assombrit localement.
                half dirt = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, IN.uv).r * _DirtAmount;
                diffuse *= 1.0 - dirt * 0.5;

                // Panneaux emissifs (cyan / vert KINETICS).
                half emiMask = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, IN.uv).r;
                half pulse = 1.0 + _EmissionPulse * sin(_Time.y * 2.0 + IN.uv.x * 10.0);
                half3 emission = _EmissionColor.rgb * emiMask * _EmissionIntensity * pulse;

                half3 color = diffuse + emission;
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
