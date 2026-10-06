// HAREKAT Parallax Occlusion Mapping + ıslak zemin (Lit uyumlu özellik adları). Yalnız ileri geçiş.
#ifndef HAREKAT_PARALLAX_LIT_PASS_INCLUDED
#define HAREKAT_PARALLAX_LIT_PASS_INCLUDED

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
    half _OcclusionStrength;
    half _Parallax;              // derinlik ölçeği (UV birimi, 0.005-0.08)
    half _HarekatPomMinSteps;    // bakış dikken adım
    half _HarekatPomMaxSteps;    // eğik bakışta adım
    half _HarekatPomFadeStart;   // m: POM sönmeye başlar
    half _HarekatPomFadeEnd;     // m: POM biter (tek adım/düz)
    half _HarekatWetResponse;    // 0-1: küresel ıslaklığa duyarlılık (çamur 1.0)
    half _HarekatBaseWet;        // 0-1: her zaman ıslak taban (yaş çamur)
    half _HarekatPuddleLevel;    // 0-1: su birikinti dolgu yüksekliği üst sınırı
    half _HarekatWetDarken;      // ıslak albedo çarpanı
    half _HarekatWetSmooth;      // ıslak/birikinti pürüzsüzlüğü
    half _HarekatPuddleScale;    // m: birikinti dağılım ölçeği
CBUFFER_END

TEXTURE2D(_MaskMap);        SAMPLER(sampler_MaskMap);        // ORMH: R=AO, G=pürüzlülük, B=metalik, A=yükseklik
TEXTURE2D(_ParallaxMap);    SAMPLER(sampler_ParallaxMap);    // Lit uyumu: G=yükseklik (MaskMap yokken)
TEXTURE2D(_HarekatWetMask); SAMPLER(sampler_HarekatWetMask); // R=ıslaklık maskesi (beyaz=her yer)

// Küresel POM kalitesi: 0=kapalı (düz), 0.5 Orta, 1 Yüksek, 1.5 Ultra (adım çarpanı). TerrainShaderBinder yazar.
float _HarekatPomQuality;

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
    half4  tangentWS                : TEXCOORD3;    // xyz: tanjant, w: işaret
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

half HK_SampleHeight(float2 uv, float2 dx, float2 dy)
{
#if defined(_MASKMAP)
    return SAMPLE_TEXTURE2D_GRAD(_MaskMap, sampler_MaskMap, uv, dx, dy).a;
#else
    return SAMPLE_TEXTURE2D_GRAD(_ParallaxMap, sampler_ParallaxMap, uv, dx, dy).g;
#endif
}

// Parallax Occlusion Mapping: doğrusal arama + iki nokta enterpolasyonu. Mesafe/kalite ile adım ve ölçek azalır.
// Dönüş: yeni UV; heightOut: o noktadaki yükseklik (ıslak birikinti için).
float2 HK_POM(float2 uv, half3 viewDirTS, float dist, out half heightOut)
{
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    heightOut = HK_SampleHeight(uv, dx, dy);

    float fade = 1.0 - smoothstep(_HarekatPomFadeStart, max(_HarekatPomFadeEnd, _HarekatPomFadeStart + 0.01), dist);
    float scale = _Parallax * fade;
    float q = _HarekatPomQuality;

    [branch] if (q <= 0.001 || scale <= 1e-5)
        return uv;

    float vz = max(abs(viewDirTS.z), 0.12);
    float steps = clamp(lerp(_HarekatPomMaxSteps, _HarekatPomMinSteps, saturate(abs(viewDirTS.z))) * q, 4.0, 64.0);
    float layerDepth = 1.0 / steps;
    float2 duv = (viewDirTS.xy / vz) * scale * layerDepth;   // adım başına UV kayması

    float2 cuv = uv;
    float curDepth = 0.0;
    float mapDepth = 1.0 - heightOut;
    [loop] for (int i = 0; i < 64; i++)
    {
        if (curDepth >= mapDepth || i >= (int)steps)
            break;
        cuv -= duv;
        mapDepth = 1.0 - HK_SampleHeight(cuv, dx, dy);
        curDepth += layerDepth;
    }

    float2 prevUV = cuv + duv;
    float after = mapDepth - curDepth;
    float before = (1.0 - HK_SampleHeight(prevUV, dx, dy)) - curDepth + layerDepth;
    float w = saturate(after / (after - before - 1e-5));
    float2 res = lerp(cuv, prevUV, w);
    heightOut = HK_SampleHeight(res, dx, dy);
    return res;
}

Varyings HarekatPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.normalWS = normalInput.normalWS;
    real sgn = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sgn);

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

void HarekatInitInputData(Varyings input, half3 normalTS, out InputData inputData)
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
    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif
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

void HarekatInitBakedGI(Varyings input, inout InputData inputData)
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

void HarekatPassFragment(
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
    half height = 1.0;
    float dist = distance(input.positionWS, _WorldSpaceCameraPos);

#if defined(_PARALLAXMAP)
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, GetWorldSpaceNormalizeViewDir(input.positionWS));
    uv = HK_POM(uv, viewDirTS, dist, height);
#else
    height = HK_SampleHeight(uv, ddx(uv), ddy(uv));
#endif

    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;

    half occlusion = 1.0;
    half smoothness = _Smoothness;
    half metallic = _Metallic;
#if defined(_MASKMAP)
    half4 orm = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
    occlusion = lerp(1.0, orm.r, _OcclusionStrength);
    smoothness = (1.0 - orm.g) * _Smoothness;   // G = pürüzlülük; _Smoothness = üst sınır/çarpan
    metallic = orm.b * _Metallic;
#endif
    half3 normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);

    // ---- Islaklık / su birikintisi (küresel ıslaklık * duyarlılık + taban ıslaklık) ----
    half wet = saturate((half)_HarekatWetness * _HarekatWetResponse + _HarekatBaseWet);
    [branch] if (wet > 0.001)
    {
        half mask = SAMPLE_TEXTURE2D(_HarekatWetMask, sampler_HarekatWetMask, uv).r;
        wet *= mask;
        // Büyük ölçekli dağılım: her çukur eşit dolmasın
        float nz = HK_Fbm(input.positionWS.xz / max(_HarekatPuddleScale, 0.25));
        half level = saturate((wet - 0.25) / 0.75) * _HarekatPuddleLevel * (half)lerp(0.35, 1.0, nz);
        half pud = (half)(1.0 - smoothstep(level - 0.04, level + 0.02, height)) * step(0.001, level);
        albedo *= lerp(1.0, _HarekatWetDarken, wet);
        albedo *= lerp(1.0, 0.8, pud);
        smoothness = lerp(smoothness, _HarekatWetSmooth, saturate(wet * 0.55 + pud));
        metallic *= (1.0 - pud);
        normalTS.xy *= (1.0 - pud * 0.95);
        normalTS = normalize(normalTS);
    }

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.metallic = saturate(metallic);
    surfaceData.specular = half3(0, 0, 0);
    surfaceData.smoothness = saturate(smoothness);
    surfaceData.occlusion = occlusion;
    surfaceData.normalTS = normalTS;
    surfaceData.emission = _EmissionColor.rgb * 0;   // emisif kullanılmıyor (Lit uyumu için alan var)
    surfaceData.alpha = 1.0;

    InputData inputData;
    HarekatInitInputData(input, surfaceData.normalTS, inputData);
    HarekatInitBakedGI(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1.0;
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
