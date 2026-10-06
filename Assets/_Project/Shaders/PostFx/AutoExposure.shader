// HAREKAT otomatik pozlama (göz uyumu). 4 geçiş:
//  0 Grid   : sahne rengi -> GxG ızgara, hücre başına 4x4 örnek log2 parlaklık ortalaması (R16F/R32F).
//  1 Reduce : ızgara -> 8x8 (merkez ağırlıklı log-ortalama).
//  2 Adapt  : 8x8 -> 1x1 EV (önceki kare EV'si _HkAePrev ile üstel uyum; hızlı parlaklaşma, yavaş kararma).
//  3 Apply  : sahne rengi *= 2^EV (Blend DstColor Zero; alfa korunur).
// Parlaklık ölçümü pozlama ÖNCESİ sahne renginden yapılır (açık döngü, geri besleme yok => kararlı).
Shader "HAREKAT/PostFx/AutoExposure"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZTest Always ZWrite Off Cull Off

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _HkAeParams;  // x log2(referans parlaklık), y güç (0..1), z min EV, w maks EV
        float4 _HkAeParams2; // x parlaklaşma hızı (1/sn), y kararma hızı (1/sn), z dt (sn), w 1 = ilk kare (yakala)
        float4 _HkAeGrid;    // x ızgara kenarı (piksel), y 1/ızgara, z, w
        TEXTURE2D(_HkAePrev);

        float HkAeCenterWeight(float2 uv)
        {
            float2 d = uv - 0.5;
            float r2 = dot(d, d) * 4.0;
            return 0.35 + 0.65 * exp(-r2 * 1.4);
        }

        float HkAeLuma(float3 c)
        {
            // Aşırı parlak (güneş diski, parlama) tek pikselin ortalamayı çökertmesini engelle.
            float l = dot(c, float3(0.2126, 0.7152, 0.0722));
            return clamp(l, 1e-4, 32.0);
        }
        ENDHLSL

        Pass
        {
            Name "AeGrid"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragGrid

            float4 FragGrid(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float cell = _HkAeGrid.y; // hücre = 1/G (uv uzayında)
                float acc = 0.0;
                [unroll]
                for (int y = 0; y < 4; y++)
                {
                    [unroll]
                    for (int x = 0; x < 4; x++)
                    {
                        float2 o = (float2(x, y) + 0.5) * 0.25 - 0.5; // -0.5..0.5 hücre içi
                        float2 suv = uv + o * cell;
                        float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, suv, 0).rgb;
                        acc += log2(HkAeLuma(c));
                    }
                }
                return float4(acc * (1.0 / 16.0), 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "AeReduce"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragReduce

            float4 FragReduce(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                // Kaynak ızgara (G x G) 8x8 bloklara bölünür: bu piksel 1/8 uv'lik bloğu ortalar.
                float g = _HkAeGrid.x;
                float blk = g * 0.125;         // blok kenarı (kaynak texel)
                float2 baseUv = floor(uv * 8.0) * 0.125; // blok sol-alt uv
                float acc = 0.0;
                float wsum = 0.0;
                int n = (int)blk;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float2 suv = baseUv + (float2(x, y) + 0.5) / g;
                        float v = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, suv, 0).r;
                        float w = HkAeCenterWeight(suv);
                        acc += v * w;
                        wsum += w;
                    }
                }
                // 2. kanal: toplam ağırlık (adapt geçişi ağırlıklı ortalamayı bununla yapar).
                return float4(acc / max(wsum, 1e-4), wsum, 0.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "AeAdapt"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAdapt

            float4 FragAdapt(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float acc = 0.0;
                float wsum = 0.0;
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        float2 q = LOAD_TEXTURE2D_X(_BlitTexture, uint2(x, y)).rg;
                        acc += q.x * q.y;
                        wsum += q.y;
                    }
                }
                float avgLog2 = acc / max(wsum, 1e-4);
                float targetEv = clamp((_HkAeParams.x - avgLog2) * saturate(_HkAeParams.y), _HkAeParams.z, _HkAeParams.w);

                float prev = LOAD_TEXTURE2D(_HkAePrev, uint2(0, 0)).r;
                float ev = targetEv;
                if (_HkAeParams2.w < 0.5)
                {
                    float speed = targetEv < prev ? _HkAeParams2.x : _HkAeParams2.y;
                    float k = 1.0 - exp(-max(speed, 0.0) * clamp(_HkAeParams2.z, 0.0, 0.1));
                    ev = prev + (targetEv - prev) * k;
                }
                return float4(ev, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "AeApply"
            Blend DstColor Zero, Zero One // rgb *= çıktı; alfa korunur

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragApply

            float4 FragApply(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float ev = LOAD_TEXTURE2D_X(_BlitTexture, uint2(0, 0)).r;
                float m = exp2(ev);
                return float4(m, m, m, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
