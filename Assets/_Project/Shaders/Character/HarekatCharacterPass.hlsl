// HAREKAT karakter malzemeleri ortak ileri geçiş. Mod: HK_CHAR_FABRIC / HK_CHAR_SKIN / HK_CHAR_GEAR / HK_CHAR_NVG (shader başında define).
// Formüller CharacterMaterialMath.cs ile birebir aynıdır (WrapDiffuse, DirtGradient, SpecularOcclusion, WetFactor).
#ifndef HAREKAT_CHARACTER_PASS_INCLUDED
#define HAREKAT_CHARACTER_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Assets/_Project/Shaders/Common/HarekatCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _EmissionColor;
    half _Smoothness;
    half _Metallic;
    half _BumpScale;
    float _DetailTiling;       // detay normal döşeme (UV metre biriminde; ~30-60)
    float _MacroTiling;        // makro kir/leke döşeme (~2-5)
    half4 _SheenColor;
    half _SheenStrength;
    half _SheenPower;
    half _WrapAmount;
    half4 _SssColor;
    half _SssStrength;
    half4 _DirtColor;
    half _DirtStrength;        // 0-1 (çamur/toz genel gücü)
    half _DirtStart;           // nesne-uzayı y: bunun altında kir başlar (diz/bot)
    half _DirtSpan;            // m: kirin tam doyduğu mesafe
    half _DustStrength;        // yukarı bakan yüzeyde toz
    half _WetResponse;         // küresel yağmura duyarlılık
    half _WetDarken;           // ıslak albedo çarpanı
    half _EdgeWear;            // kenar aşınması (boya)
    half4 _WearColor;
    half4 _LensTint;
    half _LensEmission;
CBUFFER_END

TEXTURE2D(_MacroMap); SAMPLER(sampler_MacroMap);   // R = makro kir/leke gürültüsü

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV  : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    float3 positionWS               : TEXCOORD1;
    float3 normalWS                 : TEXCOORD2;
    half4  tangentWS                : TEXCOORD3;
    float  objY                     : TEXCOORD4;
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5;
#else
    half  fogFactor                 : TEXCOORD5;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV        : TEXCOORD9;
#endif
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion           : TEXCOORD10;
#endif
    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

#define HKC_ALBEDO_MAX 1.0h    // lineer; NaN avı bitti (normaller mesh seviyesinde düzeldi) — kırpma gevşetildi
#define HKC_FILL_MIN 0.10h     // ters ışıkta (güneş arkada) minimum yarım-küre dolgu: siluet olmasın
#define HKC_FILL_MAX 0.20h
#define HKC_EXTRA_MAX 0.12h    // sheen/SSS/rim toplam ek katkı üst sınırı (albedo ölçeğinde ışık EKLEMEZ)

half HKC_WrapDiffuse(half ndl, half w)
{
    return saturate((ndl + w) / (1.0 + w));
}

half HKC_DirtGradient(float y, half start, half span)
{
    return saturate((start - (half)y) / max(span, 0.01));
}

half HKC_SpecOcclusion(half ndv, half ao, half smoothness)
{
    half rough = 1.0 - smoothness;
    return saturate(pow(abs(ndv + ao), exp2(-16.0 * rough - 1.0)) - 1.0 + ao);
}

Varyings HarekatCharVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0.0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.objY = input.positionOS.y;
    output.normalWS = normalInput.normalWS;
    real sgn = input.tangentOS.w * GetOddNegativeScale();
    // Sıfır/NaN teğet (dejenere UV üçgeni) normali NaN'a çevirip bloom'da beyaz patlama yapar: normale dik yedek teğet kur.
    float3 tanWS = normalInput.tangentWS.xyz;
    float tanLen2 = dot(tanWS, tanWS);
    float3 altTan = normalize(cross(normalInput.normalWS, abs(normalInput.normalWS.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
    tanWS = (tanLen2 > 1e-6 && !any(isnan(tanWS))) ? tanWS : altTan;
    sgn = (sgn == 0.0 || isnan(sgn)) ? 1.0 : sgn;
    output.tangentWS = half4(tanWS, sgn);

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

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

void HarekatCharInitInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float sgn = input.tangentWS.w;
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    inputData.tangentToWorld = tangentToWorld;
    inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void HarekatCharInitBakedGI(Varyings input, inout InputData inputData)
{
    #if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS);
    #elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
    #else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
}

void HarekatCharFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 uv = input.uv;
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    // Asker kumaşı/teni mat: lineer albedo üst sınırı (güneş 1.7 ile bile gövde 1'in altında kalır, bloom eşiğini aşmaz).
    half3 albedo = min(albedoAlpha.rgb * min(_BaseColor.rgb, 1.0), HKC_ALBEDO_MAX);

    half4 nPacked = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv * _DetailTiling);
    half3 normalTS = UnpackNormalScale(nPacked, _BumpScale);
    half macro = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, uv * _MacroTiling).r;
    half cavity = saturate(1.0 - length(normalTS.xy) * 0.9);

    half smoothness = _Smoothness;
    half metallic = _Metallic;
    half occlusion = lerp(0.82, 1.0, cavity);

    half3 nWS0 = normalize(input.normalWS);

    // ---- Makro kir: nesne-uzayı yükseklik gradyanı (diz/bot) + yukarı bakan yüzeyde toz ----
    half dirt = HKC_DirtGradient(input.objY, _DirtStart, _DirtSpan) * lerp(0.55, 1.45, macro);
    dirt += saturate(nWS0.y) * _DustStrength * macro;
    dirt = saturate(dirt * _DirtStrength);
#if defined(HK_CHAR_GEAR)
    dirt *= 0.45h;   // parça-yerel objY anlamsız: tüm parça çamura boyanıp siyaha dönmesin
#endif
    albedo = lerp(albedo, _DirtColor.rgb * lerp(0.7, 1.25, macro), dirt);
    smoothness *= (1.0 - dirt * 0.6);
    normalTS.xy *= (1.0 - dirt * 0.5);

#if defined(HK_CHAR_GEAR)
    // Boya kenar aşınması: normal haritadaki yüksek eğim + makro gürültü = çıplak taban rengi.
    half edge = saturate(length(normalTS.xy) * 2.2) * saturate(macro * 1.5);
    half wear = saturate(edge * _EdgeWear * 2.0);
    albedo = lerp(albedo, _WearColor.rgb, wear);
    smoothness = lerp(smoothness, 0.55, wear);
    metallic = lerp(metallic, 0.6, wear);
#endif

#if defined(HK_CHAR_SKIN)
    albedo *= lerp(0.96, 1.04, macro);                        // hafif ton değişimi
    smoothness *= lerp(0.55, 1.0, cavity);                    // gözenek boşluğunda parlaklık düşer
#endif

    // ---- Islaklık (küresel yağmur) ----
    half wet = saturate((half)_HarekatWetness * _WetResponse);
    albedo *= lerp(1.0, _WetDarken, wet);
#if defined(HK_CHAR_FABRIC)
    smoothness = lerp(smoothness, 0.62, wet * 0.65);
#else
    smoothness = lerp(smoothness, max(smoothness, 0.78), wet * 0.8);
#endif

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.metallic = saturate(metallic);
    surfaceData.specular = half3(0, 0, 0);
    surfaceData.smoothness = saturate(smoothness);
    surfaceData.occlusion = occlusion;
    surfaceData.normalTS = normalize(normalTS);
    surfaceData.emission = half3(0, 0, 0);
    surfaceData.alpha = 1.0;

    InputData inputData;
    HarekatCharInitInputData(input, surfaceData.normalTS, inputData);
    HarekatCharInitBakedGI(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    // Yarım-küre ortam dolgusu: SH/yansıma yoksa veya güneş arkadayken ekipman siluete dönmesin (kuruyla orantılı, ışık eklemez).
    {
        half upk = saturate(inputData.normalWS.y * 0.5h + 0.5h);
        color.rgb += albedo * occlusion * lerp(HKC_FILL_MIN, HKC_FILL_MAX, upk);
    }

    // ---- Ek terimler (ana ışık) ----
    half3 N = inputData.normalWS;
    half3 V = inputData.viewDirectionWS;
    half ndv = saturate(dot(N, V));
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    half ndl = dot(N, mainLight.direction);
    half3 lightCol = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);
    half specOcc = HKC_SpecOcclusion(ndv, occlusion, surfaceData.smoothness);

#if defined(HK_CHAR_FABRIC)
    half rim = pow(1.0 - ndv, max(_SheenPower, 0.5));
    half back = lerp(0.35, 1.0, saturate(ndl * 0.5 + 0.5));
    half dryness = 1.0 - wet * 0.6;
    half3 sheen = _SheenColor.rgb * _SheenStrength * rim * (lightCol * back + inputData.bakedGI * 0.5) * dryness * specOcc;
    color.rgb += min(sheen * lerp(1.0, 0.5, dirt), HKC_EXTRA_MAX);
#elif defined(HK_CHAR_SKIN)
    half wrapTerm = HKC_WrapDiffuse(ndl, _WrapAmount) - saturate(ndl);
    color.rgb += min(albedo * _SssColor.rgb * _SssStrength * max(wrapTerm, 0.0) * lightCol, HKC_EXTRA_MAX * 2.0);
    half3 skinRim = _SssColor.rgb * pow(1.0 - ndv, 4.0) * 0.12 * (lightCol + inputData.bakedGI) * specOcc;
    color.rgb += min(skinRim, HKC_EXTRA_MAX);
#elif defined(HK_CHAR_NVG)
    half fres = pow(1.0 - ndv, 3.0);
    color.rgb += min(_LensTint.rgb * _LensEmission * (0.12 + fres) * specOcc, 0.6);
#endif

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
#if !defined(HK_CHAR_NVG)
    // NaN/Inf ve aşırı HDR koruması: yalnızca NaN→0 ve taşma (Inf) koruması; parlaklık sınırı yok (karanlık siluet hatası).
    color.rgb = any(isnan(color.rgb)) ? half3(0, 0, 0) : min(color.rgb, 16.0h);
#endif
    color.a = 1.0;
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
