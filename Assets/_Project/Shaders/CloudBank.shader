// A wall of clouds all around the horizon (drawn on the inside of a huge cylinder). It hides where the city ends,
// drifts slowly sideways, and is lit pink and orange on the side of the setting sun and violet everywhere else.
// No fog on it: it is the fog bank itself.
Shader "AlienInvaders/CloudBank"
{
    Properties
    {
        _NoiseTex ("Cloud Noise (tileable)", 2D) = "grey" {}
        _BaseColor ("Base Color", Color) = (0.32, 0.26, 0.48, 1)
        _TopColor ("Top Color", Color) = (0.62, 0.48, 0.78, 1)
        _SunColor ("Sun Side Color", Color) = (1, 0.62, 0.5, 1)
        _SunAzimuth ("Sun Compass Direction (degrees)", Float) = 117
        _Repeats ("Noise Repeats Around", Float) = 6
        _Speed ("Drift (turns per second)", Float) = 0.002
        _Coverage ("Coverage", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudBank"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half4 _BaseColor;
                half4 _TopColor;
                half4 _SunColor;
                float _SunAzimuth;
                float _Repeats;
                float _Speed;
                half _Coverage;
            CBUFFER_END

            // Same global as the sky: the bank turns with it so the sun side stays lit.
            float _AI_SkyRotation;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float azimuth : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.azimuth = degrees(atan2(input.positionOS.x, input.positionOS.z));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // u goes once around the horizon, v from the bottom (0) to the top (1) of the bank.
                float u = input.uv.x * _Repeats + _Time.y * _Speed * _Repeats;
                half big = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(u, input.uv.y * 0.6)).r;
                half small = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(u * 2.7 + 0.3, input.uv.y * 1.6)).r;
                half density = big * 0.7 + small * 0.3;

                // Solid at the bottom, a billowing edge near the top that the noise cuts into puffs.
                half profile = 1.0 - smoothstep(0.35, 1.0, input.uv.y);
                half alpha = saturate(profile * 1.6 + (density - (1.0 - _Coverage)) * 2.0 - input.uv.y);
                alpha *= smoothstep(1.0, 0.85, input.uv.y);

                // Brighter, pinker tops facing the sun.
                float sunAzimuth = _SunAzimuth - _AI_SkyRotation * 360.0;
                half facing = saturate(cos(radians(input.azimuth - sunAzimuth)));
                half3 top = lerp(_TopColor.rgb, _SunColor.rgb, facing * facing);
                half3 color = lerp(_BaseColor.rgb, top, saturate(input.uv.y * 1.4 + (density - 0.5)));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
