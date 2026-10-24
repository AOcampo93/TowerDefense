// Asphalt, sidewalks and crosswalks of the city, painted with the texture of a road pack: one big atlas of road
// pieces. Every street is a long strip, and its vertices carry where its piece lies in the atlas (start of a tile, one
// tile along the street, across it), so the piece repeats along the street however long it is. The pack's daylight
// colors are dimmed to night and lit by the orange street lights of the street map, so the lane marks and the
// crosswalks catch the light. When a texel of the atlas gets smaller than a pixel the texture melts into the average
// color of its piece, so the far streets do not borrow colors from their neighbors in the atlas.
// The strips are drawn after the ground and the buildings without writing depth, in the order of the mesh: crossings,
// crosswalks and avenues simply paint over the streets laid before them.
Shader "AlienInvaders/Roads"
{
    Properties
    {
        _Atlas ("Road Atlas", 2D) = "gray" {}
        _Night ("Night Light", Color) = (0.16, 0.15, 0.24, 1)
        _LampColor ("Street Light", Color) = (1, 0.55, 0.15, 1)
        _LampGlow ("Street Light Strength", Range(0, 4)) = 1.6
        _LampFill ("Street Light Across The Street", Range(0, 1)) = 0.2
        _DetailFade ("Texels Per Pixel Where The Texture Melts (start, end)", Vector) = (3, 12, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+1" }

        Pass
        {
            Name "Roads"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CityMist.hlsl"

            // Set by CityGenerator once the streets are laid out (green = glow of the street lights).
            TEXTURE2D(_AI_CityMap);
            SAMPLER(sampler_AI_CityMap);
            float _AI_CityMapHalf;

            TEXTURE2D(_Atlas);
            SAMPLER(sampler_Atlas);

            CBUFFER_START(UnityPerMaterial)
                float4 _Atlas_TexelSize;
                half4 _Night;
                half4 _LampColor;
                half _LampGlow;
                half _LampFill;
                float4 _DetailFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 average : COLOR;
                float2 tile : TEXCOORD0;   // x = across the piece (0 to 1), y = tiles along the street
                float2 origin : TEXCOORD1; // atlas position of the start of a tile, on its first edge
                float2 along : TEXCOORD2;  // atlas step over one tile along the street
                float2 across : TEXCOORD3; // atlas step from the first edge of the piece to the second
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 average : COLOR;
                float2 tile : TEXCOORD0;
                float2 origin : TEXCOORD1;
                float2 along : TEXCOORD2;
                float2 across : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float3 cityPosition : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.average = input.average;
                output.tile = input.tile;
                output.origin = input.origin;
                output.along = input.along;
                output.across = input.across;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.cityPosition = CityPosition(TransformObjectToWorld(input.positionOS.xyz));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Position inside one tile of the piece, a hair inside its borders so neighbors never bleed in.
                float2 inTile = float2(input.tile.x, frac(input.tile.y)) * 0.98 + 0.01;
                float2 atlasUV = input.origin + input.along * inTile.y + input.across * inTile.x;
                // Screen steps of the unwrapped position: the jump from one tile to the next must not pick a tiny mip.
                float2 stepX = (input.along * ddx(input.tile.y) + input.across * ddx(input.tile.x)) * 0.98;
                float2 stepY = (input.along * ddy(input.tile.y) + input.across * ddy(input.tile.x)) * 0.98;
                half3 albedo = SAMPLE_TEXTURE2D_GRAD(_Atlas, sampler_Atlas, atlasUV, stepX, stepY).rgb;
                float texels = max(length(stepX), length(stepY)) * _Atlas_TexelSize.z;
                half far = saturate((texels - _DetailFade.x) / max(_DetailFade.y - _DetailFade.x, 0.001));
                albedo = lerp(albedo, input.average.rgb, far);

                // Night: a dim violet sky light, plus the orange street lights along the curbs and a little across.
                float2 mapUV = input.cityPosition.xz / (2.0 * max(_AI_CityMapHalf, 1.0)) + 0.5;
                half lampLine = SAMPLE_TEXTURE2D(_AI_CityMap, sampler_AI_CityMap, mapUV).g;
                half3 light = _Night.rgb + _LampColor.rgb * _LampGlow * (_LampFill + lampLine);
                half3 color = albedo * light;

                color = ApplyCityMist(color, input.cityPosition);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
