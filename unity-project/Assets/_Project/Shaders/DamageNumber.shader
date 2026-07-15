// ============================================================================
//  KINETICS 5 — Damage Number (billboard text + outline + glow)
//  Pipeline : Universal Render Pipeline (URP) — Unity 6000.0 LTS
// ============================================================================
//  Affichage des nombres de dégâts flottants au-dessus des ennemis.
//  Le billboard est réalisé côté CPU (Quad camera-facing) ou via ce shader
//  qui tourne les UV selon la matrice de vue (optionnel, désactivé ici pour
//  rester compatible avec TextMeshPro).
//
//  Effets :
//    • OUTLINE : dilatation alpha + lerp couleur.
//    • GLOW : blur approximé par second échantillonnage à distance.
//    • COLOR BY ELEMENT : Kinetic=blanc, Energy=cyan, Cryo=bleu, Volt=jaune,
//      Explosive=orange. Piloté par _ElementId (0..4).
//    • FLOAT ANIMATION : ondulation Y sinusoïdale (debug — supprimée car le
//      mouvement est géré par Transform).
// ============================================================================
Shader "KINETICS5/UI/Damage Number"
{
    Properties
    {
        [Header(Texture)]
        _MainTex         ("Atlas nombres (R=masque)", 2D)      = "white" {}
        _MainColor       ("Couleur texte", Color)               = (1, 1, 1, 1)

        [Header(Outline)]
        _OutlineColor    ("Couleur outline", Color)             = (0.02, 0.024, 0.05, 1)
        _OutlineWidth    ("Largeur outline (UV)", Range(0, 0.05))= 0.008

        [Header(Glow)]
        _GlowColor       ("Couleur glow", Color)                 = (0.102, 0.631, 0.808, 1)
        _GlowIntensity   ("Intensité glow", Range(0, 4))         = 1.5
        _GlowSpread      ("Étalement glow (UV)", Range(0.002, 0.05)) = 0.012

        [Header(Element Color)]
        _ElementId       ("Élément (0=Kin 1=Ene 2=Cryo 3=Volt 4=Expl)", Range(0, 4)) = 0
        _KineticColor    ("Cinétique", Color)                   = (1.0, 1.0, 1.0, 1.0)
        _EnergyColor     ("Énergie", Color)                     = (0.10, 0.78, 0.93, 1.0)
        _CryoColor       ("Cryo", Color)                        = (0.40, 0.78, 1.0, 1.0)
        _VoltColor       ("Volt", Color)                        = (1.0, 0.91, 0.21, 1.0)
        _ExplosiveColor  ("Explosif", Color)                    = (1.0, 0.42, 0.13, 1.0)

        [Header(Billboard)]
        [Toggle] _Billboard ("Billboard camera", Float)         = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+15"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DamageNumber"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _BILLBOARD_ON

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
                half4  _OutlineColor;
                half   _OutlineWidth;
                half4  _GlowColor;
                half   _GlowIntensity;
                half   _GlowSpread;
                int    _ElementId;
                half4  _KineticColor;
                half4  _EnergyColor;
                half4  _CryoColor;
                half4  _VoltColor;
                half4  _ExplosiveColor;
                half   _Billboard;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            half4 PickElementColor(int id)
            {
                // Switch manuel (HLSL ne supporte pas toujours switch sur int non-constant).
                if (id == 1) return _EnergyColor;
                if (id == 2) return _CryoColor;
                if (id == 3) return _VoltColor;
                if (id == 4) return _ExplosiveColor;
                return _KineticColor;
            }

            Varyings vert (Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posOS = IN.positionOS.xyz;
#if defined(_BILLBOARD_ON)
                // Billboard : on aligne le quad avec la caméra (matrice view-rotation).
                float3 viewDir = UNITY_MATRIX_V._m03_m13_m23 - posOS; // approximatif
                float3 right   = UNITY_MATRIX_V._m00_m10_m20;
                float3 up      = UNITY_MATRIX_V._m01_m11_m21;
                posOS = right * IN.positionOS.x + up * IN.positionOS.y;
#endif
                OUT.positionCS = TransformObjectToHClip(posOS);
                OUT.uv   = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 main  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half  mask  = main.r;

                // Outline : 4 échantillons autour.
                half2 o = half2(_OutlineWidth, _OutlineWidth);
                half outlineMask = 0;
                outlineMask = max(outlineMask, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2( o.x, 0.0)).r);
                outlineMask = max(outlineMask, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2(-o.x, 0.0)).r);
                outlineMask = max(outlineMask, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2( 0.0,  o.y)).r);
                outlineMask = max(outlineMask, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2( 0.0, -o.y)).r);
                outlineMask = saturate(outlineMask - mask); // seulement le contour

                // Glow : 8 échantillons plus loin.
                half2 g = half2(_GlowSpread, _GlowSpread);
                half glowMask = 0;
                glowMask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2( g.x, 0.0)).r;
                glowMask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2(-g.x, 0.0)).r;
                glowMask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2( 0.0,  g.y)).r;
                glowMask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2( 0.0, -g.y)).r;
                glowMask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2( g.x,  g.y)).r;
                glowMask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2(-g.x,  g.y)).r;
                glowMask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2( g.x, -g.y)).r;
                glowMask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + half2(-g.x, -g.y)).r;
                glowMask = saturate(glowMask * 0.125 - mask);

                // Couleur selon élément + vertex color (vie/distance).
                half4 elem = PickElementColor(_ElementId);
                half3 rgb = _MainColor.rgb * elem.rgb * IN.color.rgb;

                // Composite : outline sous le texte, glow au-dessus.
                half3 finalRgb = lerp(_OutlineColor.rgb, rgb, mask);
                finalRgb += _GlowColor.rgb * glowMask * _GlowIntensity;

                half alpha = max(mask, max(outlineMask, glowMask * 0.5)) * IN.color.a;
                return half4(finalRgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
