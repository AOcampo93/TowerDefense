// Alien portal: a black hole in the sky with purple spiral arms that turn towards the center, brighter at the rim.
// Drawn from the disc's own coordinates, so it needs no texture.
Shader "AlienInvaders/PortalVortex"
{
    Properties
    {
        _CoreColor ("Core", Color) = (0.01, 0, 0.03, 1)
        _ArmColor ("Spiral Arms", Color) = (0.6, 0.15, 1, 1)
        _RimColor ("Rim", Color) = (0.9, 0.5, 1, 1)
        _Arms ("Arms", Float) = 3
        _Twist ("Twist", Float) = 9
        _Speed ("Turn Speed", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Vortex"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _ArmColor;
                half4 _RimColor;
                float _Arms;
                float _Twist;
                float _Speed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 disc : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // The disc is a flattened cylinder: its face spans x and z from -0.5 to 0.5.
                output.disc = input.positionOS.xz * 2.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radius = saturate(length(input.disc));
                float angle = atan2(input.disc.y, input.disc.x);

                // Spiral arms: bands whose angle shifts with the radius, turning over time.
                float spiral = sin(angle * _Arms + radius * _Twist - _Time.y * _Speed);
                float arms = smoothstep(0.35, 0.95, spiral) * smoothstep(0.1, 0.7, radius);
                float rim = smoothstep(0.82, 1.0, radius);

                half3 color = _CoreColor.rgb;
                color = lerp(color, _ArmColor.rgb, arms);
                color = lerp(color, _RimColor.rgb, rim);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
