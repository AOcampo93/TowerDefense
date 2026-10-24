// Hull of the alien starfighters (models of the Starfighter Set 01 pack, whose own shader is for the old built-in
// pipeline). Like the pack's shader, a mask paints two custom colors over the base texture (green channel = first
// color, blue = second, red = light trim), so every ship type gets the alien palette without new textures. Light is
// kept cheap for the headset: the sun, the sky's ambient light with the texture's baked occlusion, a soft highlight
// on the metal parts, the glowing lights of the emission map and a soft rim of light so the dark hulls stand out
// against the city below.
Shader "AlienInvaders/Starfighter"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _MaskMap ("Color Mask (G = color 1, B = color 2, R = trim)", 2D) = "black" {}
        _Color1 ("Color 1", Color) = (0.45, 0.25, 0.85, 1)
        _Color2 ("Color 2", Color) = (1, 0.3, 0.75, 1)
        _MetalMap ("Metal (R = metal, G = gloss, B = occlusion)", 2D) = "white" {}
        _EmissionMap ("Lights", 2D) = "black" {}
        _EmissionColor ("Light Color", Color) = (1, 1, 1, 1)
        _EmissionStrength ("Light Strength", Range(0, 8)) = 3
        _Shine ("Highlight Strength", Range(0, 2)) = 0.6
        _RimColor ("Rim Light", Color) = (0.5, 0.3, 0.9, 1)
        _RimPower ("Rim Sharpness", Range(1, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Starfighter"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);
            TEXTURE2D(_MetalMap);
            TEXTURE2D(_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color1;
                half4 _Color2;
                half4 _EmissionColor;
                half _EmissionStrength;
                half _Shine;
                half4 _RimColor;
                half _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, input.uv).rgb;
                half3 metal = SAMPLE_TEXTURE2D(_MetalMap, sampler_BaseMap, input.uv).rgb;
                half3 lights = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv).rgb;
                // Same mix as the pack's own shader.
                half3 albedo = saturate(lerp(base, _Color1.rgb, mask.g) * lerp(base, _Color2.rgb, mask.b) + 0.25 * mask.r);

                float3 normal = normalize(input.normalWS);
                float3 view = normalize(input.viewWS);
                Light sun = GetMainLight();
                half diffuse = saturate(dot(normal, sun.direction));
                half3 color = albedo * (SampleSH(normal) * metal.b + sun.color * diffuse);
                // A soft highlight where the metal catches the sun.
                float3 halfway = normalize(sun.direction + view);
                half highlight = pow(saturate(dot(normal, halfway)), 8.0 + 56.0 * metal.g) * metal.g;
                color += sun.color * highlight * _Shine * lerp(0.3h, albedo, metal.r);
                color += lights * _EmissionColor.rgb * _EmissionStrength;
                color += _RimColor.rgb * pow(1.0h - saturate(dot(normal, view)), _RimPower);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
