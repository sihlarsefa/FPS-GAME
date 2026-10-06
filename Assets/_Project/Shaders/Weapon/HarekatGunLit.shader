// HAREKAT silah malzemesi: parkerize çelik / anodize alüminyum / polimer / ahşap / boya, kenar aşınması (mesh'in dörtgen-kenar UV'sinden),
// mikro doku, yağ parlaması, namlu karbon birikimi. Optik cam için HAREKAT/Weapon/OpticGlass.
// _GunKind: 0 parkerize çelik, 1 anodize alüminyum, 2 polimer, 3 ahşap, 4 boya, 5 kauçuk.
// MPB (WeaponMaterialDriver): _HarekatCarbon (x karbon 0-1, y namlu Z, z karbon uzunluğu, w yağ), _HarekatPartPivot (parça pivotu, model uzayı).
Shader "HAREKAT/Weapon/GunLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Renk", Color) = (0.13, 0.13, 0.14, 1)
        _Smoothness("Pürüzsüzlük", Range(0.0, 1.0)) = 0.4
        _Metallic("Metalik", Range(0.0, 1.0)) = 0.7
        _GunKind("Tür (0 çelik 1 alüminyum 2 polimer 3 ahşap 4 boya 5 kauçuk)", Float) = 0
        _WearAmount("Aşınma", Range(0.0, 1.0)) = 0.5
        _WearColor("Aşınmış yüzey rengi", Color) = (0.5, 0.5, 0.52, 1)
        _WearMetallic("Aşınmış metalik", Range(0.0, 1.0)) = 1.0
        _WearSmoothness("Aşınmış pürüzsüzlük", Range(0.0, 1.0)) = 0.55
        _EdgeWidth("Kenar aşınma genişliği (m)", Float) = 0.0018
        _MicroStrength("Mikro doku gücü", Range(0.0, 2.0)) = 1.0
        _SheenAmount("Yağ parlaması", Range(0.0, 1.0)) = 0.5
        _CoatTint("Kaplama rengi A (cam)", Color) = (0.2, 0.3, 0.9, 1)
        _CoatTint2("Kaplama rengi B (cam)", Color) = (0.9, 0.5, 0.15, 1)

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

            #pragma vertex GunPassVertex
            #pragma fragment GunPassFragment

            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Assets/_Project/Shaders/Weapon/HarekatGunLitPass.hlsl"
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
