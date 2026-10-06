// HAREKAT SSR-lite: hi-Z'siz doğrusal ekran-uzayı ışın yürüyüşü (derinlik + normal). Yansıma probu sahne renginde zaten var;
// bu geçiş yalnız isabet olduğunda, Fresnel/pürüzsüzlük/kenar/mesafe ağırlıklı EK katkı verir (isabet yoksa 0 => probe'a geri düşer).
// Geçiş 0: yarı çözünürlük RGBA16F (rgb = yansıyan renk, a = ağırlık). Geçiş 1: tam çözünürlük, derinlik duyarlı yukarı örnekleme, toplamsal karışım.
Shader "HAREKAT/ScreenSpace/SSRLite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "SsrMarch"
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragMarch

            #include "Assets/_Project/Shaders/ScreenSpace/HarekatSsCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _HkSsrParams;  // x azami mesafe (m), y azami adım, z kalınlık mutlak (m), w kalınlık göreli
            float4 _HkSsrParams2; // x kenar sönümü, y güç, z ıslaklık, w pürüzsüzlük alfada mı (1/0)
            float4 _HkSsrParams3; // x pürüzsüzlük cutoff, y sönüm başlangıcı, z F0, w inceltme adımı
            float4 _HkSsrParams4; // x adım (piksel)

            float HkRejectDepthAt(float2 uv, out float eyeDepth)
            {
                float sd = SampleSceneDepth(uv);
                eyeDepth = HkIsFarDepth(sd) ? 1e9 : LinearEyeDepth(sd, _ZBufferParams);
                return sd;
            }

            half4 FragMarch(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float d = SampleSceneDepth(uv);
                if (HkIsFarDepth(d))
                    return 0;

                // Normal + pürüzsüzlük (alfa: URP 17.6 _WRITE_SMOOTHNESS) veya ıslaklık sezgiseli
                float3 N = SampleSceneNormals(uv);
                float lenN = dot(N, N);
                if (lenN < 0.25)
                    return 0;
                N = N * rsqrt(lenN);
                float smoothA = 0.0;
                if (_HkSsrParams2.w > 0.5)
                    smoothA = LOAD_TEXTURE2D_X(_CameraNormalsTexture, uint2(uv * _ScreenSize.xy)).a;
                float wet = saturate(_HkSsrParams2.z) * smoothstep(0.6, 0.95, N.y);
                float smoothness = max(smoothA, wet);
                float sw = smoothstep(_HkSsrParams3.x, max(_HkSsrParams3.y, _HkSsrParams3.x + 1e-3), smoothness);
                if (sw <= 0.0)
                    return 0;

                float eye = LinearEyeDepth(d, _ZBufferParams);
                float maxDist = _HkSsrParams.x;
                float3 P = HkWorldFromDepth(uv, d);
                float3 V = normalize(P - _WorldSpaceCameraPos.xyz);
                float3 R = reflect(V, N);

                // Kameraya doğru giden yansıma yönlerinde görünür veri yok: sönümle (görünüm uzayı z; ileri = eksi).
                float rvz = mul((float3x3)UNITY_MATRIX_V, R).z;
                float camFade = 1.0 - smoothstep(-0.1, 0.25, rvz);
                if (camFade <= 0.0)
                    return 0;

                float3 P1 = P + R * maxDist;
                float2 uv0;
                float w0 = HkProject(P, uv0);
                float2 uv1;
                float w1 = HkProject(P1, uv1);
                if (w1 < 0.1)
                {
                    float tc = saturate((w0 - 0.1) / max(w0 - w1, 1e-4));
                    P1 = lerp(P, P1, tc);
                    w1 = HkProject(P1, uv1);
                }
                w0 = max(w0, 1e-3);
                w1 = max(w1, 1e-3);

                float k0 = rcp(w0);
                float k1 = rcp(w1);
                float distPx = length((uv1 - uv0) * _ScreenSize.xy);
                if (distPx < 1.0)
                    return 0;
                float maxSteps = max(1.0, _HkSsrParams.y);
                int steps = (int)clamp(ceil(distPx / max(0.5, _HkSsrParams4.x)), 1.0, maxSteps);

                float jit = HkNoise(input.positionCS.xy);
                float thickAbs = _HkSsrParams.z;
                float thickRel = _HkSsrParams.w;
                float biasAbs = 0.02;
                float biasRel = 0.002;

                bool hit = false;
                float tPrev = 0.0;
                float tHit = 0.0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float t = (i + 0.25 + 0.75 * jit) / steps;
                    float2 suv = lerp(uv0, uv1, t);
                    if (suv.x < 0.0 || suv.x > 1.0 || suv.y < 0.0 || suv.y > 1.0)
                        break;
                    float w = rcp(lerp(k0, k1, t));
                    float se;
                    HkRejectDepthAt(suv, se);
                    float diff = w - se; // > 0: ışın görünür yüzeyin arkasında
                    if (diff > biasAbs + biasRel * w && diff < thickAbs + thickRel * w)
                    {
                        hit = true;
                        tHit = t;
                        break;
                    }
                    tPrev = t;
                }

                if (!hit)
                    return 0;

                // İkili arama ile isabeti incelt.
                float lo = tPrev;
                float hi = tHit;
                int refine = (int)_HkSsrParams3.w;
                [loop]
                for (int j = 0; j < refine; j++)
                {
                    float mid = 0.5 * (lo + hi);
                    float mw = rcp(lerp(k0, k1, mid));
                    float mse;
                    HkRejectDepthAt(lerp(uv0, uv1, mid), mse);
                    if (mw - mse > biasAbs + biasRel * mw)
                        hi = mid;
                    else
                        lo = mid;
                }

                float2 huv = lerp(uv0, uv1, hi);

                // Arka yüze isabet reddi
                float3 Nh = SampleSceneNormals(huv);
                if (dot(Nh, R) > 0.15)
                    return 0;

                float hd = SampleSceneDepth(huv);
                if (HkIsFarDepth(hd))
                    return 0;
                float3 Ph = HkWorldFromDepth(huv, hd);
                float travel = distance(Ph, P);
                float x = saturate(travel / max(maxDist, 1e-3));
                float distFade = 1.0 - x * x;

                float2 e = min(huv, 1.0 - huv);
                float edge = smoothstep(0.0, max(_HkSsrParams2.x, 1e-3), min(e.x, e.y));

                float nov = saturate(dot(N, -V));
                float f0 = _HkSsrParams3.z;
                float fres = f0 + (1.0 - f0) * pow(1.0 - nov, 5.0);

                float weight = sw * sw * fres * distFade * edge * camFade * _HkSsrParams2.y;
                float3 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, huv, 0).rgb;
                col = col * rcp(1.0 + max(0.0, max(col.r, max(col.g, col.b))) * 0.0625); // HDR ateş böceği bastırma
                return half4(col, saturate(weight));
            }
            ENDHLSL
        }

        Pass
        {
            Name "SsrApply"
            Blend One One, Zero One // toplamsal; alfa korunur

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragApply

            #include "Assets/_Project/Shaders/ScreenSpace/HarekatSsCommon.hlsl"

            half4 FragApply(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float d = SampleSceneDepth(uv);
                if (HkIsFarDepth(d))
                    return 0;

                float eye = LinearEyeDepth(d, _ZBufferParams);
                float2 texel = _HkLowResTexel.xy;
                float3 acc = 0.0;
                float wsum = 0.0;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float2 o = float2((k & 1) ? 0.5 : -0.5, (k & 2) ? 0.5 : -0.5);
                    float2 tuv = uv + o * texel;
                    float sd = SampleSceneDepth(tuv);
                    float se = HkIsFarDepth(sd) ? 1e9 : LinearEyeDepth(sd, _ZBufferParams);
                    float w = HkDepthWeight(eye, se);
                    float4 s = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, tuv, 0);
                    acc += s.rgb * s.a * w; // önceden ağırlıklı: isabetsiz örnekler komşuya sızmaz
                    wsum += w;
                }
                return half4(acc / max(wsum, 1e-4), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
