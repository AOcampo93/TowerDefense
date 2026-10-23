// Buildings drawn like a flat night city illustration: near black walls, strips of warm yellow and orange windows,
// neon outlines along the edges and the roof, glass towers glowing in their neon color, and the warm glow of the
// street lights climbing the lower floors. Each building gets its own style from its vertex color (r = neon color,
// g = random seed, b = neon outline on or off, a = glass tower), so a merged city mesh still has varied buildings.
// The window grid and the outlines are measured in meters along each wall (UVs), so they stay glued to the walls
// while the city leans. When a window or an outline gets smaller than a few pixels it melts into its average color,
// so the far city does not shimmer on the headset. Low mist fills the streets between the buildings.
Shader "AlienInvaders/Building"
{
    Properties
    {
        _WallColor ("Wall", Color) = (0.035, 0.035, 0.07, 1)
        _WallLitColor ("Wall Facing The Sun", Color) = (0.09, 0.07, 0.14, 1)
        _RoofColor ("Roof", Color) = (0.03, 0.03, 0.05, 1)
        _WindowColor ("Dark Window", Color) = (0.07, 0.08, 0.15, 1)
        _LitColorA ("Lit Window (yellow)", Color) = (1, 0.86, 0.32, 1)
        _LitColorB ("Lit Window (orange)", Color) = (1, 0.55, 0.16, 1)
        _NeonA ("Neon (pink)", Color) = (1, 0.25, 0.65, 1)
        _NeonB ("Neon (cyan)", Color) = (0.25, 0.85, 1, 1)
        _NeonC ("Neon (yellow)", Color) = (1, 0.8, 0.25, 1)
        _NeonD ("Neon (orange)", Color) = (1, 0.45, 0.15, 1)
        _CellSize ("Window Cell (width, height in meters)", Vector) = (2.4, 3.2, 0, 0)
        _WindowFill ("Window Share Of The Cell (x, y)", Vector) = (0.82, 0.42, 0, 0)
        _LitChance ("Lit Window Strips", Range(0, 1)) = 0.45
        _EdgeWidth ("Neon Outline Width (meters)", Float) = 0.6
        _StreetGlow ("Street Light Glow On The Lower Floors", Range(0, 2)) = 0.8
        _StreetGlowHeight ("Street Light Glow Height (meters)", Float) = 30
        _GlassGlow ("Glow Of Glass Towers", Range(0, 1)) = 0.35
        _DetailFade ("Windows Fade Into Average (start, end meters)", Vector) = (300, 600, 0, 0)
        _UseVertexColor ("Style From The Vertex Color", Range(0, 1)) = 1
        _Style ("Style Otherwise (neon color, seed, outline, glass)", Vector) = (0.1, 0.5, 1, 0)
        _UVInMeters ("UVs Are Meters (merged city meshes); off for plain cubes", Range(0, 1)) = 1
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
            #include "CityMist.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WallColor;
                half4 _WallLitColor;
                half4 _RoofColor;
                half4 _WindowColor;
                half4 _LitColorA;
                half4 _LitColorB;
                half4 _NeonA;
                half4 _NeonB;
                half4 _NeonC;
                half4 _NeonD;
                float4 _CellSize;
                float4 _WindowFill;
                half _LitChance;
                float _EdgeWidth;
                half _StreetGlow;
                float _StreetGlowHeight;
                half _GlassGlow;
                float4 _DetailFade;
                half _UseVertexColor;
                half4 _Style;
                half _UVInMeters;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 style : COLOR;
                float2 faceUV : TEXCOORD0;   // meters across the face: along the wall and up from its base
                float2 faceSize : TEXCOORD1; // size of the face in meters
                float3 normalOS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                float distance : TEXCOORD5;
                float3 cityPosition : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                ZERO_INITIALIZE(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.style = lerp(_Style, input.color, _UseVertexColor);
                if (_UVInMeters > 0.5)
                {
                    // Merged city meshes carry the meters of each face in their UVs.
                    output.faceUV = input.uv0;
                    output.faceSize = input.uv1;
                }
                else
                {
                    // A plain cube: its UVs go from 0 to 1 on every face, so they are scaled by the object's size.
                    float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21),
                                          length(UNITY_MATRIX_M._m02_m12_m22));
                    float3 n = abs(input.normalOS);
                    float2 size = n.y > 0.5 ? scale.xz : float2(n.x > 0.5 ? scale.z : scale.x, scale.y);
                    output.faceSize = size;
                    output.faceUV = input.uv0 * size;
                }
                output.normalOS = input.normalOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.distance = distance(positionWS, GetCameraPositionWS());
                output.cityPosition = CityPosition(positionWS);
                return output;
            }

            // Cheap stable random number (same input = same number every frame).
            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half3 Neon(half pick)
            {
                return pick < 0.25 ? _NeonA.rgb : (pick < 0.5 ? _NeonB.rgb : (pick < 0.75 ? _NeonC.rgb : _NeonD.rgb));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                bool roof = abs(input.normalOS.y) > 0.5;
                half3 neon = Neon(input.style.r);
                half seed = input.style.g;
                bool outlined = input.style.b > 0.5;
                half glass = input.style.a;

                // Everything that needs screen derivatives is worked out before any branch.
                float2 grid = input.faceUV / _CellSize.xy;
                float2 cellPixels = 1.0 / max(fwidth(grid), 0.0001);
                // 0 when a window cell is under 3 pixels on screen, 1 from 8 pixels up.
                half detail = saturate((min(cellPixels.x, cellPixels.y) - 3.0) / 5.0);
                float2 fromStart = input.faceUV;
                float2 fromEnd = input.faceSize - input.faceUV;
                // Distance to the outline: the side edges and the top of a wall, all four edges of a roof.
                float edge = roof ? min(min(fromStart.x, fromEnd.x), min(fromStart.y, fromEnd.y))
                                  : min(min(fromStart.x, fromEnd.x), fromEnd.y);
                float edgeStep = max(fwidth(edge), 0.0001);

                // Flat shading like an illustration: only the faces turned to the sun are a touch lighter.
                Light sun = GetMainLight();
                half facing = saturate(dot(normalize(input.normalWS), sun.direction));
                half3 wall = lerp(_WallColor.rgb, _WallLitColor.rgb, facing);
                // Glass towers glow in their neon color.
                wall = lerp(wall, neon * _GlassGlow, glass * 0.7);
                half3 color = roof ? _RoofColor.rgb : wall;

                if (!roof)
                {
                    // Windows come in strips: each floor is cut into runs of a few windows, lit or dark together.
                    float2 cell = floor(grid);
                    float run = 2.0 + floor(Hash(float3(cell.y, seed * 61.0, 3.0)) * 5.0);
                    float strip = floor(cell.x / run);
                    float random = Hash(float3(strip, cell.y, seed * 97.0 + 11.0));
                    float2 inCell = abs(frac(grid) - 0.5);
                    bool window = inCell.x < _WindowFill.x * 0.5 && inCell.y < _WindowFill.y * 0.5;
                    half3 lit = lerp(_LitColorA.rgb, _LitColorB.rgb, frac(random * 7.3));
                    lit = lerp(lit, neon * 1.3, glass * 0.6);
                    half3 near = window ? (random < _LitChance ? lit : _WindowColor.rgb) : wall;
                    // Small on screen or far away, the windows become their average color.
                    half area = _WindowFill.x * _WindowFill.y;
                    half3 average = lerp(wall, lerp(_WindowColor.rgb, (_LitColorA.rgb + _LitColorB.rgb) * 0.5, _LitChance), area);
                    float fade = saturate((input.distance - _DetailFade.x) / max(1.0, _DetailFade.y - _DetailFade.x));
                    color = lerp(near, average, max(fade, 1.0 - detail));

                    // The street lights wash the lower floors in warm orange and pink.
                    float above = max(0.0, input.cityPosition.y - _AI_Mist.x);
                    half3 streetLight = lerp(_NeonD.rgb, _NeonA.rgb, seed);
                    color += streetLight * _StreetGlow * 0.5 * exp(-above / max(_StreetGlowHeight, 1.0));
                }

                if (outlined)
                {
                    // Antialiased neon line; it fades out when it would be thinner than a pixel.
                    half stroke = saturate((_EdgeWidth * 0.5 - edge) / edgeStep + 0.5);
                    stroke *= saturate(_EdgeWidth / edgeStep);
                    color = lerp(color, neon * 1.5, stroke);
                }

                color = ApplyCityMist(color, input.cityPosition);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
