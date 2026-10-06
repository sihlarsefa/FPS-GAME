#ifndef HAREKAT_WIND_COMMON_INCLUDED
#define HAREKAT_WIND_COMMON_INCLUDED

// HAREKAT ortak ruzgar + ezilme (trample) fonksiyonlari. Kucuk, bagimsiz; Core.hlsl'den sonra eklenir.
// Global degiskenler C# tarafindan WindSystem / GrassInteractors ile Shader.SetGlobal* ile yazilir.

float4 _HarekatWind;          // xyz: ruzgar yonu (y=0, normalize), w: guc (0..1.5)
float4 _HarekatWindParams;    // x: ruzgar patlamasi (gust 0..1), y: hiz carpani, z: turbulans (0..1), w: aktif (1/0)
float4 _HarekatInteractors[8];// xyz: dunya konumu, w: yaricap (0 = bos)
float  _HarekatInteractorCount;
float4 _HarekatGrassFade;     // x: cim cizim mesafesi (0 = kapali), y: solma serit genisligi (m)

// Genel dalga/gust faktoru (0..~1.5). Dunya konumuna gore buyuk olcekli dalgalar ruzgar yonunde ilerler.
float HarekatWindWave(float3 posWS)
{
    float t = _Time.y * (0.6 + _HarekatWindParams.y);
    float along = dot(posWS.xz, _HarekatWind.xz);
    float wave = sin(along * 0.35 - t) * 0.5 + 0.5;
    float gust = sin(along * 0.07 - t * 0.37 + 1.7) * 0.5 + 0.5;
    gust = gust * gust * _HarekatWindParams.x;
    return 0.25 + wave * 0.5 + gust;
}

// Cimen / yaprak egilmesi. bend: 0..1 (vertex color R). phase: 0..1 (vertex color G, kamis basina rastgele).
// Donus: dunya uzayinda konum kaymasi. height: yatay kayma yuksekligi telafisi icin nesne uzunlugu (m).
float3 HarekatWindBend(float3 posWS, float bend, float phase, float influence, float height)
{
    if (_HarekatWindParams.w < 0.5 || bend <= 0.0001)
        return float3(0, 0, 0);
    float strength = _HarekatWind.w * influence;
    float wave = HarekatWindWave(posWS);
    float2 dir = _HarekatWind.xz;
    float2 perp = float2(-dir.y, dir.x);
    float t = _Time.y * (1.5 + _HarekatWindParams.y * 3.0);
    float flutter = sin(t + phase * 6.2831 + posWS.x * 0.9 + posWS.z * 1.1) * _HarekatWindParams.z;
    float amp = strength * wave * bend * height * 0.35;
    float2 off = dir * amp + perp * flutter * strength * bend * height * 0.12;
    float drop = dot(off, off) / max(height, 0.05) * 0.5; // yay: yatay kayma kadar boy kisalir
    return float3(off.x, -drop, off.y);
}

// Govde sallanmasi (agac): nesne yukseklik orani ile ustel buyur, dusuk frekans.
float3 HarekatTrunkSway(float3 posWS, float3 pivotWS, float heightAbovePivot, float trunkSway, float influence)
{
    if (_HarekatWindParams.w < 0.5 || trunkSway <= 0.0001)
        return float3(0, 0, 0);
    float t = _Time.y * (0.35 + _HarekatWindParams.y * 0.5);
    float h = max(heightAbovePivot, 0.0);
    float k = h * h * 0.012 * trunkSway * _HarekatWind.w * influence; // h^2: tepe cok, taban az
    float slow = sin(t + dot(pivotWS.xz, _HarekatWind.xz) * 0.12) * 0.5 + 0.5;
    float gust = HarekatWindWave(pivotWS) * (0.6 + slow * 0.8);
    float2 dir = _HarekatWind.xz;
    float2 off = dir * k * gust + float2(-dir.y, dir.x) * sin(t * 0.7 + pivotWS.x) * k * 0.15;
    return float3(off.x, -dot(off, off) / max(h, 0.5) * 0.5, off.y);
}

// Oyuncu/arac ezilmesi: yakin etkilesimciden uzaga it + yatir. Donus: dunya uzayinda kayma.
float3 HarekatTrample(float3 posWS, float3 rootWS, float bend, float strength)
{
    float3 acc = float3(0, 0, 0);
    int n = (int)min(_HarekatInteractorCount, 8.0);
    [unroll] for (int i = 0; i < 8; i++)
    {
        if (i >= n) break;
        float4 it = _HarekatInteractors[i];
        if (it.w <= 0.001) continue;
        float2 d = rootWS.xz - it.xz;
        float dist = length(d);
        float f = saturate(1.0 - dist / it.w);
        f = f * f * (3.0 - 2.0 * f);                         // smoothstep
        float dy = saturate(1.0 - abs(rootWS.y - it.y) / 2.5);// yalniz yakin yukseklik
        float2 pushDir = d / max(dist, 0.001);
        acc.xz += pushDir * f * dy * it.w * 0.55;
        acc.y -= f * dy * 0.5;
    }
    return acc * bend * strength;
}

#endif
