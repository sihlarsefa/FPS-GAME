// HAREKAT karakter shader (Skin). Ten: wrap-diffuse + alt-yüzey tonu, gözenek normali, speküler oklüzyon.
Shader "HAREKAT/Character/Skin"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Renk", Color) = (1, 1, 1, 1)
        _Smoothness("Pürüzsüzlük", Range(0.0, 1.0)) = 0.25
        _Metallic("Metalik", Range(0.0, 1.0)) = 0.0
        [NoScaleOffset] _BumpMap("Detay normal", 2D) = "bump" {}
        _BumpScale("Normal gücü", Float) = 1.0
        _DetailTiling("Detay döşeme", Float) = 40
        [NoScaleOffset] _MacroMap("Makro kir (R)", 2D) = "white" {}
        _MacroTiling("Makro döşeme", Float) = 3
        _SheenColor("Sheen rengi", Color) = (0.6, 0.62, 0.55, 1)
        _SheenStrength("Sheen gücü", Range(0.0, 2.0)) = 0.35
        _SheenPower("Sheen üssü", Range(0.5, 8.0)) = 3.0
        _WrapAmount("Wrap", Range(0.0, 1.0)) = 0.4
        _SssColor("Alt-yüzey tonu", Color) = (0.9, 0.3, 0.2, 1)
        _SssStrength("Alt-yüzey gücü", Range(0.0, 2.0)) = 0.6
        _DirtColor("Kir rengi", Color) = (0.22, 0.17, 0.11, 1)
        _DirtStrength("Kir gücü", Range(0.0, 1.0)) = 0.5
        _DirtStart("Kir başlangıç y", Float) = -0.05
        _DirtSpan("Kir mesafesi", Float) = 0.35
        _DustStrength("Toz", Range(0.0, 1.0)) = 0.2
        _WetResponse("Islaklık duyarlılığı", Range(0.0, 1.0)) = 0.8
        _WetDarken("Islak albedo çarpanı", Range(0.2, 1.0)) = 0.62
        _EdgeWear("Kenar aşınması", Range(0.0, 1.0)) = 0.0
        _WearColor("Aşınma rengi", Color) = (0.28, 0.27, 0.25, 1)
        _LensTint("Lens tonu", Color) = (0.1, 0.9, 0.35, 1)
        _LensEmission("Lens parıltısı", Range(0.0, 2.0)) = 0.0

        [HideInInspector] _EmissionColor("Emisyon", Color) = (0, 0, 0, 1)
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _ReceiveShadows("Receive Shadows", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex HarekatCharVertex
            #pragma fragment HarekatCharFragment

            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_REFLECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #define HK_CHAR_SKIN 1
            #include "Assets/_Project/Shaders/Character/HarekatCharacterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
