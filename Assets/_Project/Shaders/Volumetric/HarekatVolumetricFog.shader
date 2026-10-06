// HAREKAT hacimsel sis + ışık huzmesi (URP 17 / Unity 6). Üç geçiş, VolumetricFogPass (RenderGraph) tarafından çağrılır:
//   Pass 0 Raymarch  : çeyrek çözünürlük, derinlikten dünya konumu, yükseklik sisi + ana ışık gölge haritası, mavi gürültü jitter.
//   Pass 1 Temporal  : önceki kare yeniden izdüşümü + varyans sıkıştırma (clamp).
//   Pass 2 Composite : çift yönlü (derinlik duyarlı) üst örnekleme; kamera rengine  rgb + dst * a  ("One SrcAlpha") harmanı.
// Not: Bu dosya Unity dışında derlenemedi; Cursor doğrulaması gerekir (bkz. Docs/AAA_URP_20.md, "S2-volumetrik").
Shader "HAREKAT/Volumetric/Fog"
{
    Properties
    {
        [HideInInspector] _VolParams0("Params0 (density, falloff, baseH, g)", Vector) = (0.004, 0.06, 0, 0.7)
        [HideInInspector] _VolParams1("Params1 (sun, ambient, maxDist, steps)", Vector) = (1, 0.3, 200, 16)
        [HideInInspector] _VolTint("Tint", Vector) = (1, 1, 1, 1)
        [HideInInspector] _VolAmbient("Ambient", Vector) = (0.2, 0.2, 0.2, 1)
        [HideInInspector] _VolTarget("Low-res target size (w, h, 1/w, 1/h)", Vector) = (480, 270, 0.002, 0.0037)
        [HideInInspector] _VolJitter("Jitter (noiseOffset, halton, shadowOn, debug)", Vector) = (0, 0, 1, 0)
        [HideInInspector] _VolHistory("History (weight)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _VolNoiseTex("Blue noise", 2D) = "white" {}
        [HideInInspector] _VolRawTex("Raw", 2D) = "black" {}
        [HideInInspector] _VolHistTex("History", 2D) = "black" {}
        [HideInInspector] _VolSrcTex("Source", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        float4 _VolParams0;   // x yoğunluk, y yükseklik azalma, z taban yükseklik, w anizotropi g
        float4 _VolParams1;   // x güneş çarpanı, y ortam çarpanı, z azami mesafe, w adım sayısı
        float4 _VolTint;
        float4 _VolAmbient;
        float4 _VolTarget;    // düşük çözünürlük boyutu: w, h, 1/w, 1/h
        float4 _VolJitter;    // x gürültü kayması, y halton, z gölge açık, w hata ayıklama modu
        float4 _VolHistory;   // x geçmiş ağırlığı
        float4x4 _VolPrevVP;
        TEXTURE2D(_VolNoiseTex);
        TEXTURE2D(_VolRawTex);
        TEXTURE2D(_VolHistTex);
        TEXTURE2D(_VolSrcTex);

        struct Attributes { uint vertexID : SV_VertexID; };
        struct Varyings { float4 positionCS : SV_POSITION; };

        Varyings Vert(Attributes input)
        {
            Varyings o;
            o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            return o;
        }

        // Henyey-Greenstein, izotropik = 1 olacak biçimde (4π ile çarpılmış).
        float HenyeyGreenstein(float cosTheta, float g)
        {
            g = clamp(g, -0.99, 0.99);
            float denom = 1.0 + g * g - 2.0 * g * cosTheta;
            return (1.0 - g * g) * rsqrt(max(denom * denom * denom, 1e-4));
        }

        struct RayInfo
        {
            float3 origin;
            float3 dir;
            float dist;
        };

        // uv: tüm ekran 0..1 (doku yönelimi). Derinlikten dünya konumu; gökyüzünde azami mesafeye kadar.
        RayInfo MakeRay(float2 uv)
        {
            float rawDepth = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                bool sky = rawDepth <= 1e-6;
            #else
                bool sky = rawDepth >= 1.0 - 1e-6;
            #endif
            float3 posWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            RayInfo r;
            r.origin = GetCameraPositionWS();
            float3 v = posWS - r.origin;
            float len = length(v);
            r.dir = v / max(len, 1e-5);
            r.dist = sky ? _VolParams1.z : min(len, _VolParams1.z);
            return r;
        }
        ENDHLSL

        // ---------------------------------------------------------------- Pass 0: ışın yürütme
        Pass
        {
            Name "VolumetricRaymarch"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRaymarch
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float4 FragRaymarch(Varyings i) : SV_Target
            {
                float2 uv = i.positionCS.xy * _VolTarget.zw;      // positionCS.xy piksel merkezi (x + 0.5)
                uint2 pix = (uint2)i.positionCS.xy;
                RayInfo r = MakeRay(uv);

                int n = (int)_VolParams1.w;
                float noise = LOAD_TEXTURE2D(_VolNoiseTex, pix & 63u).r;
                noise = frac(noise + _VolJitter.x);

                float3 toLight = _MainLightPosition.xyz;
                float phase = min(HenyeyGreenstein(dot(r.dir, toLight), _VolParams0.w), 12.0);
                float3 sunCol = _MainLightColor.rgb * _VolParams1.x;
                float3 amb = _VolAmbient.rgb * _VolParams1.y;
                bool useShadow = _VolJitter.z > 0.5;

                float3 acc = 0;
                float T = 1.0;
                float prevD = 0.0;
                float shSum = 0.0;

                [loop]
                for (int s = 0; s < n; s++)
                {
                    float d1 = r.dist * pow((s + 1.0) / n, 1.5);
                    float dl = d1 - prevD;
                    float d = prevD + dl * noise;
                    float3 p = r.origin + r.dir * d;

                    float dens = _VolParams0.x * exp(-max(0.0, p.y - _VolParams0.z) * _VolParams0.y);
                    float stepT = exp(-dens * dl);

                    float sh = 1.0;
                    if (useShadow)
                        sh = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                    shSum += sh;

                    float3 L = (sunCol * phase * sh * 0.25 + amb) * _VolTint.rgb;   // birim sönümleme başına ışıma
                    acc += T * L * (1.0 - stepT);
                    T *= stepT;
                    prevD = d1;
                }

                if (_VolJitter.w > 1.5)   // hata ayıklama 2: gölge terimi
                    acc = (shSum / max(n, 1)) * (1.0 - T);

                return float4(acc, T);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- Pass 1: zamansal karışım
        Pass
        {
            Name "VolumetricTemporal"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragTemporal

            float4 FragTemporal(Varyings i) : SV_Target
            {
                int2 pix = (int2)i.positionCS.xy;
                int2 maxPix = (int2)_VolTarget.xy - 1;
                float2 uv = i.positionCS.xy * _VolTarget.zw;

                float4 cur = LOAD_TEXTURE2D(_VolRawTex, pix);

                // komşuluk momentleri (3x3) -> varyans sıkıştırma kutusu
                float4 m1 = 0, m2 = 0;
                [unroll]
                for (int dy = -1; dy <= 1; dy++)
                {
                    [unroll]
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        float4 c = LOAD_TEXTURE2D(_VolRawTex, clamp(pix + int2(dx, dy), int2(0, 0), maxPix));
                        m1 += c;
                        m2 += c * c;
                    }
                }
                float4 mu = m1 / 9.0;
                float4 sigma = sqrt(max(m2 / 9.0 - mu * mu, 0.0));
                float4 boxMin = mu - 1.5 * sigma;
                float4 boxMax = mu + 1.5 * sigma;

                // yeniden izdüşüm: ışının bitiş noktası önceki karede nereye düşüyordu
                RayInfo r = MakeRay(uv);
                float3 endWS = r.origin + r.dir * r.dist;
                float3 ndc = ComputeNormalizedDeviceCoordinatesWithZ(endWS, _VolPrevVP);
                float2 huv = ndc.xy;
                bool valid = all(huv > 0.002) && all(huv < 0.998) && ndc.z > 0.0 && ndc.z < 1.0;

                float4 hist = SAMPLE_TEXTURE2D_LOD(_VolHistTex, sampler_LinearClamp, huv, 0);
                hist = clamp(hist, min(boxMin, cur), max(boxMax, cur));

                float w = valid ? _VolHistory.x : 0.0;
                return lerp(cur, hist, w);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- Pass 2: üst örnekleme + harman
        Pass
        {
            Name "VolumetricComposite"
            Blend One SrcAlpha     // sonuç = inscatter + hedef * geçirgenlik

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            float4 FragComposite(Varyings i) : SV_Target
            {
                float2 uv = i.positionCS.xy * _ScreenSize.zw;
                float fullD = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);

                float2 lowSize = _VolTarget.xy;
                float2 p = uv * lowSize - 0.5;
                int2 baseT = (int2)floor(p);
                float2 f = p - baseT;
                int2 maxT = (int2)lowSize - 1;

                float4 sum = 0;
                float wsum = 0;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    int2 o = int2(k & 1, k >> 1);
                    int2 t = clamp(baseT + o, int2(0, 0), maxT);
                    float2 luv = (t + 0.5) * _VolTarget.zw;
                    float ld = LinearEyeDepth(SampleSceneDepth(luv), _ZBufferParams);
                    float wb = (o.x ? f.x : 1.0 - f.x) * (o.y ? f.y : 1.0 - f.y);
                    float dw = exp(-abs(ld - fullD) / (0.08 * fullD + 0.05));
                    float w = wb * dw + 1e-4;
                    sum += LOAD_TEXTURE2D(_VolSrcTex, t) * w;
                    wsum += w;
                }
                float4 v = sum / wsum;
                v.rgb = max(v.rgb, 0.0);
                v.a = saturate(v.a);
                return v;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
