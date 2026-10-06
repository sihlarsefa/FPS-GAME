// HAREKAT su yüzeyi: 3 yönlü Gerstner dalgası (köşe), çift kayan normal harita, fresnel, güneş parıltısı,
// PlanarReflection (varsa) + gökyüzü yansıması. Cull Off: sudan bakınca (FRONT_FACE) mavi-yeşil, bozulmuş "tavan" görünümü.
// Derinlik/opak dokusu KULLANMAZ (URP asset ayarından bağımsız); kıyı köpüğü ayrı şerit mesh ile gelir.
Shader "HAREKAT/Water/Surface"
{
    Properties
    {
        _BaseColor("Sığ renk", Color) = (0.12, 0.38, 0.40, 0.72)
        _DeepColor("Derin renk", Color) = (0.03, 0.14, 0.2, 0.92)
        _BumpMap("Normal A", 2D) = "bump" {}
        _BumpScale("Normal gücü", Float) = 0.9
        _DetailNormalMap("Normal B", 2D) = "bump" {}
        _DetailScale("Detay gücü", Float) = 0.6
        _WaveAmp("Dalga genliği (m)", Range(0, 0.6)) = 0.12
        _WaveSteep("Dalga dikliği", Range(0, 1)) = 0.35
        _Smoothness("Pürüzsüzlük", Range(0, 1)) = 0.9
        _ReflectStrength("Yansıma gücü", Range(0, 1)) = 0.85
        _WaveRot("Dalga yönü dönüşü (rad, rüzgâr)", Range(-1.2, 1.2)) = 0
        _GlitterStrength("Güneş parıltısı gücü", Range(0, 2)) = 0.8
        _FlowSpeed("Dere akış hızı (UV/sn)", Range(0, 0.2)) = 0.035
        _UnderColor("Su altı rengi", Color) = (0.05, 0.28, 0.32, 1)
        [HideInInspector] _Surface("__surface", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_DetailNormalMap);  SAMPLER(sampler_DetailNormalMap);
            TEXTURE2D(_PlanarReflectionTex); SAMPLER(sampler_PlanarReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DeepColor;
                half4 _UnderColor;
                float4 _BumpMap_ST;
                float4 _DetailNormalMap_ST;
                half _BumpScale;
                half _DetailScale;
                half _WaveAmp;
                half _WaveSteep;
                half _Smoothness;
                half _ReflectStrength;
                half _WaveRot;
                half _GlitterStrength;
                half _FlowSpeed;
            CBUFFER_END
            float4 _PlanarReflectionParams;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 flow : TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uvA : TEXCOORD1;
                float2 uvB : TEXCOORD2;
                half3 waveNormal : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float2 flow : TEXCOORD5;
            };

            // Gerstner: xy yön, z dalga boyu (m), w hız. Hafif, tek yönlü ağırlıklı (kıyı dalgası hissi).
            static const float4 kWaves[3] = {
                float4(0.86, 0.50, 17.0, 1.1),
                float4(0.55, -0.83, 9.0, 1.5),
                float4(-0.30, 0.95, 4.5, 1.9)
            };

            void GerstnerWaves(float3 p, float t, out float3 offset, out half3 normal)
            {
                offset = 0; float3 n = float3(0, 1, 0);
                [unroll] for (int i = 0; i < 3; i++)
                {
                    float2 d0 = normalize(kWaves[i].xy);
                    float rs, rc; sincos(_WaveRot, rs, rc); // rüzgâr yönü: tüm dalga yönleri birlikte döner
                    float2 d = float2(d0.x * rc - d0.y * rs, d0.x * rs + d0.y * rc);
                    float k = 6.2831853 / kWaves[i].z;
                    float a = _WaveAmp * (kWaves[i].z / 17.0) * 0.8;
                    float f = k * (dot(d, p.xz) - kWaves[i].w * t * 2.0);
                    float s = sin(f), c = cos(f);
                    float q = _WaveSteep / (k * a * 3.0 + 0.001);
                    q = min(q, 1.0);
                    offset.xz += d * (q * a * c);
                    offset.y += a * s;
                    n.xz -= d * (k * a * c);
                    n.y -= q * k * a * s;
                }
                normal = (half3)normalize(n);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                float3 off; half3 wn;
                GerstnerWaves(pw, _Time.y, off, wn);
                pw += off;
                o.positionWS = pw;
                o.positionCS = TransformWorldToHClip(pw);
                o.waveNormal = wn;
                // Dere akışı: UV2 = nehir yönü (göl/deniz 0); normal haritalar akış yönünde kayar.
                float2 fl = v.flow * (_FlowSpeed * _Time.y);
                o.flow = v.flow;
                o.uvA = pw.xz * _BumpMap_ST.xy + _BumpMap_ST.zw - fl;
                o.uvB = pw.xz * _DetailNormalMap_ST.xy + _DetailNormalMap_ST.zw - fl * 1.6;
                o.fogFactor = (half)ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                bool below = !IS_FRONT_VFACE(face, true, false);
                half3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uvA), _BumpScale);
                half3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, i.uvB), _DetailScale);
                half3 nT = half3(nA.xy + nB.xy, nA.z * nB.z);
                half3 wn = normalize(half3(i.waveNormal.x + nT.x, i.waveNormal.y, i.waveNormal.z + nT.y));

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));

                if (below)
                {
                    // Sudan yüzeye bakış: normal ters, bozulmuş gökyüzü ışığı + mavi-yeşil tavan, kritik açıda iç yansıma parlaması.
                    half3 bn = -wn;
                    half ndv = saturate(dot(bn, viewDir));
                    half3 sky = GlossyEnvironmentReflection(refract(-viewDir, bn, 0.75), i.positionWS, 0.35, 1.0, screenUV + wn.xz * 0.04);
                    half3 col = lerp(_UnderColor.rgb * 0.7, _UnderColor.rgb * 1.4 + sky * 0.45 * mainLight.color, ndv);
                    half alpha = lerp(0.95, 0.6, ndv);
                    col = MixFog(col, i.fogFactor);
                    return half4(col, alpha);
                }

                half ndv = saturate(dot(wn, viewDir));
                half fres = pow(1.0 - ndv, 4.0);
                fres = saturate(0.04 + 0.96 * fres) * _ReflectStrength;
                half3 body = lerp(_DeepColor.rgb, _BaseColor.rgb, ndv * ndv);
                body *= 0.45 + 0.55 * saturate(dot(mainLight.color, half3(0.33, 0.33, 0.33)));

                float3 refl = reflect(-viewDir, wn);
                half3 env = GlossyEnvironmentReflection(refl, i.positionWS, (half)(1.0 - _Smoothness), 1.0, screenUV);
                if (_PlanarReflectionParams.x > 0.5)
                {
                    float2 uv = screenUV + wn.xz * _PlanarReflectionParams.w * 2.0;
                    half3 pr = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_PlanarReflectionTex, saturate(uv)).rgb;
                    env = lerp(env, pr, saturate(_PlanarReflectionParams.z));
                }

                half3 col = lerp(body, env, fres);
                // Güneş parıltısı (Blinn-Phong).
                float3 h = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(wn, h)), lerp(60.0, 900.0, _Smoothness)) * (0.6 + 3.0 * _Smoothness);
                col += mainLight.color * spec * mainLight.shadowAttenuation;

                // Alçak güneşte parıltı: görüş yönüne dik bileşeni kısılmış normal (uzamış yol) + kıpırtılı kıvılcım maskesi.
                half low = saturate((0.45 - mainLight.direction.y) / 0.4) * saturate(mainLight.direction.y * 12.0 + 0.6);
                if (_GlitterStrength > 0.001 && low > 0.001)
                {
                    float2 vh = normalize(viewDir.xz + 1e-4);
                    float al = dot(wn.xz, vh);
                    float2 cr = wn.xz - al * vh;
                    half3 an = normalize(half3(vh * al + cr * 0.35, wn.y));
                    half gs = pow(saturate(dot(an, h)), 140.0);
                    float2 cell = floor(i.positionWS.xz * 5.0);
                    float tq = floor(_Time.y * 6.0);
                    half sp = frac(sin(dot(cell + tq * 1.7, float2(12.9898, 78.233))) * 43758.5453);
                    half sparkle = lerp(0.55, 2.2, step(0.78, sp));
                    col += mainLight.color * gs * sparkle * low * _GlitterStrength * mainLight.shadowAttenuation;
                }

                half alpha = saturate(lerp(_BaseColor.a, _DeepColor.a, 1.0 - ndv) + fres * 0.4);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
