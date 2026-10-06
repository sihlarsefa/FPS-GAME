// HAREKAT patlama katmanları (ExplosionFx): prosedürel ateş topu -> duman geçişi, ısı kırılması, toz halkası, toprak sütunu.
// Doku gerektirmez (değer gürültüsü fbm). _FxMode: 0 ateş/duman, 1 ısı kırılması (opak doku okur), 2 yer toz halkası, 3 toprak/toz sütunu.
// MPB: _FxParams (x yaş01, y ısı 1->0, z duman 0->1, w aşınma eşiği), _FxParams2 (x alfa, y tohum, z kırılma gücü, w yumuşak mesafe m),
// _FxParams3 (x gece 0..1, y yumuşak parçacık açık 1/0), _FxSunDir (güneşe doğru), _FxSunColor.
Shader "HAREKAT/Vfx/Fireball"
{
    Properties
    {
        _FxMode("Mod", Float) = 0.0
        _FxParams("Parametreler", Vector) = (0.0, 1.0, 0.0, 0.1)
        _FxParams2("Parametreler 2", Vector) = (1.0, 0.0, 0.0, 1.5)
        _FxParams3("Parametreler 3", Vector) = (0.0, 1.0, 0.0, 0.0)
        _FxSunDir("Güneş yönü", Vector) = (0.3, 0.8, 0.5, 0.0)
        _FxSunColor("Güneş rengi", Color) = (1.0, 0.95, 0.85, 1.0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "HarekatFireball"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _FxMode;
                float4 _FxParams;
                float4 _FxParams2;
                float4 _FxParams3;
                float4 _FxSunDir;
                float4 _FxSunColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += a * VNoise(p);
                    p = p * 2.03 + 17.0;
                    a *= 0.5;
                }
                return v;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float age = _FxParams.x;
                float heat = _FxParams.y;
                float smoke = _FxParams.z;
                float erode = _FxParams.w;
                float alphaEnv = _FxParams2.x;
                float seed = _FxParams2.y;
                float night = _FxParams3.x;

                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float2 q = p * 2.2 + seed * 13.7 + float2(0.0, -age * 0.9);
                float n = Fbm(q);

                float3 rgb;
                float alpha;
                int mode = (int)(_FxMode + 0.5);

                if (mode == 1)
                {
                    // Isı kırılması: normal kaydırmalı, ekran görüntüsünü bükerek geri yazar.
                    float edge = saturate(1.0 - r);
                    edge = edge * edge;
                    float2 w = float2(Fbm(q + float2(age * 2.5, 0.0)), Fbm(q + float2(7.3, age * 2.5))) - 0.5;
                    float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                    float2 offset = w * _FxParams2.z * edge * 0.12;
                    float3 scene = SampleSceneColor(saturate(screenUV + offset));
                    rgb = scene;
                    alpha = saturate(edge * alphaEnv * 2.0);
                    return float4(rgb, alpha);
                }

                if (mode == 2)
                {
                    // Yer toz halkası: genişleyen halka (r ~ 0.6..1), kenarı aşınmış.
                    float ringCenter = lerp(0.35, 0.85, saturate(age));
                    float ring = smoothstep(ringCenter - 0.28, ringCenter, r) * (1.0 - smoothstep(ringCenter, ringCenter + 0.14, r));
                    float dens = ring * (0.55 + n * 0.9);
                    alpha = smoothstep(erode, erode + 0.2, dens) * alphaEnv;
                    float lit = lerp(0.45, 1.0, saturate(dot(float3(0.0, 0.0, 1.0), _FxSunDir.xyz) * 0.5 + 0.5));
                    rgb = float3(0.46, 0.38, 0.29) * lit * _FxSunColor.rgb * lerp(1.0, 0.25, night);
                    rgb += float3(1.4, 0.55, 0.15) * heat * 0.35 * (1.0 - r);
                }
                else if (mode == 3)
                {
                    // Toprak sütunu: tabanı geniş, yukarı daralan, gürültüyle kıyılmış gövde.
                    float v = input.uv.y;
                    float halfWidth = lerp(0.62, 0.24, v) * (0.8 + n * 0.5);
                    float shape = 1.0 - smoothstep(halfWidth * 0.55, halfWidth, abs(p.x));
                    float dens = shape * (0.5 + n * 0.8) * (1.0 - v * 0.55);
                    alpha = smoothstep(erode, erode + 0.22, dens) * alphaEnv;
                    float lit = lerp(0.45, 1.0, saturate(dot(float3(p.x, 0.3, 0.7), _FxSunDir.xyz) * 0.5 + 0.5));
                    rgb = lerp(float3(0.30, 0.23, 0.17), float3(0.20, 0.17, 0.14), v) * lit * _FxSunColor.rgb * lerp(1.0, 0.25, night);
                    rgb += float3(1.6, 0.6, 0.15) * heat * 0.4 * (1.0 - v);
                }
                else
                {
                    // Ateş topu -> duman: yumuşak yuvarlak gövde, aşınma maskesi, turuncu -> siyah geçiş.
                    float body = saturate(1.0 - r);
                    float dens = n * 0.6 + body * 0.85;
                    alpha = smoothstep(erode, erode + 0.2, dens) * alphaEnv * (1.0 - smoothstep(0.7, 1.0, r));

                    float3 view0 = UNITY_MATRIX_V[0].xyz;
                    float3 view1 = UNITY_MATRIX_V[1].xyz;
                    float3 view2 = UNITY_MATRIX_V[2].xyz;
                    float3 nrm = normalize(view0 * p.x + view1 * p.y + view2 * sqrt(max(0.0, 1.0 - r * r)));
                    float lit = lerp(0.3, 1.0, saturate(dot(nrm, _FxSunDir.xyz) * 0.5 + 0.5));

                    float core = saturate(body * 1.4 - 0.2);
                    float3 fire = lerp(float3(1.6, 0.35, 0.06), float3(4.0, 1.8, 0.5), core * heat) * (0.55 + n * 0.9);
                    float3 soot = lerp(float3(0.16, 0.14, 0.12), float3(0.05, 0.045, 0.04), saturate(smoke * 1.2));
                    float3 smokeCol = soot * lit * _FxSunColor.rgb * lerp(1.0, 0.3, night);
                    smokeCol += float3(1.2, 0.45, 0.1) * heat * 0.2 * body;
                    rgb = lerp(fire * heat + smokeCol * (1.0 - heat), smokeCol, smoke);
                }

                if (_FxParams3.y > 0.5)
                {
                    float2 suv = input.positionCS.xy / _ScaledScreenParams.xy;
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                    float fade = saturate((sceneEye - input.positionCS.w) / max(_FxParams2.w, 0.05));
                    alpha *= fade;
                }

                return float4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
