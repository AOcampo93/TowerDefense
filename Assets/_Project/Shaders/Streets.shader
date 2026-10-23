// Ground of the city seen from 60 m up: city blocks separated by darker streets with faint lane marks. The grid is
// drawn in the object's own space in meters, so it is glued to the ground while the city leans. A regular street
// grid is a strong depth cue: the player can judge how far down the ground is.
Shader "AlienInvaders/Streets"
{
    Properties
    {
        _BlockColor ("City Block", Color) = (0.2, 0.18, 0.17, 1)
        _StreetColor ("Street", Color) = (0.07, 0.07, 0.08, 1)
        _LineColor ("Lane Mark", Color) = (0.55, 0.45, 0.25, 1)
        _BlockSize ("Block Size (meters)", Float) = 40
        _StreetWidth ("Street Width (meters)", Float) = 9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Streets"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BlockColor;
                half4 _StreetColor;
                half4 _LineColor;
                float _BlockSize;
                float _StreetWidth;
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
                float2 groundMeters : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float2 scale = float2(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m02_m12_m22));
                output.groundMeters = input.positionOS.xz * scale;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normalWS = normalize(input.normalWS);
                Light sun = GetMainLight();
                half3 lighting = SampleSH(normalWS) + sun.color * saturate(dot(normalWS, sun.direction));

                // Distance to the nearest street center line, on each axis.
                float2 local = abs(frac(input.groundMeters / _BlockSize) - 0.5) * _BlockSize;
                float2 fromStreet = _BlockSize * 0.5 - local;
                float street = min(fromStreet.x, fromStreet.y);

                half3 color = street < _StreetWidth * 0.5 ? _StreetColor.rgb : _BlockColor.rgb;
                // Dashed lane mark in the middle of each street.
                float2 dashes = frac(input.groundMeters / 6.0);
                bool laneX = fromStreet.x < 0.25 && dashes.y < 0.5;
                bool laneY = fromStreet.y < 0.25 && dashes.x < 0.5;
                if (laneX || laneY)
                {
                    color = _LineColor.rgb;
                }

                color = MixFog(color * lighting, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
