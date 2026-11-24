// Cheap stylized shading for tiles and board cells: a fixed fake light (no realtime lights needed),
// side faces darkened, a thin dark rim between stacked tiles, and on top of that a candy look:
// a bright bevel ring around the top cap (cap UV.x runs 0 at the centre to 1 at the rim), a light
// gradient across the cap and a fresnel edge light on the sides.
// SRP Batcher compatible; supports GPU instancing for non-batched paths.
Shader "HexaSort/Stylized"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _ShadeStrength ("Side Shade", Range(0, 1)) = 0.35
        _LightDir ("Fake Light Direction", Vector) = (0.3, 1, -0.45, 0)
        _RimWidth ("Layer Rim Width", Range(0, 0.5)) = 0.12
        _RimDarken ("Layer Rim Darken", Range(0, 1)) = 0.25
        _Highlight ("Highlight", Range(0, 1)) = 0
        _BevelLight ("Top Bevel Light", Range(0, 1)) = 0.35
        _BevelWidth ("Top Bevel Width", Range(0.02, 0.5)) = 0.16
        _TopGradient ("Top Light Gradient", Range(0, 0.5)) = 0.12
        _Fresnel ("Side Edge Light", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Stylized"
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _ShadeStrength;
                float4 _LightDir;
                half _RimWidth;
                half _RimDarken;
                half _Highlight;
                half _BevelLight;
                half _BevelWidth;
                half _TopGradient;
                half _Fresnel;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half edge : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                // x: radial distance on the top cap (0 centre, 1 rim), y: 1 on the top cap.
                half2 cap : TEXCOORD3;
                float2 planar : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // 0 at the top/bottom edge of a side face, 0.5 in its middle and on the caps.
                output.edge = min(input.uv.y, 1.0 - input.uv.y);
                half isTop = input.normalOS.y > 0.5 ? 1.0 : 0.0;
                output.cap = half2(input.uv.x, isTop);
                output.planar = input.positionOS.xz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 n = normalize(input.normalWS);
                half3 l = normalize((half3)_LightDir.xyz);

                // Half-Lambert keeps the shadowed side colorful instead of black.
                half ndl = dot(n, l) * 0.5 + 0.5;
                half shade = lerp(1.0 - _ShadeStrength, 1.0, ndl);

                half rim = 1.0 - _RimDarken * (1.0 - smoothstep(0.0, _RimWidth, input.edge));
                half3 color = _BaseColor.rgb * shade * rim;

                // Top cap: brighter on the side facing the light, glossy bevel ring at the rim.
                half top = input.cap.y;
                half2 toward = normalize(l.xz + 1e-4);
                float planarLength = length(input.planar);
                half side = planarLength > 1e-4 ? dot(input.planar / planarLength, toward) * saturate(input.cap.x) : 0.0;
                color *= 1.0 + top * _TopGradient * side;
                half bevel = smoothstep(1.0 - _BevelWidth, 1.0, input.cap.x) * top;
                color = lerp(color, half3(1, 1, 1), bevel * _BevelLight * (0.65 + 0.35 * saturate(side + 0.5)));

                // Sides: light catches the edges turned away from the camera.
                half3 v = normalize(GetCameraPositionWS() - input.positionWS);
                half fresnel = pow(1.0 - saturate(dot(n, v)), 3.0) * (1.0 - top);
                color += fresnel * _Fresnel;

                color = lerp(color, half3(1, 1, 1), _Highlight);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
