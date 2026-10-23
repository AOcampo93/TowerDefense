// Sunset sky for the tower: a gradient from a dark violet zenith to a burning orange horizon, a smoky band below
// the horizon and a low sun with a wide glow. Computed per pixel from the view direction, so it needs no texture
// and stays sharp at any resolution.
Shader "AlienInvaders/SunsetSky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.1, 0.07, 0.18, 1)
        _HorizonColor ("Horizon", Color) = (0.95, 0.42, 0.18, 1)
        _BelowColor ("Below The Horizon", Color) = (0.2, 0.09, 0.06, 1)
        _SunColor ("Sun", Color) = (1, 0.82, 0.55, 1)
        _SunDirection ("Direction Towards The Sun", Vector) = (-0.99, 0.14, 0, 0)
        _SunSize ("Sun Size", Range(0.0005, 0.05)) = 0.004
        _SunGlow ("Sun Glow Tightness", Range(1, 64)) = 10
        _HorizonSharpness ("Horizon Height", Range(0.5, 8)) = 2.5
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

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _BelowColor;
                half4 _SunColor;
                float4 _SunDirection;
                float _SunSize;
                float _SunGlow;
                float _HorizonSharpness;
            CBUFFER_END

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

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.direction);
                float height = direction.y;

                // Above the horizon: orange near it, violet overhead. Below: a dark band of smoke over the city.
                half3 sky = height >= 0.0
                    ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(height), 1.0 / _HorizonSharpness))
                    : lerp(_HorizonColor.rgb, _BelowColor.rgb, saturate(-height * 6.0));

                float towardsSun = dot(direction, normalize(_SunDirection.xyz));
                sky += _SunColor.rgb * pow(saturate(towardsSun), _SunGlow) * 0.7;
                sky += _SunColor.rgb * smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.5, towardsSun) * 1.5;
                return half4(sky, 1.0h);
            }
            ENDHLSL
        }
    }
}
