// ============================================================================
//  KINETICS 5 — Post-Processing (Bloom sélectif + CA + Vignette + Grain + LUT)
//  Pipeline : Universal Render Pipeline (URP) — Unity 6000.0 LTS
// ============================================================================
//  Composite post-FX pour le rendu KINETICS 5 :
//    • SELECTIVE BLOOM (sur pixels > _BloomThreshold — i.e. émissifs).
//    • CHROMATIC ABERRATION subtile (désactivable).
//    • VIGNETTE radial.
//    • FILM GRAIN animé (POISSON hash, pas de texture).
//    • LUT 32x32x32 color grading (cyan-teal shadows / orange highlights).
//  Mode d'emploi :
//    1. Ajouter le ScriptableRendererFeature K5PostProcessFeature au URP Asset
//       (Universal Renderer Data → Renderer Features → Add).
//    2. Configurer une LUT 32x32x32 (texture, Filtering=Point, Compression=None).
// ============================================================================
Shader "Hidden/KINETICS5/PostProcess"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "K5Composite"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _K5_BLOOM _K5_BLOOM_SOFT
            #pragma multi_compile_local _ _K5_CA
            #pragma multi_compile_local _ _K5_VIGNETTE
            #pragma multi_compile_local _ _K5_GRAIN
            #pragma multi_compile_local _ _K5_LUT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
            TEXTURE2D(_BloomTex);      SAMPLER(sampler_BloomTex);
            TEXTURE2D(_LutTex);        SAMPLER(sampler_LutTex);

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half  _BloomThreshold;
                half  _BloomIntensity;
                half  _BloomSoftKnee;
                half  _CAAmount;
                half4 _VignetteColor;
                half  _VignettePower;
                half  _VignetteIntensity;
                half  _GrainIntensity;
                half  _GrainSpeed;
                half  _LutContribution;
                float _LutSize;        // 32 par convention
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // PRNG type Poisson disk pour grain stable spatialement.
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // Sample LUT 32x32x32 standard (slices horizontales).
            half3 SampleLUT(half3 color)
            {
                color = saturate(color);
                float maxDim = _LutSize - 1.0;
                float slice = color.b * maxDim;
                float z0 = floor(slice);
                float z1 = min(z0 + 1.0, maxDim);
                half2 r0 = half2((color.r * maxDim + z0 + 0.5) / (_LutSize * _LutSize), (color.g * maxDim + 0.5) / _LutSize);
                half2 r1 = half2((color.r * maxDim + z1 + 0.5) / (_LutSize * _LutSize), (color.g * maxDim + 0.5) / _LutSize);
                half3 c0 = SAMPLE_TEXTURE2D(_LutTex, sampler_LutTex, r0).rgb;
                half3 c1 = SAMPLE_TEXTURE2D(_LutTex, sampler_LutTex, r1).rgb;
                return lerp(c0, c1, slice - z0);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                half4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

#if defined(_K5_CA)
                // Chromatic aberration : décalage radial R/B.
                float2 dir = uv - 0.5;
                float  r2  = dot(dir, dir);
                float2 ofs = dir * r2 * _CAAmount;
                src.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + ofs).r;
                src.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - ofs).b;
#endif

#if defined(_K5_BLOOM) || defined(_K5_BLOOM_SOFT)
                half3 bloom = SAMPLE_TEXTURE2D(_BloomTex, sampler_BloomTex, uv).rgb;
                bloom *= _BloomIntensity;
                // Soft knee optionnel (désaturé pour économie ALU).
                src.rgb += bloom;
#endif

#if defined(_K5_VIGNETTE)
                float2 d = uv - 0.5;
                half vig = saturate(1.0 - pow(dot(d, d) * 4.0, _VignettePower));
                src.rgb = lerp(_VignetteColor.rgb, src.rgb, lerp(1.0, vig, _VignetteIntensity));
#endif

#if defined(_K5_GRAIN)
                float2 gUv = uv * _MainTex_TexelSize.zw;
                float  n = Hash(gUv + _Time.y * _GrainSpeed);
                half  grain = (n - 0.5) * _GrainIntensity;
                src.rgb += grain;
#endif

#if defined(_K5_LUT)
                half3 graded = SampleLUT(src.rgb);
                src.rgb = lerp(src.rgb, graded, _LutContribution);
#endif

                return src;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
