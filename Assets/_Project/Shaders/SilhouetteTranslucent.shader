// The player's body as a faint glow: almost invisible inside, with a thin luminous blue edge where the surface
// turns away from the eyes, so the outline of the body shows without hiding the world. The edge adds light
// (premultiplied blending), like a hologram. Drawn after SilhouetteDepth has written the body's depth, so only the
// front surface is colored and the overlapping capsules never stack into brighter spots. Whatever is very close to
// the eyes (chest, shoulders, upper arms seen from above) fades out, so the body never blocks the view.
Shader "AlienInvaders/SilhouetteTranslucent"
{
    Properties
    {
        _BaseColor ("Body Tint", Color) = (0.2, 0.55, 1, 1)
        _BodyAlpha ("Body Opacity", Range(0, 1)) = 0.04
        _RimColor ("Edge Color", Color) = (0.3, 0.75, 1, 1)
        _RimIntensity ("Edge Glow", Range(0, 3)) = 1.1
        _RimAlpha ("Edge Opacity", Range(0, 1)) = 0.3
        _RimPower ("Edge Thinness", Range(0.5, 8)) = 4
        _FadeNear ("Fade Start (m from the eyes)", Float) = 0.3
        _FadeFar ("Fully Visible (m from the eyes)", Float) = 0.5
    }

    SubShader
    {
        // After the depth pass (queue 3005) and after other transparent objects.
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }
        Blend One OneMinusSrcAlpha
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
                half _RimIntensity;
                float _FadeNear;
                float _FadeFar;
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
                // Premultiplied: the faint tint and the edge glow add light, and only the edge dims the world behind.
                half3 color = _BaseColor.rgb * _BodyAlpha + _RimColor.rgb * (rim * _RimIntensity);
                half fade = smoothstep(_FadeNear, _FadeFar, length(input.viewDirWS));
                return half4(color, saturate(_BodyAlpha + rim * _RimAlpha)) * fade;
            }
            ENDHLSL
        }
    }
}
