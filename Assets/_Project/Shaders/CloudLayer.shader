// A sheet of drifting clouds below the top of the tower, among the tallest buildings: seeing clouds under your feet
// is one of the strongest height cues. Two layers of the same tileable noise scroll at different speeds, so the
// shapes keep changing; far clouds melt into the haze.
Shader "AlienInvaders/CloudLayer"
{
    Properties
    {
        _NoiseTex ("Cloud Noise (tileable)", 2D) = "grey" {}
        _Color ("Lit Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.62, 0.68, 0.76, 1)
        _Scale ("Size Of One Noise Tile (meters)", Float) = 600
        _Wind ("Wind (meters per second, x and z)", Vector) = (3, 0, 1.5, 0)
        _Coverage ("Coverage", Range(0, 1)) = 0.45
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.18
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _ClearRadius ("Clear Radius Around The Tower (meters)", Float) = 60
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Clouds"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                half4 _Color;
                half4 _ShadowColor;
                float _Scale;
                float4 _Wind;
                half _Coverage;
                half _Softness;
                half _Opacity;
                float _ClearRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 meters : TEXCOORD0; // position on the sheet in meters (object space times scale)
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float2 scale = float2(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m02_m12_m22));
                output.meters = input.positionOS.xz * scale;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 drift = _Wind.xz * _Time.y;
                half a = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, (input.meters + drift) / _Scale).r;
                half b = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, (input.meters - drift * 0.6) / (_Scale * 0.45) + 0.37).r;
                half density = a * 0.65 + b * 0.35;

                half cloud = smoothstep(1.0 - _Coverage, 1.0 - _Coverage + _Softness, density);
                // A clearing around the tower keeps the view straight down open.
                cloud *= smoothstep(_ClearRadius * 0.6, _ClearRadius, length(input.meters));

                // Thick parts are bright, thin edges take the bluish shadow color.
                half3 color = lerp(_ShadowColor.rgb, _Color.rgb, saturate((density - (1.0 - _Coverage)) * 3.0));
                color = MixFog(color, input.fogFactor);
                return half4(color, cloud * _Opacity);
            }
            ENDHLSL
        }
    }
}
