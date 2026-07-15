// ============================================================================
//  KINETICS 5 — Bloom Pre-Pass (Downsample + Threshold)
//  Pipeline : Universal Render Pipeline (URP) — Unity 6000.0 LTS
//  Passe de préparation pour le bloom sélectif : on extrait les pixels dont la
//  luminance dépasse _BloomThreshold, on désature légèrement, on floute
//  approximativement (noyau 9-tap). Résultat consommé par K5PostProcess.shader.
// ============================================================================
Shader "Hidden/KINETICS5/BloomPrePass"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "K5BloomPre"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half _BloomThreshold;
                half _BloomSoftKnee;
                half _BloomIntensity;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half Luminance(half3 c) { return dot(c, half3(0.2126, 0.7152, 0.0722)); }

            half4 frag (Varyings IN) : SV_Target
            {
                // 9-tap box blur (downsample ×2 implicite via TexelSize).
                float2 ts = _MainTex_TexelSize.xy * 1.5;
                half3 sum = 0;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x, -ts.y)).rgb;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0.0,  -ts.y)).rgb;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x, -ts.y)).rgb;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x,  0.0 )).rgb;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb * 2.0;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x,  0.0 )).rgb;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-ts.x,  ts.y)).rgb;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0.0,   ts.y)).rgb;
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( ts.x,  ts.y)).rgb;
                sum *= 0.1;

                // Threshold soft knee.
                half lum = Luminance(sum);
                half soft = smoothstep(_BloomThreshold - _BloomSoftKnee, _BloomThreshold + _BloomSoftKnee, lum);
                half3 bright = sum * soft;

                return half4(bright * _BloomIntensity, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
