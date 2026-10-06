// HAREKAT kontrast uyarlamalı keskinleştirme (AMD FidelityFX CAS mantığı, artı biçimli 5 örnek).
// Geçiş 0: CAS (kaynak -> geçici). Geçiş 1: düz kopya (geçici -> kamera rengi). Son işlem sonrası, TAA/STP bulanıklığını toparlar.
Shader "HAREKAT/PostFx/CasSharpen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZTest Always ZWrite Off Cull Off Blend Off

        Pass
        {
            Name "Cas"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCas

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _HkCasParams; // x keskinlik (0..1)

            half4 FragCas(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy;

                half4 center = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                half3 e = center.rgb;
                half3 b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(0.0, -texel.y), 0).rgb;
                half3 d = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(-texel.x, 0.0), 0).rgb;
                half3 f = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(texel.x, 0.0), 0).rgb;
                half3 h = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(0.0, texel.y), 0).rgb;

                // HDR/negatif değerlere karşı güvenli aralık.
                e = max(e, 0.0); b = max(b, 0.0); d = max(d, 0.0); f = max(f, 0.0); h = max(h, 0.0);

                half3 mn = min(min(min(d, e), min(f, b)), h);
                half3 mx = max(max(max(d, e), max(f, b)), h);

                // Yerel kontrast uyarlamalı kazanç: yüksek kontrastlı kenarda az, düz alanda çok keskinleştirir.
                half3 rcpM = rcp(max(mx, 1e-4h));
                half3 amp = saturate(min(mn, 2.0 - mx) * rcpM);
                amp = sqrt(amp);

                half sharp = (half)saturate(_HkCasParams.x);
                half peak = -rcp(lerp(8.0, 5.0, sharp));
                half3 w = amp * peak;
                half3 rcpW = rcp(1.0 + 4.0 * w);
                half3 outc = saturate((b + d + f + h) * w + e) * rcpW;

                // Sıfır güçte kaynağı aynen koru.
                outc = lerp(e, outc, step(0.001, sharp));
                return half4(outc, center.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Copy"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopy

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragCopy(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
