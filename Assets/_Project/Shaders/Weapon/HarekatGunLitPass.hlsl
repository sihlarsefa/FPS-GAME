// HAREKAT silah ileri geçişi (GunLit + OpticGlass). Dinamik nesne: lightmap yok, SH/yansıma probu kullanır.
// Girdi: UV0 (projeksiyon), UV1 = dörtgen yerel (u,v), UV2 = dörtgenin metre boyutu (w,h; <=0 veya >=500 = kenar yok).
#ifndef HAREKAT_GUN_LIT_PASS_INCLUDED
#define HAREKAT_GUN_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _EmissionColor;
    half _Smoothness;
    half _Metallic;
    half _GunKind;
    half _WearAmount;
    half4 _WearColor;
    half _WearMetallic;
    half _WearSmoothness;
    float _EdgeWidth;
    half _MicroStrength;
    half _SheenAmount;
    half4 _CoatTint;
    half4 _CoatTint2;
    float4 _HarekatCarbon;     // x karbon 0-1, y namlu Z (model), z karbon uzunluğu (m), w yağ çarpanı
    float4 _HarekatPartPivot;  // xyz parça pivotu (model uzayı)
CBUFFER_END

// Küresel (WeaponMaterialDriver yazar). Atanmamış = 0 = tam detay / varsayılan taban ışık.
float _HarekatGunLowDetail;   // 1 = mikro doku/çizik kapalı (Düşük kademe)
float _HarekatGunFillBias;    // taban ortam ışığına ek (iç mekân)

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 texcoord     : TEXCOORD0;
    float2 quadUv       : TEXCOORD1;
    float2 quadSize     : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    float3 positionWS               : TEXCOORD1;
    float3 normalWS                 : TEXCOORD2;
    float4 edge                     : TEXCOORD3;   // xy: dörtgen UV, zw: dörtgen boyutu (m)
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5;
#else
    half  fogFactor                 : TEXCOORD5;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif
    float3 positionOS               : TEXCOORD7;
    half3 vertexSH                  : TEXCOORD8;
    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ---- Prosedürel gürültü (3B değer gürültüsü) ----
float GK_Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float GK_Noise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = GK_Hash31(i);
    float n100 = GK_Hash31(i + float3(1, 0, 0));
    float n010 = GK_Hash31(i + float3(0, 1, 0));
    float n110 = GK_Hash31(i + float3(1, 1, 0));
    float n001 = GK_Hash31(i + float3(0, 0, 1));
    float n101 = GK_Hash31(i + float3(1, 0, 1));
    float n011 = GK_Hash31(i + float3(0, 1, 1));
    float n111 = GK_Hash31(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
}

float GK_Fbm3(float3 p)
{
    return GK_Noise3(p) * 0.6 + GK_Noise3(p * 2.17 + 5.3) * 0.4;
}

// Dörtgen ekseninde en yakın kenara metre cinsinden uzaklık (boyut geçersizse büyük değer).
float GK_AxisDist(float c, float size)
{
    return (size > 0.0001 && size < 500.0) ? min(c, 1.0 - c) * size : 1.0;
}

Varyings GunPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.normalWS = normalInput.normalWS;
    output.edge = float4(input.quadUv, input.quadSize);
    output.positionOS = input.positionOS.xyz;
    output.vertexSH = SampleSHVertex(output.normalWS);

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

    output.positionWS = vertexInput.positionWS;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif
    output.positionCS = vertexInput.positionCS;
    return output;
}

// Yüzey normalini yükseklik alanının ekran türevinden bozar (teğet uzayı gerektirmez).
half3 GK_PerturbNormal(half3 n, float3 positionWS, float height, float scale)
{
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float dhdx = ddx(height);
    float dhdy = ddy(height);
    float3 r1 = cross(dpdy, (float3)n);
    float3 r2 = cross((float3)n, dpdx);
    float det = dot(dpdx, r1);
    float3 grad = sign(det) * (dhdx * r1 + dhdy * r2);
    return (half3)normalize(abs(det) * (float3)n - scale * grad);
}

void GunPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 posWS = input.positionWS;
    float dist = distance(posWS, _WorldSpaceCameraPos);
    float3 pms = input.positionOS + _HarekatPartPivot.xyz;   // model uzayı (metre)
    half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
    float detailOn = (1.0 - saturate(_HarekatGunLowDetail)) * (1.0 - smoothstep(2.5, 7.0, dist));

    half3 albedo = _BaseColor.rgb;
    half metallic = _Metallic;
    half smoothness = _Smoothness;
    float height = 0.0;
    float heightScale = 0.0;

#if defined(HK_GLASS)
    // Kaplamalı lens: bakış açısına göre mavi-amber kayan ince film tonu, yüksek yansıma.
    half ndv = saturate(dot(normalWS, viewDirWS));
    half fres = (half)pow(1.0 - ndv, 2.0);
    half3 coat = lerp(_CoatTint.rgb, _CoatTint2.rgb, saturate(fres * 1.6));
    albedo = lerp(albedo, coat, 0.55);
    metallic = lerp(_Metallic, 0.9, fres);
#else
    int kind = (int)(_GunKind + 0.5);
    float n2 = GK_Fbm3(pms * 38.0);                    // iri ölçek: aşınma dağılımı
    float n1 = GK_Noise3(pms * 120.0);                 // ince ölçek: kenar çentiği

    // ---- Mikro doku ----
    if (kind == 0)
    {
        height = GK_Noise3(pms * 190.0) * 0.6 + GK_Noise3(pms * 65.0) * 0.4;
        heightScale = 0.0006;
        smoothness -= 0.12 * (half)(height - 0.5);
    }
    else if (kind == 1)
    {
        height = GK_Noise3(pms * 240.0);
        heightScale = 0.0005;
    }
    else if (kind == 2)
    {
        height = GK_Noise3(pms * 82.0) * 0.55 + GK_Noise3(pms * 230.0) * 0.45;
        heightScale = 0.0008;
    }
    else if (kind == 5)
    {
        height = GK_Noise3(pms * 90.0);
        heightScale = 0.0014;
    }
    else if (kind == 4)
    {
        height = GK_Noise3(pms * 150.0);
        heightScale = 0.0006;
    }
    else if (kind == 3)
    {
        // Ahşap damarı: gövde ekseni +Z, halkalar XY düzleminde.
        float wob = GK_Noise3(pms * float3(26.0, 26.0, 3.5)) * 2.4;
        float rings = frac(length(pms.xy * float2(1.0, 1.7)) * 95.0 + wob);
        float streak = smoothstep(0.0, 0.55, rings) * (1.0 - smoothstep(0.55, 1.0, rings));
        float pores = GK_Noise3(pms * float3(220.0, 220.0, 30.0));
        albedo *= (half)(lerp(0.72, 1.14, streak) * lerp(0.9, 1.08, pores));
        height = pores * 0.5 + streak * 0.5;
        heightScale = 0.0007;
        smoothness += (half)((streak - 0.5) * 0.08);
    }

    // ---- Kenar aşınması (mesh dörtgen-kenar UV'si) + çizikler ----
    float edgeDist = min(GK_AxisDist(input.edge.x, input.edge.z), GK_AxisDist(input.edge.y, input.edge.w));
    float edgeW = max(_EdgeWidth, 0.0002) * (0.45 + 1.7 * n1);
    float edge = 1.0 - smoothstep(0.0, edgeW, edgeDist);
    float wearField = saturate((float)_WearAmount * (0.3 + 1.1 * n2));
    float scratchLine = 1.0 - smoothstep(0.0, 0.035, abs(GK_Noise3(pms * float3(160.0, 40.0, 120.0)) - 0.5));
    float scratch = scratchLine * step(0.72, n2) * (float)_WearAmount * 0.9 * detailOn;
    float scuffK = (kind == 2 || kind == 3) ? 1.0 : 0.4;
    float scuff = smoothstep(0.62, 0.9, n2) * (float)_WearAmount * 0.35 * scuffK;   // geniş el/yüzey sürtünmesi
    half wear = (half)saturate(edge * wearField * 1.9 + scratch + scuff);

    albedo = lerp(albedo, _WearColor.rgb, wear);
    metallic = lerp(metallic, _WearMetallic, wear);
    smoothness = lerp(smoothness, _WearSmoothness, wear);

    // ---- Yağ parlaması: metalde yaygın, ahşap/polimerde az ----
    float oilK = (kind <= 1) ? 1.0 : ((kind == 4) ? 0.35 : 0.5);
    float oilMask = smoothstep(0.55, 0.86, GK_Fbm3(pms * 21.0 + 3.1));
    half oil = (half)(oilMask * _SheenAmount * _HarekatCarbon.w * oilK);
    smoothness = saturate(smoothness + oil * 0.32);
    albedo *= 1.0 - oil * 0.18;

    // ---- Namlu karbon birikimi: namlu ucuna doğru + kovan atma bölgesi, kurum çizgileriyle ----
    float carbonAmt = _HarekatCarbon.x;
    [branch] if (carbonAmt > 0.002)
    {
        float len = max(_HarekatCarbon.z, 0.02);
        float muzzle = smoothstep(_HarekatCarbon.y - len, _HarekatCarbon.y + 0.01, pms.z);
        float port = (1.0 - smoothstep(0.0, 0.1, abs(pms.z - 0.02))) * 0.45 * step(0.0, pms.y);
        float sootStreak = lerp(0.65, 1.0, GK_Noise3(pms * float3(60.0, 60.0, 8.0)));
        half soot = (half)saturate((muzzle + port) * sootStreak * carbonAmt * 1.25);
        albedo = lerp(albedo, albedo * half3(0.2, 0.19, 0.18), soot);
        smoothness = lerp(smoothness, 0.12, soot * 0.85);
        metallic *= 1.0 - soot * 0.6;
        float tempering = (float)soot * smoothstep(0.35, 0.9, carbonAmt) * (1.0 - smoothstep(0.0, 0.05, abs(pms.z - _HarekatCarbon.y + 0.03)));
        albedo += half3(0.05, 0.035, 0.08) * (half)tempering;   // ısıdan mavimsi renk kayması
    }

    // Türev içerdiği için koşulsuz çağrılır (kapalıyken ölçek 0).
    normalWS = GK_PerturbNormal(normalWS, posWS, height, (float)_MicroStrength * heightScale * detailOn);
#endif

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = saturate(albedo);
    // Tam metal difüz ışığı siler (silah siyah blob); üst sınır + yansıma probusuz da okunur.
    surfaceData.metallic = min(saturate(metallic), 0.55h);
    surfaceData.specular = half3(0, 0, 0);
    surfaceData.smoothness = saturate(smoothness);
    surfaceData.occlusion = 1.0;
    surfaceData.normalTS = half3(0, 0, 1);
    surfaceData.emission = half3(0, 0, 0);
    surfaceData.alpha = 1.0;

    InputData inputData = (InputData)0;
    inputData.positionWS = posWS;
#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(posWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(posWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(posWS, 1.0), input.fogFactor);
#endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    // Ortam ışığı: SH (renderer'da lightProbeUsage açık) + iç mekânda çok koyu kalmasın diye taban.
    half3 sh = SampleSHPixel(input.vertexSH, normalWS);
    half floorLevel = 0.14 + (half)_HarekatGunFillBias;
    inputData.bakedGI = max(sh, half3(floorLevel, floorLevel, floorLevel));
    inputData.shadowMask = half4(1, 1, 1, 1);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    // Ortam taban dolgusu: gölgede/uzakta albedo * taban (viewmodel tabanı 0.14 korunur).
    color.rgb += surfaceData.albedo * (1.0h - surfaceData.metallic) * floorLevel * 0.6h;
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1.0;
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
