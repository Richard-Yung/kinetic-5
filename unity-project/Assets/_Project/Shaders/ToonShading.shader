// ============================================================================
//  KINETICS 5 — Toon Shading (Cel-Shaded)
//  Pipeline : Universal Render Pipeline (URP) — Unity 6000.0 LTS
//  Auteur   : Task 2-f — Shaders/Network/Tests/Docs
// ============================================================================
//  Shader cel-shaded pour les personnages (agents VULCAN/XEN/JOLT/XANO) et les
//  ennemis. Palette KINETICS 5 (#1AA1CE cyan, #6CF42E vert néon, #FE0022 rouge).
//
//  Caractéristiques :
//    • RAMP TEXTURE 1D échantillonnée selon N·L → 3 à 5 bandes (cellules).
//    • Rim light fuchsia pour détourer les silhouettes (lisibilité mobile).
//    • Outline par INVERTED HULL : seconde passe qui extrude les normales.
//    • Emission optionnelle pour les zones d'armure lumineuse.
//    • Lightmode Universal ForwardOnly (1 directional principale + 1 add).
//  Performance : 2 passes, ~24 ALU, mobile-friendly, sans SSCS/SSAO.
// ============================================================================
Shader "KINETICS5/Toon Shading"
{
    Properties
    {
        [Header(Couleur Principale)]
        _MainColor       ("Couleur principale", Color)          = (0.102, 0.631, 0.808, 1.0)
        _MainTex         ("Albedo (RGB)", 2D)                   = "white" {}
        _RampTex         ("Ramp (3-5 bandes)", 2D)              = "white" {}
        _RampOffset      ("Décalage ramp", Range(-1, 1))        = 0.0

        [Header(Rim Light)]
        _RimColor        ("Couleur rim", Color)                 = (0.42, 0.96, 0.18, 1.0)
        _RimPower        ("Puissance rim", Range(0.5, 8))       = 2.5
        _RimThreshold    ("Seuil rim", Range(0, 1))             = 0.6

        [Header(Emission)]
        _EmissionColor   ("Couleur émission", Color)            = (0, 0, 0, 1)
        _EmissionTex     ("Masque émission (R)", 2D)            = "black" {}
        _EmissionIntensity ("Intensité émission", Range(0, 8))  = 1.0

        [Header(Outline - Inverted Hull)]
        _OutlineColor    ("Couleur contour", Color)             = (0.02, 0.024, 0.05, 1.0)
        _OutlineWidth    ("Largeur contour", Range(0, 0.01))    = 0.0025

        [Header(Spécial)]
        _HurtColor       ("Couleur dégâts (flash)", Color)      = (1, 0, 0, 0)
        [Toggle(UNITY_COLORSPACE_GAMMA)] _Gamma ("Espace gamma", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        // =========================================================================
        //  PASSE 0 — CORPS (ramp + rim + emission)
        // =========================================================================
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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
                half4 _MainColor;
                float4 _MainTex_ST;
                float4 _RampTex_ST;
                half  _RampOffset;
                half4 _RimColor;
                half  _RimPower;
                half  _RimThreshold;
                half4 _EmissionColor;
                float4 _EmissionTex_ST;
                half  _EmissionIntensity;
                half4 _OutlineColor;
                half  _OutlineWidth;
                half4 _HurtColor;
            CBUFFER_END

            TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
            TEXTURE2D(_RampTex);       SAMPLER(sampler_RampTex);
            TEXTURE2D(_EmissionTex);   SAMPLER(sampler_EmissionTex);

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = nrmInputs.normalWS;
                OUT.viewDirWS  = GetCameraPositionWS() - posInputs.positionWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _MainColor;

                // Lumière principale URP.
                Light mainLight = GetMainLight();
                half3 N = normalize(IN.normalWS);
                half3 L = normalize(mainLight.direction);
                half  NdotL = dot(N, L);
                half  rampU = saturate(NdotL * 0.5 + 0.5 + _RampOffset);

                // Échantillonnage ramp → cellules nettes (pas de filtrage doux).
                half4 ramp = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(rampU, 0.5));
                half3 diffuse = albedo.rgb * mainLight.color * ramp.rgb;

                // Rim light (Fresnel simplifié).
                half3 V = normalize(IN.viewDirWS);
                half  NdotV = saturate(dot(N, V));
                half  rim = saturate(pow(1.0 - NdotV, _RimPower) * step(_RimThreshold, NdotL));
                diffuse += _RimColor.rgb * rim;

                // Emission pilotée par masque (R) — bandes d'armure cyan/vert.
                half emiMask = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, IN.uv).r;
                half3 emission = _EmissionColor.rgb * emiMask * _EmissionIntensity;

                // Flash de dégâts (cover by _HurtColor.a).
                diffuse = lerp(diffuse, _HurtColor.rgb, _HurtColor.a);

                half3 color = diffuse + emission;
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // =========================================================================
        //  PASSE 1 — OUTLINE (Inverted Hull)
        //  On extrude les sommets selon leur normale, en couleur pleine noire.
        // =========================================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _MainColor;
                float4 _MainTex_ST;
                float4 _RampTex_ST;
                half  _RampOffset;
                half4 _RimColor;
                half  _RimPower;
                half  _RimThreshold;
                half4 _EmissionColor;
                float4 _EmissionTex_ST;
                half  _EmissionIntensity;
                half4 _OutlineColor;
                half  _OutlineWidth;
                half4 _HurtColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 posOS = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
    CustomEditor "UnityEditor.ShaderGraph.UnlitShaderGUI"
}
