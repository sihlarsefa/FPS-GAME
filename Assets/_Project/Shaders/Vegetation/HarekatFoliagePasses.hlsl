#ifndef HAREKAT_FOLIAGE_PASSES_INCLUDED
#define HAREKAT_FOLIAGE_PASSES_INCLUDED

// Ortak yaprak/cim gecis kodu. Her gecis .shader icinde bir HAREKAT_PASS_* tanimlayip bu dosyayi ekler.
//   HAREKAT_GRASS tanimliysa: cim modu (egilme agirlikli ruzgar + ezilme + ornek rengi). Yoksa: agac/calı modu (govde sallanmasi + dal titremesi).
// Vertex color: R = egilme agirligi, G = kamis basina rastgele faz, B = ortam kapanmasi (AO).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "HarekatWindCommon.hlsl"

// SRP Batcher: tum malzeme ozellikleri ayni UnityPerMaterial blogunda, tum gecislerde ayni sirada.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4  _BaseColor;
half4  _ColorBottom;
half4  _ColorTop;
half   _Cutoff;
half   _Smoothness;
half   _Translucency;
half   _NormalUp;
half   _WindInfluence;
half   _TrunkSway;
half   _BranchFlutter;
half   _TrampleStrength;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// Ornek basina renk (Graphics.RenderMeshInstanced veri yapisinda Matrix4x4'ten sonra gelen float4).
UNITY_INSTANCING_BUFFER_START(HarekatFoliage)
    UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
UNITY_INSTANCING_BUFFER_END(HarekatFoliage)

float3 HarekatDisplaceWS(float3 posWS, float3 posOS, float4 vcol)
{
    float bend = vcol.r;
    float phase = vcol.g;
    float3 rootWS = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
    float scaleY = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
#if defined(HAREKAT_GRASS)
    // Mesafe solmasi: cizim mesafesinin sonunda bicak boyu 0'a iner (alfa kesmeli cimde ani belirmeyi onler).
    if (_HarekatGrassFade.x > 0.0)
    {
        float camDist = distance(rootWS, _WorldSpaceCameraPos);
        float fade = saturate((_HarekatGrassFade.x - camDist) / max(_HarekatGrassFade.y, 0.01));
        posWS = rootWS + (posWS - rootWS) * fade;
    }
    float3 off = HarekatWindBend(posWS, bend, phase, _WindInfluence, scaleY);
    off += HarekatTrample(posWS, rootWS, bend, _TrampleStrength);
    return posWS + off;
#else
    float h = posOS.y * scaleY;
    float3 off = HarekatTrunkSway(posWS, rootWS, h, _TrunkSway, _WindInfluence);
    off += HarekatWindBend(posWS, bend, phase, _WindInfluence, _BranchFlutter);
    return posWS + off;
#endif
}

float3 HarekatNormalWS(float3 normalOS)
{
    float3 n = TransformObjectToWorldNormal(normalOS);
    return normalize(lerp(n, float3(0, 1, 0), _NormalUp));
}

// =====================================================================================
#if defined(HAREKAT_PASS_FORWARD)

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord   : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    half4  fogAo      : TEXCOORD5;   // x: sis, yzw: vertex isik
    half   ao         : TEXCOORD3;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD6;
#endif
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings ForwardVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    positionWS = HarekatDisplaceWS(positionWS, input.positionOS.xyz, input.color);
    float3 normalWS = HarekatNormalWS(input.normalOS);

    VertexPositionInputs vertexInput = (VertexPositionInputs)0;
    vertexInput.positionWS = positionWS;
    vertexInput.positionVS = TransformWorldToView(positionWS);
    vertexInput.positionCS = TransformWorldToHClip(positionWS);
    float4 ndc = vertexInput.positionCS * 0.5f;
    vertexInput.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    vertexInput.positionNDC.zw = vertexInput.positionCS.zw;

    half3 vertexLight = 0;
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    vertexLight = VertexLighting(positionWS, normalWS);
#endif
    half fogFactor = 0;
#if !defined(_FOG_FRAGMENT)
    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.normalWS = normalWS;
    output.ao = input.color.b;
    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
    OUTPUT_SH4(positionWS, normalWS, GetWorldSpaceNormalizeViewDir(positionWS), output.vertexSH, output.probeOcclusion);
    output.fogAo = half4(fogFactor, vertexLight);
    output.positionWS = positionWS;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif
    output.positionCS = vertexInput.positionCS;
    return output;
}

half4 ForwardFragment(Varyings input, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 tex = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half alpha = Alpha(tex.a, _BaseColor, _Cutoff);

    half3 albedo = tex.rgb * _BaseColor.rgb;
#if defined(HAREKAT_GRASS)
    float4 inst = UNITY_ACCESS_INSTANCED_PROP(HarekatFoliage, _InstColor);
    half3 ic = inst.a > 0.5 ? inst.rgb : half3(1, 1, 1);
    albedo *= lerp(_ColorBottom.rgb, _ColorTop.rgb, saturate(input.uv.y)) * ic;
#endif

    SurfaceData s = (SurfaceData)0;
    s.albedo = albedo;
    s.metallic = 0;
    s.specular = 0;
    s.smoothness = _Smoothness;
    s.normalTS = half3(0, 0, 1);
    s.occlusion = lerp(0.4, 1.0, input.ao);
    s.alpha = alpha;

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    // Cift yuzlu: arka yuz icin normali cevir (cimde _NormalUp zaten yukari bakar, etkisi kucuk).
    half3 nWS = input.normalWS * IS_FRONT_VFACE(isFront, 1.0, -1.0);
    inputData.normalWS = NormalizeNormalPerPixel(nWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogAo.x);
    inputData.vertexLighting = input.fogAo.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogAo.x);
#endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS);
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

    half4 color = UniversalFragmentPBR(inputData, s);

    // Yaprak icinden gecen isik (translucency): isik kameraya dogru ise parlar, golgeye saygi duyar.
    if (_Translucency > 0.001)
    {
        Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
        half back = saturate(dot(-inputData.viewDirectionWS, mainLight.direction));
        back = back * back * back;
        half wrap = saturate(dot(-nWS, mainLight.direction) * 0.5 + 0.5);
        color.rgb += albedo * mainLight.color * mainLight.shadowAttenuation * (back * 0.8 + wrap * 0.2) * _Translucency;
    }

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1;
    return color;
}

// =====================================================================================
#elif defined(HAREKAT_PASS_SHADOW)

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord   : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv         : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings ShadowVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    positionWS = HarekatDisplaceWS(positionWS, input.positionOS.xyz, input.color);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(positionCS);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    return output;
}

half4 ShadowFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    return 0;
}

// =====================================================================================
#elif defined(HAREKAT_PASS_DEPTH)

struct Attributes
{
    float4 positionOS : POSITION;
    float2 texcoord   : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv         : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    positionWS = HarekatDisplaceWS(positionWS, input.positionOS.xyz, input.color);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformWorldToHClip(positionWS);
    return output;
}

half DepthFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    return input.positionCS.z;
}

// =====================================================================================
#elif defined(HAREKAT_PASS_DEPTHNORMALS)

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord   : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    positionWS = HarekatDisplaceWS(positionWS, input.positionOS.xyz, input.color);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.normalWS = HarekatNormalWS(input.normalOS);
    return output;
}

void DepthNormalsFragment(
    Varyings input
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);

    #if defined(_GBUFFER_NORMALS_OCT)
    float3 normalWS = normalize(input.normalWS);
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
    outNormalWS = half4(packedNormalWS, 0.0);
    #else
    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
    outNormalWS = half4(normalWS, 0.0);
    #endif

    #ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

#endif
#endif
