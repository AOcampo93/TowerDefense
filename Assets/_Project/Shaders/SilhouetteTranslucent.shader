// The player's body in the style of the Quest system hands: light grey and see-through, with a brighter white
// outline. Drawn after SilhouetteDepth has written the body's depth, so only the front surface is colored and the
// overlapping capsules never stack into darker spots.
Shader "AlienInvaders/SilhouetteTranslucent"
{
    Properties
    {
        _BaseColor ("Body Color", Color) = (0.82, 0.86, 0.9, 1)
        _BodyAlpha ("Body Opacity", Range(0, 1)) = 0.22
        _RimColor ("Outline Color", Color) = (1, 1, 1, 1)
        _RimAlpha ("Outline Opacity", Range(0, 1)) = 0.85
        _RimPower ("Outline Sharpness", Range(0.5, 8)) = 2.2
    }

    SubShader
    {
        // After the depth pass (queue 3005) and after other transparent objects.
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "SilhouetteTranslucent"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _BodyAlpha;
                half4 _RimColor;
                half _RimAlpha;
                half _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half rim = pow(1.0h - facing, _RimPower);
                return half4(lerp(_BaseColor.rgb, _RimColor.rgb, rim), lerp(_BodyAlpha, _RimAlpha, rim));
            }
            ENDHLSL
        }
    }
}
