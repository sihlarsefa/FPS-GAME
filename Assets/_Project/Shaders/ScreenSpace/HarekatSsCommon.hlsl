#ifndef HAREKAT_SS_COMMON_INCLUDED
#define HAREKAT_SS_COMMON_INCLUDED

// HAREKAT ekran-uzayı ortak yardımcıları (URP 17.6 ShaderLibrary). Kontakt gölge + SSR-lite paylaşır.
// Yalnız perspektif kameralar (C# tarafı ortografik kamerayı atlar).

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float4 _HkLowResTexel; // xy = 1/boyut, zw = boyut (yarı çözünürlük hedef)

float HkNoise(float2 pixel)
{
    // Interleaved gradient noise (uzamsal; zamansal titreme yok)
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

bool HkIsFarDepth(float d)
{
#if UNITY_REVERSED_Z
    return d <= 1e-6;
#else
    return d >= 1.0 - 1e-6;
#endif
}

float3 HkWorldFromDepth(float2 uv, float rawDepth)
{
#if !UNITY_REVERSED_Z
    rawDepth = rawDepth * 2.0 - 1.0;
#endif
    return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
}

// Dünya konumunu ekran uv'sine yansıtır; dönüş: klip w (= göz derinliği, ön tarafta pozitif).
float HkProject(float3 posWS, out float2 uv)
{
    float4 cs = mul(UNITY_MATRIX_VP, float4(posWS, 1.0));
    float2 ndc = cs.xy / cs.w;
#if UNITY_UV_STARTS_AT_TOP
    ndc.y = -ndc.y;
#endif
    uv = ndc * 0.5 + 0.5;
    return cs.w;
}

float HkEyeDepthAt(float2 uv, out bool isSky)
{
    float d = SampleSceneDepth(uv);
    isSky = HkIsFarDepth(d);
    return isSky ? 1e9 : LinearEyeDepth(d, _ZBufferParams);
}

// Derinlik duyarlı 2x2 yukarı örnekleme ağırlığı (yarı çözünürlük örnek derinliğine göre).
float HkDepthWeight(float centerEye, float sampleEye)
{
    float tol = 0.05 + centerEye * 0.02;
    float x = abs(centerEye - sampleEye) / tol;
    return rcp(1.0 + x * x * 8.0);
}

#endif
