// Sky from a 360 photo (equirectangular image). It turns slowly so the clouds drift, it leans with the city when the
// tower leans, and near the horizon and below it it fades into the fog color, so the hazy city and the sky meet
// without a visible edge.
Shader "AlienInvaders/PanoramaSky"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama (equirectangular)", 2D) = "grey" {}
        _Exposure ("Exposure", Range(0.2, 2)) = 1
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _HazeHeight ("Haze Height (sine of the elevation)", Range(0.01, 0.5)) = 0.08
        _HorizonOffset ("Horizon Offset (moves the picture up or down, share of its height)", Range(-0.25, 0.25)) = 0
        _Rotation ("Turn (share of a full turn, to put the sun where the game needs it)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half _Exposure;
                half4 _Tint;
                float _HazeHeight;
                float _HorizonOffset;
                float _Rotation;
            CBUFFER_END

            // Set every frame by SkyController (globals, so the material asset is never modified at runtime).
            float _AI_SkyRotation;
            // Quaternion that turns a view direction back into the level sky while the world leans. Never set (all
            // zeros) it leaves the direction as it is, so the sky is level in the editor too.
            float4 _AI_SkyTilt;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float3 Rotate(float4 rotation, float3 direction)
            {
                return direction + 2.0 * cross(rotation.xyz, cross(rotation.xyz, direction) + rotation.w * direction);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = Rotate(_AI_SkyTilt, normalize(input.direction));

                // Compass angle from +Z towards +X and elevation, mapped onto the panorama (+Z is its center). Painted
                // panoramas do not always have their horizon in the middle, so the picture can be moved up or down.
                float azimuth = atan2(direction.x, direction.z);
                float2 uv = float2(azimuth / (2.0 * PI) + 0.5 + _AI_SkyRotation, asin(direction.y) / PI + 0.5 + _HorizonOffset);
                uv.x += _Rotation;
                // Level 0 always: a mip jump at the seam of the panorama would draw a thin line.
                half3 sky = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rgb * _Tint.rgb * _Exposure;

                // Haze: the photo fades into the fog color towards the horizon and is fully fog below it.
                half haze = 1.0 - smoothstep(0.0, _HazeHeight, direction.y);
                sky = lerp(sky, unity_FogColor.rgb, haze);
                return half4(sky, 1.0h);
            }
            ENDHLSL
        }
    }
}
