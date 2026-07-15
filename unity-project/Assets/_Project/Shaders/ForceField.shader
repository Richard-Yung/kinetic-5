// ============================================================================
//  KINETICS 5 — Force Field (bouclier)
//  Pipeline : Universal Render Pipeline (URP) — Unity 6000.0 LTS
// ============================================================================
//  Bulle de bouclier VFX pour agents (VULCAN, ION X-S, TITAN M-8).
//  Effets :
//    • FRESNEL : bordure cyan intense, intérieur transparent.
//    • HEX GRID : motif hexagonal procédural (sans texture).
//    • IMPACT RIPPLE : onde concentrique depuis _ImpactPoint0..3.
//    • PULSE faible pour "respiration" énergétique.
//  Blend : Additive (SrcAlpha One) pour halo lumineux.
// ============================================================================
Shader "KINETICS5/VFX/Force Field"
{
    Properties
    {
        [Header(Base)]
        _BaseColor       ("Couleur base", Color)               = (0.102, 0.631, 0.808, 0.45)
        _EdgeColor       ("Couleur bord (Fresnel)", Color)      = (0.42, 0.96, 0.18, 1.0)

        [Header(Fresnel)]
        _FresnelPower    ("Puissance Fresnel", Range(0.5, 8))   = 3.0
        _FresnelIntensity("Intensité Fresnel", Range(0, 4))     = 1.5

        [Header(Hex Grid)]
        _HexScale        ("Échelle hex", Range(5, 80))          = 28.0
        _HexIntensity    ("Intensité hex", Range(0, 1))         = 0.55
        _HexScrollSpeed  ("Vitesse défilement hex", Range(0, 2)) = 0.4

        [Header(Impact Ripples)]
        _RippleIntensity ("Intensité ripple", Range(0, 3))      = 1.5
        _RippleSpeed     ("Vitesse ripple", Range(0.5, 8))      = 3.0
        _RippleWidth     ("Largeur onde", Range(0.02, 0.3))     = 0.12
        _ImpactPoint0    ("Impact 0 (xyz)", Vector)             = (0, 0, 0, 0)
        _ImpactPoint1    ("Impact 1 (xyz)", Vector)             = (0, 0, 0, 0)
        _ImpactPoint2    ("Impact 2 (xyz)", Vector)             = (0, 0, 0, 0)
        _ImpactPoint3    ("Impact 3 (xyz)", Vector)             = (0, 0, 0, 0)
        _ImpactStart0    ("Départ impact 0", Float)             = -100
        _ImpactStart1    ("Départ impact 1", Float)             = -100
        _ImpactStart2    ("Départ impact 2", Float)             = -100
        _ImpactStart3    ("Départ impact 3", Float)             = -100

        [Header(Pulse)]
        _PulseSpeed      ("Vitesse pulse", Range(0, 8))         = 1.5
        _PulseAmount     ("Amplitude pulse", Range(0, 0.5))     = 0.15

        [Header(Alpha)]
        _OverallAlpha    ("Alpha global", Range(0, 1))          = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+10"
            "IgnoreProjector" = "True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "ForceField"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half  _FresnelPower;
                half  _FresnelIntensity;
                half  _HexScale;
                half  _HexIntensity;
                half  _HexScrollSpeed;
                half  _RippleIntensity;
                half  _RippleSpeed;
                half  _RippleWidth;
                float4 _ImpactPoint0;
                float4 _ImpactPoint1;
                float4 _ImpactPoint2;
                float4 _ImpactPoint3;
                float  _ImpactStart0;
                float  _ImpactStart1;
                float  _ImpactStart2;
                float  _ImpactStart3;
                half  _PulseSpeed;
                half  _PulseAmount;
                half  _OverallAlpha;
            CBUFFER_END

            // --- Motif hexagonal procédural ---
            // Retourne distance au centre du hex le plus proche + id cellule.
            float HexPattern(float2 uv, float scale, out float2 cellId)
            {
                uv *= scale;
                float2 s = float2(1.0, 1.7320508);           // ratio hex (sqrt(3))
                float2 hC = float2(0.5 * s.x, 0.5 * s.y);
                float2 a = fmod(uv, s) - hC;
                float2 b = fmod(uv + hC, s) - hC;
                float2 g = dot(a, a) < dot(b, b) ? a : b;
                cellId = uv - g;
                return 0.5 - 0.5 * (abs(g.x) + abs(g.y) * 1.1547005);
            }

            // Une seule onde concentrique (distance, rayon arrivé).
            half Ripple(float3 worldPos, float3 impact, float startTime, float t)
            {
                float dist = distance(worldPos, impact);
                float radius = (t - startTime) * _RippleSpeed;
                half  fall = saturate(1.0 - (t - startTime) / 1.5);          // fade 1.5s
                half  band = smoothstep(_RippleWidth, 0.0, abs(dist - radius));
                return band * fall * step(0.0, t - startTime);
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
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                half  NdotV = saturate(dot(N, V));

                // Fresnel (Schlick simplifié).
                half fres = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;
                half3 col = _BaseColor.rgb + _EdgeColor.rgb * fres;

                // Hex grid qui se déplace dans le temps.
                float2 hexUV = IN.uv + float2(0, _Time.y * _HexScrollSpeed);
                float2 cellId;
                float hex = HexPattern(hexUV, _HexScale, cellId);
                half hexLine = smoothstep(0.93, 1.0, hex);
                col += _EdgeColor.rgb * hexLine * _HexIntensity;

                // Ripples d'impact (jusqu'à 4 simultanés).
                float t = _Time.y;
                half rip = 0;
                rip += Ripple(IN.positionWS, _ImpactPoint0.xyz, _ImpactStart0, t);
                rip += Ripple(IN.positionWS, _ImpactPoint1.xyz, _ImpactStart1, t);
                rip += Ripple(IN.positionWS, _ImpactPoint2.xyz, _ImpactStart2, t);
                rip += Ripple(IN.positionWS, _ImpactPoint3.xyz, _ImpactStart3, t);
                col += _EdgeColor.rgb * rip * _RippleIntensity;

                // Pulse global.
                half pulse = 1.0 + _PulseAmount * sin(t * _PulseSpeed * 6.2831853);

                // Alpha : base + fresnel + ripples.
                half alpha = (_BaseColor.a + fres * 0.8 + rip * 0.6) * pulse * _OverallAlpha;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
