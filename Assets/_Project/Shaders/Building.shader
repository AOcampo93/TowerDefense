// Building that reads as a building: concrete walls and gravel roofs (textures sized in meters), lit by the sun,
// with a grid of windows (a few lit, a few on fire and flickering). The windows are computed from the object's own space in meters, so boxes of any
// size get the right number of floors and the pattern stays glued to the walls while the city leans.
Shader "AlienInvaders/Building"
{
    Properties
    {
        _BaseColor ("Wall Tint", Color) = (0.24, 0.22, 0.22, 1)
        _RoofColor ("Roof Tint", Color) = (0.12, 0.11, 0.11, 1)
        [NoScaleOffset] _WallTex ("Wall Texture", 2D) = "white" {}
        [NoScaleOffset] _RoofTex ("Roof Texture", 2D) = "white" {}
        _WallTile ("Wall Texture Size (meters)", Float) = 8
        _RoofTile ("Roof Texture Size (meters)", Float) = 12
        _WindowColor ("Dark Window", Color) = (0.04, 0.05, 0.07, 1)
        _LitColor ("Lit Window", Color) = (1, 0.68, 0.32, 1)
        _FireColor ("Burning Window", Color) = (1, 0.32, 0.06, 1)
        _CellSize ("Window Cell (width, height in meters)", Vector) = (2.5, 3.2, 0, 0)
        _WindowFill ("Window Share Of The Cell (x, y)", Vector) = (0.55, 0.5, 0, 0)
        _UseVertexColor ("Tint Each Building With Its Vertex Color", Range(0, 1)) = 0
        _LitChance ("Lit Windows", Range(0, 1)) = 0.25
        _FireChance ("Burning Windows", Range(0, 1)) = 0.04
        _DetailFade ("Windows Fade Into Average (start, end meters)", Vector) = (300, 600, 0, 0)
        _NeonChance ("Neon Signs", Range(0, 1)) = 0
        _NeonA ("Neon A", Color) = (1, 0.2, 0.7, 1)
        _NeonB ("Neon B", Color) = (0.2, 0.85, 1, 1)
        _NeonC ("Neon C", Color) = (0.65, 0.3, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Building"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WallTex);
            SAMPLER(sampler_WallTex);
            TEXTURE2D(_RoofTex);
            SAMPLER(sampler_RoofTex);

            CBUFFER_START(UnityPerMaterial)
                float _WallTile;
                float _RoofTile;
                half4 _BaseColor;
                half4 _RoofColor;
                half4 _WindowColor;
                half4 _LitColor;
                half4 _FireColor;
                float4 _CellSize;
                float4 _WindowFill;
                half _LitChance;
                half _FireChance;
                half _UseVertexColor;
                float4 _DetailFade;
                half _NeonChance;
                half4 _NeonA;
                half4 _NeonB;
                half4 _NeonC;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 tint : COLOR;
                float3 positionMeters : TEXCOORD0; // object space scaled to meters, base of the box at y = 0
                float3 normalOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 scale : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float distance : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21),
                                      length(UNITY_MATRIX_M._m02_m12_m22));
                output.scale = scale;
                // Merged city meshes carry one color per building, so the blocks are not all the same white.
                output.tint = lerp(half3(1.0, 1.0, 1.0), input.color.rgb, _UseVertexColor);
                output.positionMeters = (input.positionOS.xyz + float3(0.0, 0.5, 0.0)) * scale;
                output.normalOS = input.normalOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.distance = distance(TransformObjectToWorld(input.positionOS.xyz), GetCameraPositionWS());
                return output;
            }

            // Cheap stable random number for a window cell (same cell = same number every frame).
            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normalWS = normalize(input.normalWS);
                Light sun = GetMainLight();
                half3 lighting = SampleSH(normalWS) + sun.color * saturate(dot(normalWS, sun.direction));

                half3 color;
                if (abs(input.normalOS.y) > 0.5)
                {
                    half3 roof = SAMPLE_TEXTURE2D(_RoofTex, sampler_RoofTex, input.positionMeters.xz / _RoofTile).rgb;
                    color = _RoofColor.rgb * input.tint * roof * lighting;
                }
                else
                {
                    // Horizontal coordinate along this wall, vertical coordinate from the base: floors and columns.
                    float3 along = normalize(cross(float3(0.0, 1.0, 0.0), input.normalOS));
                    float2 grid = float2(dot(input.positionMeters, along), input.positionMeters.y) / _CellSize.xy;
                    float2 cell = floor(grid);
                    float2 inCell = abs(frac(grid) - 0.5);
                    bool window = inCell.x < _WindowFill.x * 0.5 && inCell.y < _WindowFill.y * 0.5;

                    half3 wall = SAMPLE_TEXTURE2D(_WallTex, sampler_WallTex, float2(grid.x * _CellSize.x, input.positionMeters.y) / _WallTile).rgb;
                    color = _BaseColor.rgb * input.tint * wall * lighting;
                    // Far away the window grid would shimmer on the headset: blend it into its average color.
                    half area = _WindowFill.x * _WindowFill.y;
                    half3 windowAverage = _WindowColor.rgb * lighting * (1.0 - _LitChance - _FireChance)
                                        + _LitColor.rgb * _LitChance * 0.8 + _FireColor.rgb * _FireChance;
                    half3 farColor = lerp(color, windowAverage, area);
                    float fade = saturate((input.distance - _DetailFade.x) / max(1.0, _DetailFade.y - _DetailFade.x));
                    if (window)
                    {
                        // The box size makes every building's pattern different without per-object data.
                        float random = Hash(float3(cell, dot(input.normalOS, float3(1.0, 2.0, 3.0))) + input.scale);
                        color = _WindowColor.rgb * lighting;
                        if (random > 1.0 - _FireChance)
                        {
                            half flicker = 0.65 + 0.35 * sin(_Time.y * (9.0 + random * 7.0) + random * 40.0);
                            color = _FireColor.rgb * flicker * 1.6;
                        }
                        else if (random < _LitChance)
                        {
                            color = _LitColor.rgb * (0.6 + 0.4 * frac(random * 13.7));
                        }
                    }
                    color = lerp(color, farColor, fade);

                    // Neon signs: short glowing bands across some facades, four windows wide, near a floor's top.
                    float2 signCell = float2(floor(grid.x / 4.0), cell.y);
                    float signRandom = Hash(float3(signCell, 7.0 + dot(input.normalOS, float3(1.0, 2.0, 3.0))) + input.scale);
                    float inFloor = frac(grid.y);
                    if (signRandom < _NeonChance && inFloor > 0.84 && inFloor < 0.96)
                    {
                        half3 neon = signRandom < _NeonChance * 0.33 ? _NeonA.rgb : (signRandom < _NeonChance * 0.66 ? _NeonB.rgb : _NeonC.rgb);
                        color = neon * 1.4;
                    }
                }

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
