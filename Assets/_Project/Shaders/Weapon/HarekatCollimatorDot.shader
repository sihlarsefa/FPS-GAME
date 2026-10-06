// HAREKAT kolimatör (kırmızı nokta) camı: nokta sonsuza, namlu yönüne yansıtılır -> paralaksız.
// Pencere mesh'i: yerel +Z = namlu yönü. Göz ışını yerel +Z'ye ne kadar yakınsa nokta o kadar parlak (açısal uzayda çizilir).
Shader "HAREKAT/Weapon/CollimatorDot"
{
    Properties
    {
        _DotColor("Nokta rengi", Color) = (1.0, 0.08, 0.05, 1)
        _DotRadius("Nokta açısal yarıçapı (tan, 0.0015 ~ 1.5 MOA)", Range(0.0002, 0.01)) = 0.0016
        _Brightness("Parlaklık", Range(0.0, 8.0)) = 3.0
        _BoreTilt("Sıfırlama sapması (tan x,y)", Vector) = (0, 0, 0, 0)
        _WindowFade("Pencere kenarı sönümü", Range(0.01, 0.5)) = 0.12
        _GlassTint("Cam rengi", Color) = (0.7, 0.85, 0.9, 1)
        _GlassAlpha("Cam saydamlığı", Range(0.0, 0.5)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Collimator"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DotColor;
                float _DotRadius;
                float _Brightness;
                float4 _BoreTilt;
                float _WindowFade;
                half4 _GlassTint;
                float _GlassAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirOS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                // Göz -> pencere noktası yönü (nesne uzayı); pencere düzlemi xy, namlu +z.
                float3 eyeOS = TransformWorldToObject(GetCameraPositionWS());
                o.viewDirOS = v.positionOS.xyz - eyeOS;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 v = normalize(i.viewDirOS);
                // Açısal koordinat: namlu yönünden sapma (tan). Göz konumu değişse de nokta namlu yönünde kalır.
                float2 ang = v.xy / max(abs(v.z), 1e-4) - _BoreTilt.xy;
                float d = length(ang);
                float dot = 1.0 - smoothstep(_DotRadius * 0.6, _DotRadius, d);
                float halo = exp(-d / (_DotRadius * 4.0)) * 0.12;

                // Pencere kenarında söner (uv 0..1 merkezli).
                float2 e = abs(i.uv - 0.5) * 2.0;
                float edge = 1.0 - smoothstep(1.0 - _WindowFade, 1.0, max(e.x, e.y));

                float3 dotRgb = _DotColor.rgb * _Brightness * (dot + halo) * edge;
                float a = saturate(dot + halo) * edge;
                float3 rgb = dotRgb + _GlassTint.rgb * _GlassAlpha;
                return half4(rgb, saturate(a + _GlassAlpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
