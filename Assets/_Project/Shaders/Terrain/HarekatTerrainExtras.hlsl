// HAREKAT arazi eklentileri: yamaç triplanar, mesafe makro varyasyonu, detay normal, ıslaklık/su birikintisi.
// Tüm ayarlar GLOBAL shader değişkenidir (TerrainShaderBinder yazar) -> taban ve Add Pass aynı değerleri görür.
// Kalite kademesine göre kapatma: ilgili güç değeri 0 ise dal tamamen atlanır (tek tip dal; anahtar kelime/varyant yok).
#ifndef HAREKAT_TERRAIN_EXTRAS_INCLUDED
#define HAREKAT_TERRAIN_EXTRAS_INCLUDED

#include "Assets/_Project/Shaders/Common/HarekatCommon.hlsl"

// x: yamaç gücü (0=kapalı), y: normal.y eşiği (altı yamaç), z: geçiş genişliği, w: doku karo boyu (m)
float4 _HarekatTerrainParams0;
// x: makro güç (0=kapalı), y: makro ölçeği (m), z: sönme başlangıcı (m), w: tam etki mesafesi (m)
float4 _HarekatTerrainParams1;
// x: detay normal gücü (0=kapalı), y: karo boyu (m), z: görünme mesafesi (m), w: yamaç pürüzsüzlüğü
float4 _HarekatTerrainParams2;
// x: su birikintisi ölçeği (m), y: kapsama 0-1, z: ıslak albedo çarpanı, w: ıslak/birikinti pürüzsüzlüğü
float4 _HarekatTerrainParams3;

TEXTURE2D(_HarekatCliffAlbedo);   SAMPLER(sampler_HarekatCliffAlbedo);
TEXTURE2D(_HarekatCliffNormal);   SAMPLER(sampler_HarekatCliffNormal);
TEXTURE2D(_HarekatDetailNormal);  SAMPLER(sampler_HarekatDetailNormal);

// Mud kanalları: taban geçiş için hiçbiri (0), Add Pass'te katman 5 (Çamur) = .g. Shader dosyası tanımlar.
#ifndef HK_WET_CHANNELS
#define HK_WET_CHANNELS half4(0, 0, 0, 0)
#endif

// Yamaç ağırlığı: normal.y >= eşik -> 0, eşik-genişlik altında -> 1.
half HK_SlopeWeight(half normalY)
{
    return (half)(1.0 - smoothstep(_HarekatTerrainParams0.y - _HarekatTerrainParams0.z, _HarekatTerrainParams0.y, normalY));
}

// Arazi tanjant çerçevesi (URP InitializeInputData ile aynı): satırlar (-T, B, N).
half3 HK_WorldToTerrainTS(half3 nWS, half3 geoN)
{
    half3 tangentWS = (half3)cross(GetObjectToWorldMatrix()._13_23_33, geoN);
    half3 bitangent = cross(geoN, tangentWS);
    return half3(dot(nWS, -tangentWS), dot(nWS, bitangent), dot(nWS, geoN));
}

// Triplanar (UDN) - gradyanlı örnekleme: dal içinde güvenli.
void HK_Cliff(float3 posWS, half3 geoN, float3 dpdx, float3 dpdy, half slope,
              inout half3 albedo, inout half3 normalTS, inout half smoothness)
{
    float inv = 1.0 / max(_HarekatTerrainParams0.w, 0.5);
    half3 w = pow(abs(geoN), 4.0);
    w /= (w.x + w.y + w.z + 1e-5h);

    float2 uvX = posWS.zy * inv, uvY = posWS.xz * inv, uvZ = posWS.xy * inv;
    float2 dxX = dpdx.zy * inv, dyX = dpdy.zy * inv;
    float2 dxY = dpdx.xz * inv, dyY = dpdy.xz * inv;
    float2 dxZ = dpdx.xy * inv, dyZ = dpdy.xy * inv;

    half3 aX = SAMPLE_TEXTURE2D_GRAD(_HarekatCliffAlbedo, sampler_HarekatCliffAlbedo, uvX, dxX, dyX).rgb;
    half3 aY = SAMPLE_TEXTURE2D_GRAD(_HarekatCliffAlbedo, sampler_HarekatCliffAlbedo, uvY, dxY, dyY).rgb;
    half3 aZ = SAMPLE_TEXTURE2D_GRAD(_HarekatCliffAlbedo, sampler_HarekatCliffAlbedo, uvZ, dxZ, dyZ).rgb;
    half3 cliffAlbedo = aX * w.x + aY * w.y + aZ * w.z;

    half3 tX = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_HarekatCliffNormal, sampler_HarekatCliffNormal, uvX, dxX, dyX));
    half3 tY = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_HarekatCliffNormal, sampler_HarekatCliffNormal, uvY, dxY, dyY));
    half3 tZ = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_HarekatCliffNormal, sampler_HarekatCliffNormal, uvZ, dxZ, dyZ));
    tX = half3(tX.xy + geoN.zy, geoN.x);
    tY = half3(tY.xy + geoN.xz, geoN.y);
    tZ = half3(tZ.xy + geoN.xy, geoN.z);
    half3 nWS = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
    half3 cliffTS = normalize(HK_WorldToTerrainTS(nWS, geoN));

    half k = saturate(slope * _HarekatTerrainParams0.x);
    albedo = lerp(albedo, cliffAlbedo, k);
    normalTS = normalize(lerp(normalTS, cliffTS, k));
    smoothness = lerp(smoothness, _HarekatTerrainParams2.w, k);
}

// Uzak mesafede döşeme tekrarını kıran düşük frekanslı renk/parlaklık lekeleri (dokusuz, prosedürel).
half3 HK_MacroTint(float2 xz, float dist)
{
    float n = HK_Fbm(xz / max(_HarekatTerrainParams1.y, 1.0));
    float fade = smoothstep(_HarekatTerrainParams1.z, _HarekatTerrainParams1.w, dist);
    float k = (n - 0.5) * 2.0 * _HarekatTerrainParams1.x * lerp(0.35, 1.0, fade);
    return half3(1.0 + k, 1.0 + k * 0.9, 1.0 + k * 0.7);
}

half HK_PuddleMask(float2 xz, half flatness, half mud)
{
    float n = HK_Fbm(xz / max(_HarekatTerrainParams3.x, 0.5));
    float cover = saturate(_HarekatTerrainParams3.y + mud * 0.25);
    float thr = 1.0 - cover;
    float p = smoothstep(thr - 0.06, thr + 0.06, n);
    return (half)(p * smoothstep(0.88, 0.97, flatness));
}

// Islaklık: albedo kararır, pürüzsüzlük artar, birikintide normal düzleşir (yalnız _HarekatWetness > 0).
void HK_Wet(float3 posWS, half geoNy, half mud, inout half3 albedo, inout half3 normalTS,
            inout half smoothness, inout half metallic)
{
    half w = (half)_HarekatWetness;
    half wetAll = saturate(w * (1.0 + 0.6 * mud));
    half level = saturate((w - 0.25) / 0.75);
    half pud = HK_PuddleMask(posWS.xz, geoNy, mud) * level;

    albedo *= lerp(1.0, (half)_HarekatTerrainParams3.z, wetAll);
    albedo *= lerp(1.0, 0.78, pud);
    smoothness = lerp(smoothness, (half)_HarekatTerrainParams3.w, saturate(wetAll * 0.6 + pud));
    metallic *= (1.0 - pud);
    normalTS.xy *= (1.0 - pud * 0.92);
    normalTS = normalize(normalTS);
}

// Fragman kancası: makro -> yamaç -> detay normal -> ıslaklık. Hepsi güç=0 iken atlanır.
void HarekatSurfaceMods(float3 posWS, half3 geoN, float3 dpdx, float3 dpdy, half mud,
                        inout half3 albedo, inout half3 normalTS, inout half smoothness, inout half metallic)
{
    float dist = distance(posWS, _WorldSpaceCameraPos);

    [branch] if (_HarekatTerrainParams0.x > 0.001)
    {
        half slope = HK_SlopeWeight(geoN.y);
        [branch] if (slope > 0.01)
            HK_Cliff(posWS, geoN, dpdx, dpdy, slope, albedo, normalTS, smoothness);
    }

    [branch] if (_HarekatTerrainParams1.x > 0.001)
        albedo = saturate(albedo * HK_MacroTint(posWS.xz, dist));

    [branch] if (_HarekatTerrainParams2.x > 0.001 && dist < _HarekatTerrainParams2.z)
    {
        float inv = 1.0 / max(_HarekatTerrainParams2.y, 0.1);
        float f = 1.0 - smoothstep(_HarekatTerrainParams2.z * 0.5, _HarekatTerrainParams2.z, dist);
        half3 d = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_HarekatDetailNormal, sampler_HarekatDetailNormal,
            posWS.xz * inv, dpdx.xz * inv, dpdy.xz * inv), (half)(_HarekatTerrainParams2.x * f));
        normalTS = normalize(half3(normalTS.xy + d.xy, normalTS.z * d.z));
    }

    [branch] if (_HarekatWetness > 0.001)
        HK_Wet(posWS, geoN.y, mud, albedo, normalTS, smoothness, metallic);
}

#endif
