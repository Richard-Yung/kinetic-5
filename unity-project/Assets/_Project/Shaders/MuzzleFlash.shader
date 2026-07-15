// ============================================================================
//  KINETICS 5 — Muzzle Flash (particule additive)
//  Pipeline : Universal Render Pipeline (URP) — Unity 6000.0 LTS
// ============================================================================
//  Particule additive pour flash de tir (HEAVY RX-14, RIFLE CX-24, AX-9 SR...).
//  Caractéristiques :
//    • Blend Additive (SrcAlpha One) — glow lumineux.
//    • Soft edge radial (poudrage doux, pas de carré).
//    • Color tint par élément (Kinetic cyan, Energy cyan-blanc, Cryo bleu,
//      Volt jaune-vert, Explosive orange-rouge).
//    • Animation "shimmer" : déformation angulaire (rotation) + scintillement.
//  Compatible Particle System (Modules : Custom Vertex Streams → UV2/Color).
// ============================================================================
Shader "KINETICS5/VFX/Muzzle Flash"
{
    Properties
    {
        [Header(Texture)]
        _MainTex         ("Texture (R=masque)", 2D)            = "white" {}
        _TintColor       ("Teinte", Color)                      = (1.0, 0.85, 0.45, 1.0)

        [Header(Shape)]
        _SoftEdge        ("Bord doux", Range(0.1, 5))           = 1.5
        _RadialFalloff   ("Falloff radial", Range(0.2, 4))      = 2.0

        [Header(Animation)]
        _ShimmerSpeed    ("Vitesse scintillement", Range(0, 30)) = 12.0
        _ShimmerAmount   ("Amplitude scintillement", Range(0, 1)) = 0.35
        _RotationSpeed   ("Vitesse rotation", Range(-10, 10))   = 2.5

        [Header(Alpha)]
        _Intensity       ("Intensité globale", Range(0, 8))     = 2.5
        _VertexColorStrength ("Poids vertex color", Range(0, 1))= 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+5"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "MuzzleFlash"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _TintColor;
                half   _SoftEdge;
                half   _RadialFalloff;
                half   _ShimmerSpeed;
                half   _ShimmerAmount;
                half   _RotationSpeed;
                half   _Intensity;
                half   _VertexColorStrength;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

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
                // Rotation UV (autour du centre du sprite).
                float2 uv = IN.uv - 0.5;
                float  ang = _Time.y * _RotationSpeed;
                float  s = sin(ang), c = cos(ang);
                uv = mul(float2x2(c, -s, s, c), uv);
                uv += 0.5;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // Falloff radial centré.
                float2 d = IN.uv - 0.5;
                half radial = saturate(1.0 - pow(length(d) * 2.0, _RadialFalloff));
                half shape = pow(tex.r * radial, _SoftEdge);

                // Scintillement temporel (noir/blanc rapide).
                half shimmer = 1.0 + _ShimmerAmount * (sin(_Time.y * _ShimmerSpeed * 6.2831853) * 0.5 + 0.5 - 0.5);

                // Couleur = teinte × vertex color (vie/distance) × texture.
                half3 rgb = _TintColor.rgb * lerp(half3(1,1,1), IN.color.rgb, _VertexColorStrength);
                half  a   = shape * _TintColor.a * IN.color.a * _Intensity * shimmer;

                return half4(rgb * a, a); // premultiplied-friendly pour additive
            }
            ENDHLSL
        }
    }

    FallBack "Particles/Standard Unlit"
}
