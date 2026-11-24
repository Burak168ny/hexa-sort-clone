// Translucent, softly shaded silhouette: the drop preview on the hovered cell and the drag shadow.
Shader "HexaSort/Ghost"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 0.35)
        _ShadeStrength ("Side Shade", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Ghost"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _ShadeStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half up = saturate(normalize(input.normalWS).y);
                half shade = lerp(1.0 - _ShadeStrength, 1.0, up);
                return half4(_BaseColor.rgb * shade, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
