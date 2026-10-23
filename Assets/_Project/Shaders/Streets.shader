// Ground of the city seen from far above. Inside the city the streets come from the street map painted by
// CityGenerator (red = asphalt, green = the glow of the street lights), so they match the buildings, diagonal
// avenues included; beyond the map a plain grid takes over, deep in the haze. Blocks get their own shade and dots of
// lit windows (melted into their average glow when they get too small to draw cleanly). Low mist covers the streets
// in patches. Everything is in the ground's own meters, so it stays glued to the ground while the city leans.
Shader "AlienInvaders/Streets"
{
    Properties
    {
        _BlockColor ("City Block", Color) = (0.2, 0.18, 0.17, 1)
        _StreetColor ("Street", Color) = (0.07, 0.07, 0.08, 1)
        _LineColor ("Lane Mark", Color) = (0.55, 0.45, 0.25, 1)
        _LampColor ("Street Light", Color) = (1, 0.55, 0.18, 1)
        _LampGlow ("Street Light Strength", Range(0, 4)) = 0
        _DotChance ("Lit Windows On Far Blocks", Range(0, 1)) = 0
        _DotColor ("Lit Window Color", Color) = (1, 0.75, 0.4, 1)
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
            #include "CityMist.hlsl"

            // Set by CityGenerator once the streets are laid out.
            TEXTURE2D(_AI_CityMap);
            SAMPLER(sampler_AI_CityMap);
            float _AI_CityMapHalf;

            CBUFFER_START(UnityPerMaterial)
                half4 _BlockColor;
                half4 _StreetColor;
                half4 _LineColor;
                half4 _LampColor;
                half _LampGlow;
                half _DotChance;
                half4 _DotColor;
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
                float3 cityPosition : TEXCOORD3;
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
                output.cityPosition = CityPosition(TransformObjectToWorld(input.positionOS.xyz));
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(0.1031, 0.1030));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
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

                // Asphalt and street light glow: from the street map inside the city, from a plain grid beyond it.
                float2 mapUV = input.groundMeters / (2.0 * max(_AI_CityMapHalf, 1.0)) + 0.5;
                bool onMap = _AI_CityMapHalf > 0.0 && all(mapUV > 0.0) && all(mapUV < 1.0);
                half asphalt;
                half lampLine;
                if (onMap)
                {
                    half2 map = SAMPLE_TEXTURE2D(_AI_CityMap, sampler_AI_CityMap, mapUV).rg;
                    asphalt = map.r;
                    lampLine = map.g;
                }
                else
                {
                    float curb = _StreetWidth * 0.5;
                    asphalt = street < curb ? 1.0 : 0.0;
                    lampLine = street < curb + 1.0 ? exp(-abs(street - curb * 0.8) * 2.5) : 0.0;
                }

                // Blocks: a shade per 30 m patch, with dots of lit windows; streets: dark asphalt.
                float2 patch = floor(input.groundMeters / 30.0);
                half3 color = lerp(_BlockColor.rgb * lerp(0.7, 1.15, Hash(patch)), _StreetColor.rgb, asphalt);
                // Sodium street lights along the curbs, and their orange wash over the asphalt.
                half3 glow = _LampColor.rgb * _LampGlow * (0.08 * asphalt + lampLine);
                // Seen from far away, the lit windows of a block are dots of warm light. Dots smaller than a few pixels
                // would sparkle on the headset, so they melt into their average glow.
                float2 dotGrid = input.groundMeters / 4.0;
                half dotDetail = saturate((1.0 / max(max(fwidth(dotGrid.x), fwidth(dotGrid.y)), 0.0001) - 4.0) / 4.0);
                if (asphalt < 0.1)
                {
                    float2 inDot = frac(dotGrid);
                    half dotOn = Hash(floor(dotGrid) + 3.7) < _DotChance && all(abs(inDot - 0.5) < 0.22) ? 1.0 : 0.0;
                    half dot = lerp(_DotChance * 0.19, dotOn, dotDetail);
                    glow = lerp(glow, _DotColor.rgb * 0.9, dot);
                }
                // Dashed lane marks, only on the plain grid beyond the map (the map has its own street layout).
                float2 dashes = frac(input.groundMeters / 6.0);
                bool laneX = fromStreet.x < 0.25 && dashes.y < 0.5;
                bool laneY = fromStreet.y < 0.25 && dashes.x < 0.5;
                if (!onMap && (laneX || laneY))
                {
                    color = _LineColor.rgb;
                }

                // The street lights still glow through the mist, only softer.
                half mist = CityMist(input.cityPosition);
                color = lerp(color * lighting, _AI_MistColor.rgb, mist) + glow * (1.0 - 0.6 * mist);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
