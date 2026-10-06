// HAREKAT kontakt gölgeleri: derinlik tamponunda güneş yönünde kısa ışın yürüyüşü.
// Geçiş 0: yarı çözünürlük R8 görünürlük (1 = aydınlık). Geçiş 1: tam çözünürlük, derinlik duyarlı yukarı örnekleme + gölge haritası maskesi, çarpımsal karışım.
Shader "HAREKAT/ScreenSpace/ContactShadows"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "ContactMarch"
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragMarch

            #include "Assets/_Project/Shaders/ScreenSpace/HarekatSsCommon.hlsl"

            float4 _HkContactParams;  // x ışın uzunluğu (m), y kalınlık (m), z güç, w azami mesafe (m)
            float4 _HkContactParams2; // x adım sayısı

            half4 FragMarch(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float d = SampleSceneDepth(uv);
                if (HkIsFarDepth(d))
                    return 1.0;

                float eye = LinearEyeDepth(d, _ZBufferParams);
                float maxDist = _HkContactParams.w;
                float distFade = 1.0 - smoothstep(maxDist * 0.6, maxDist, eye);
                if (distFade <= 0.0)
                    return 1.0;

                float3 P = HkWorldFromDepth(uv, d);
                float3 L = normalize(_MainLightPosition.xyz); // ışığa doğru yön
                float rayLen = _HkContactParams.x;
                float thick = _HkContactParams.y;
                int steps = max(1, (int)_HkContactParams2.x);

                float3 P1 = P + L * rayLen;
                float2 uv1;
                float w1 = HkProject(P1, uv1);
                float2 uv0;
                float w0 = HkProject(P, uv0);
                if (w1 < 0.05)
                {
                    // Işın kamera düzleminin arkasına çıkıyor: yakın düzleme kısalt.
                    float tc = saturate((w0 - 0.05) / max(w0 - w1, 1e-4));
                    P1 = lerp(P, P1, tc);
                }

                float jit = HkNoise(input.positionCS.xy);
                float bias = 0.012 + eye * 0.0016;
                float maxThick = thick + eye * 0.004;
                float occ = 0.0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float t = (i + 0.25 + 0.75 * jit) / steps;
                    float3 sp = lerp(P, P1, t);
                    float2 suv;
                    float sw = HkProject(sp, suv);
                    if (sw <= 0.0 || suv.x < 0.0 || suv.x > 1.0 || suv.y < 0.0 || suv.y > 1.0)
                        break;
                    float sd = SampleSceneDepth(suv);
                    if (HkIsFarDepth(sd))
                        continue;
                    float diff = sw - LinearEyeDepth(sd, _ZBufferParams); // > 0: ışın noktası görünür yüzeyin arkasında
                    if (diff > bias && diff < maxThick)
                    {
                        occ = 1.0 - t * 0.5;
                        break;
                    }
                }

                return (half)(1.0 - occ * distFade);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ContactApply"
            Blend DstColor Zero, Zero One // renk *= çarpan, alfa korunur

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragApply

            // Ana ışık gölge anahtarları küresel olarak URP tarafından açılır; yoksa maske 1 (kontakt gölge tam uygulanır).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // Shadows.hlsl için gerekli önceki include'lar (URP ScreenSpaceShadows ile aynı).
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
            #include "Assets/_Project/Shaders/ScreenSpace/HarekatSsCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float4 _HkContactParams;  // z = güç
            float4 _HkContactParams2;

            half4 FragApply(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float d = SampleSceneDepth(uv);
                if (HkIsFarDepth(d))
                    return half4(1, 1, 1, 1);

                float eye = LinearEyeDepth(d, _ZBufferParams);

                // Derinlik duyarlı 2x2 yukarı örnekleme (yarı çözünürlük R8 görünürlük).
                float2 texel = _HkLowResTexel.xy;
                float acc = 0.0;
                float wsum = 0.0;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float2 o = float2((k & 1) ? 0.5 : -0.5, (k & 2) ? 0.5 : -0.5);
                    float2 tuv = uv + o * texel;
                    float sd = SampleSceneDepth(tuv);
                    float se = HkIsFarDepth(sd) ? 1e9 : LinearEyeDepth(sd, _ZBufferParams);
                    float w = HkDepthWeight(eye, se);
                    acc += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, tuv, 0).r * w;
                    wsum += w;
                }
                float vis = acc / max(wsum, 1e-4);

                // Gölge haritasında zaten gölgede olan yerde (mask=0) etkisiz: çifte koyulaşma yok.
                float3 P = HkWorldFromDepth(uv, d);
                half mask = MainLightRealtimeShadow(TransformWorldToShadowCoord(P));

                float factor = lerp(1.0, vis, saturate(_HkContactParams.z) * mask);
                return half4(factor, factor, factor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
