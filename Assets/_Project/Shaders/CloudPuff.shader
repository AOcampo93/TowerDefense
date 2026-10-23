// One big cloud picture (a cell of a cloud atlas) for the clouds around the horizon. The picture carries
// its own shading (lit top, darker base); the vertex color tints it, and its base melts into the fog so the clouds
// rise out of the haze with no visible line where the city ends.
Shader "AlienInvaders/CloudPuff"
{
    Properties
    {
        _MainTex ("Cloud Atlas", 2D) = "white" {}
        _AtlasRows ("Rows Of The Atlas", Float) = 2
        _Brightness ("Brightness", Range(0.5, 2)) = 1.2
        _Haze ("Haze Everywhere", Range(0, 1)) = 0.2
        _BottomHaze ("Extra Haze Towards The Base", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-5" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudPuff"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Brightness;
                float _AtlasRows;
                half _Haze;
                half _BottomHaze;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 cloud = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // Height inside this picture's own cell of the atlas: 0 at the bottom, 1 at the top.
                half height = frac(input.uv.y * _AtlasRows);
                half3 color = cloud.rgb * input.color.rgb * _Brightness;
                color = lerp(color, unity_FogColor.rgb, saturate(_Haze + (1.0 - height) * _BottomHaze));
                return half4(color, cloud.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
