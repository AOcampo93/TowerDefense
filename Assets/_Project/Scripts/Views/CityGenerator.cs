using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Builds the city 200 m below the player when the game loads, from a fixed seed (same city every time).
    /// The layout tries to look like a real city instead of a chess board: two diagonal avenues cut it into four
    /// districts, each district has its own street grid turned at its own angle, and the blocks are rectangles of
    /// irregular sizes split into lots of different widths. The streets are also painted into a map texture that the
    /// ground shader uses, so the asphalt and its orange street lights always match the buildings.
    /// Buildings come in three sizes: small ones everywhere, medium ones scattered, and a few tall ones. The player's
    /// tower always stays the tallest by a wide margin (no other building comes within 100 m of its top).
    /// Banks of low mist (drawn by the city shaders) drift between the small buildings.
    /// Buildings are merged into a few big meshes so the headset draws the whole city in a few dozen calls.
    /// It lives on an object that is active from the start, so the city is built while the game loads and not in
    /// the middle of the elevator ride (the city's own root stays hidden until VR).
    /// </summary>
    public class CityGenerator : MonoBehaviour
    {
        private static readonly int CityMapId = Shader.PropertyToID("_AI_CityMap");
        private static readonly int CityMapHalfId = Shader.PropertyToID("_AI_CityMapHalf");
        private static readonly int CityWorldToLocalId = Shader.PropertyToID("_AI_CityWorldToLocal");
        private static readonly int MistId = Shader.PropertyToID("_AI_Mist");
        private static readonly int MistColorId = Shader.PropertyToID("_AI_MistColor");
        private static readonly int MistNoiseId = Shader.PropertyToID("_AI_MistNoise");

        [Tooltip("Parent of everything generated (inside the leaning part of the world).")]
        [SerializeField] private Transform _root;

        [Header("Layout (meters, relative to the player platform)")]
        [SerializeField] private int _seed = 2026;
        [SerializeField] private float _groundTop = -200f;
        [SerializeField] private float _cityRadius = 700f;
        [Tooltip("No regular buildings this close to the tower axis (a small plaza around its base).")]
        [SerializeField] private float _plazaRadius = 15f;
        [SerializeField] private float _emptyBlockChance = 0.04f;
        [Tooltip("Size of the square areas merged into one mesh. Bigger = fewer draw calls, coarser culling.")]
        [SerializeField] private float _chunkSize = 240f;

        [Header("Districts and streets")]
        [Tooltip("Grid angle of each of the four districts between the two diagonal avenues (degrees).")]
        [SerializeField] private float[] _districtAngles = { 0f, 17f, -23f, 36f };
        [Tooltip("Length and depth range of the rectangular blocks.")]
        [SerializeField] private Vector2 _blockLength = new Vector2(55f, 130f);
        [SerializeField] private Vector2 _blockDepth = new Vector2(28f, 62f);
        [SerializeField] private float _streetWidth = 8f;
        [SerializeField] private float _wideStreetWidth = 13f;
        [SerializeField] private float _wideStreetChance = 0.25f;
        [SerializeField] private float _avenueWidth = 18f;
        [SerializeField] private float _sidewalk = 2f;
        [Tooltip("Pixels per side of the street map painted for the ground shader.")]
        [SerializeField] private int _mapResolution = 1536;
        [Tooltip("The map covers this many meters on each side of the tower.")]
        [SerializeField] private float _mapHalfSize = 800f;

        [Header("Heights")]
        [Tooltip("No building may come closer than this to the top of the player's tower.")]
        [SerializeField] private float _clearanceBelowTower = 100f;
        [Tooltip("Small buildings: most of the city.")]
        [SerializeField] private Vector2 _smallHeight = new Vector2(6f, 26f);
        [Tooltip("Share of medium buildings, scattered all over the city.")]
        [SerializeField] private float _mediumChance = 0.2f;
        [SerializeField] private Vector2 _mediumHeight = new Vector2(30f, 60f);
        [Tooltip("Share of tall buildings, scattered all over the city.")]
        [SerializeField] private float _tallChance = 0.06f;
        [SerializeField] private Vector2 _tallHeight = new Vector2(65f, 100f);

        [Header("Skyscrapers near the tower")]
        [SerializeField] private int _neighborTowers = 6;
        [SerializeField] private Vector2 _neighborDistance = new Vector2(60f, 260f);
        [Tooltip("Height of their roofs relative to the player (never above the clearance below the tower top).")]
        [SerializeField] private Vector2 _neighborRoof = new Vector2(-115f, -100f);
        [SerializeField] private float _beaconBlinkSeconds = 1.2f;

        [Header("Details")]
        [Tooltip("Rooftop machinery only where it can be seen.")]
        [SerializeField] private float _roofDetailRadius = 300f;
        [SerializeField] private GameObject _smokeColumnPrefab;
        [SerializeField] private int _smokeColumns = 6;
        [SerializeField] private Vector2 _smokeDistance = new Vector2(120f, 450f);

        [Header("Mist between the buildings")]
        [Tooltip("Height of the mist over the street (meters), about as high as the small buildings.")]
        [SerializeField] private float _mistTop = 22f;
        [SerializeField, Range(0f, 1f)] private float _mistStrength = 0.9f;
        [Tooltip("Speed of the drifting mist banks (meters per second).")]
        [SerializeField] private float _mistWind = 4f;
        [SerializeField] private Color _mistColor = new Color(0.5f, 0.4f, 0.56f);
        [Tooltip("Tileable noise that breaks the mist into banks.")]
        [SerializeField] private Texture2D _mistNoise;

        [Header("Materials")]
        [SerializeField] private Material _buildingMaterial;
        [SerializeField] private Material _neighborMaterial;
        [SerializeField] private Material _roofDetailMaterial;
        [SerializeField] private Material _beaconMaterial;

        // Geometry being collected for one merged mesh.
        private class MeshBuffer
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<int> Triangles = new List<int>();
        }

        // A straight avenue across the whole city: the points p where dot(Normal, p - Point) = 0.
        private struct Avenue
        {
            public Vector2 Point;
            public Vector2 Normal;
        }

        // One district: a street grid turned by Angle. Lines are street center lines in the district's own frame.
        private class District
        {
            public float Angle;
            public readonly List<float> XLines = new List<float>();
            public readonly List<float> XWidths = new List<float>();
            public readonly List<float> ZLines = new List<float>();
            public readonly List<float> ZWidths = new List<float>();
        }

        // A building footprint: a rectangle turned by Angle around its Center (top view, meters).
        private struct Lot
        {
            public Vector2 Center;
            public Vector2 Half;
            public float Angle;
        }

        // Facade tints picked per building (the shader multiplies the dark concrete by them): night city blues,
        // violets and charcoal, like the cyberpunk reference images.
        private static readonly Color[] Palette =
        {
            new Color(0.55f, 0.58f, 0.68f),
            new Color(0.5f, 0.46f, 0.6f),
            new Color(0.42f, 0.44f, 0.5f),
            new Color(0.62f, 0.6f, 0.66f),
            new Color(0.45f, 0.5f, 0.62f),
            new Color(0.6f, 0.52f, 0.5f)
        };

        private readonly List<Renderer> _beacons = new List<Renderer>();
        private readonly Avenue[] _avenues = new Avenue[2];
        private readonly District[] _districts = new District[4];
        // Streets that may cover one pixel of the street map: distance to their center line and half width.
        private readonly float[] _streetDistance = new float[6];
        private readonly float[] _streetHalfWidth = new float[6];
        private int _streetCount;
        private System.Random _random;
        private float _beaconTimer;

        private void Awake()
        {
            Generate();
            Shader.SetGlobalVector(MistId, new Vector4(_groundTop, _mistTop, _mistStrength, _mistWind));
            Shader.SetGlobalColor(MistColorId, _mistColor);
            Shader.SetGlobalTexture(MistNoiseId, _mistNoise);
        }

        private void LateUpdate()
        {
            // The mist belongs to the city, so it leans and drops with it.
            Shader.SetGlobalMatrix(CityWorldToLocalId, _root.worldToLocalMatrix);
        }

        private void Update()
        {
            // Aircraft warning lights on the skyscrapers: a slow on/off blink, a classic sign of great height.
            _beaconTimer += Time.deltaTime;
            bool on = Mathf.Repeat(_beaconTimer, _beaconBlinkSeconds) < _beaconBlinkSeconds * 0.5f;
            if (_beacons.Count > 0 && _beacons[0].enabled != on)
            {
                foreach (Renderer beacon in _beacons)
                {
                    beacon.enabled = on;
                }
            }
        }

        /// <summary>Lays out the streets, then creates the buildings, the skyscrapers, the smoke and the street map.</summary>
        public void Generate()
        {
            _random = new System.Random(_seed);
            PlanStreets();
            List<Lot> lots = PlanLots();
            HashSet<int> skyscrapers = PickSkyscrapers(lots);

            var chunks = new Dictionary<Vector2Int, MeshBuffer>();
            var roofChunks = new Dictionary<Vector2Int, MeshBuffer>();
            var roofs = new List<Vector3>();
            for (int i = 0; i < lots.Count; i++)
            {
                Lot lot = lots[i];
                if (skyscrapers.Contains(i))
                {
                    CreateSkyscraper(lot);
                    continue;
                }

                var chunk = new Vector2Int(Mathf.FloorToInt(lot.Center.x / _chunkSize), Mathf.FloorToInt(lot.Center.y / _chunkSize));
                float roofY = _groundTop + BuildingHeight(lot);
                Color tint = Palette[_random.Next(Palette.Length)] * Lerp(0.9f, 1.05f, Next());
                AddOrientedBox(Buffer(chunks, chunk), lot, _groundTop, roofY, tint);
                roofs.Add(new Vector3(lot.Center.x, roofY, lot.Center.y));
                if (lot.Center.magnitude < _roofDetailRadius)
                {
                    AddRoofDetails(Buffer(roofChunks, chunk), lot, roofY);
                }
            }

            foreach (KeyValuePair<Vector2Int, MeshBuffer> pair in chunks)
            {
                CreateMeshObject($"Buildings_{pair.Key.x}_{pair.Key.y}", pair.Value, _buildingMaterial);
            }
            foreach (KeyValuePair<Vector2Int, MeshBuffer> pair in roofChunks)
            {
                if (pair.Value.Vertices.Count > 0)
                {
                    CreateMeshObject($"Rooftops_{pair.Key.x}_{pair.Key.y}", pair.Value, _roofDetailMaterial);
                }
            }
            PlaceSmoke(roofs);
            PaintStreetMap();
        }

        // ---------------------------------------------------------------- streets

        // Two diagonal avenues through the middle of the city, and an irregular grid for each of the four districts.
        private void PlanStreets()
        {
            float first = Lerp(25f, 40f, Next());
            float[] angles = { first, first + Lerp(70f, 110f, Next()) };
            for (int i = 0; i < 2; i++)
            {
                float radians = angles[i] * Mathf.Deg2Rad;
                _avenues[i] = new Avenue
                {
                    Point = new Vector2(Lerp(-80f, 80f, Next()), Lerp(-80f, 80f, Next())),
                    Normal = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians))
                };
            }

            float extent = _mapHalfSize * 1.5f;
            for (int d = 0; d < _districts.Length; d++)
            {
                var district = new District { Angle = _districtAngles[d % _districtAngles.Length] };
                FillLines(district.XLines, district.XWidths, extent, _blockLength);
                FillLines(district.ZLines, district.ZWidths, extent, _blockDepth);
                _districts[d] = district;
            }
        }

        // Street center lines from -extent to +extent with irregular gaps; some streets are wider than others.
        private void FillLines(List<float> lines, List<float> widths, float extent, Vector2 gap)
        {
            float position = -extent - Lerp(0f, gap.y, Next());
            while (position < extent)
            {
                float width = Next() < _wideStreetChance ? _wideStreetWidth : _streetWidth;
                lines.Add(position);
                widths.Add(width);
                position += width + Lerp(gap.x, gap.y, Next());
            }
        }

        // Which of the four districts a point belongs to: the side of each avenue it is on.
        private int DistrictOf(Vector2 point)
        {
            return (AvenueSide(point, 0) > 0f ? 1 : 0) + (AvenueSide(point, 1) > 0f ? 2 : 0);
        }

        private float AvenueSide(Vector2 point, int avenue)
        {
            return Vector2.Dot(_avenues[avenue].Normal, point - _avenues[avenue].Point);
        }

        // ---------------------------------------------------------------- lots and buildings

        // Every block of every district is split into lots; lots that would stand on an avenue, outside the city or
        // in the plaza around the tower are dropped.
        private List<Lot> PlanLots()
        {
            var lots = new List<Lot>();
            for (int d = 0; d < _districts.Length; d++)
            {
                District district = _districts[d];
                for (int i = 0; i + 1 < district.XLines.Count; i++)
                {
                    for (int j = 0; j + 1 < district.ZLines.Count; j++)
                    {
                        float x0 = district.XLines[i] + district.XWidths[i] * 0.5f + _sidewalk;
                        float x1 = district.XLines[i + 1] - district.XWidths[i + 1] * 0.5f - _sidewalk;
                        float z0 = district.ZLines[j] + district.ZWidths[j] * 0.5f + _sidewalk;
                        float z1 = district.ZLines[j + 1] - district.ZWidths[j + 1] * 0.5f - _sidewalk;
                        Vector2 center = Rotate(new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f), district.Angle);
                        if (center.magnitude > _cityRadius + 80f || DistrictOf(center) != d || Next() < _emptyBlockChance)
                        {
                            continue;
                        }
                        SplitBlock(lots, d, x0, x1, z0, z1);
                    }
                }
            }
            return lots;
        }

        // Lots of different widths along the block, sometimes two rows deep.
        private void SplitBlock(List<Lot> lots, int d, float x0, float x1, float z0, float z1)
        {
            District district = _districts[d];
            float length = x1 - x0;
            float depth = z1 - z0;
            int rows = depth > 34f && Next() < 0.75f ? 2 : 1;
            for (int r = 0; r < rows; r++)
            {
                float rz0 = z0 + depth * r / rows;
                float rz1 = z0 + depth * (r + 1) / rows;
                float x = x0;
                while (x < x1 - 8f)
                {
                    float width = Mathf.Min(x1 - x, Lerp(10f, 28f, Next()));
                    float gap = Lerp(0.6f, 2.5f, Next());
                    float w = (width - gap) * Lerp(0.8f, 1f, Next());
                    float h = (rz1 - rz0 - gap) * Lerp(0.75f, 1f, Next());
                    var localCenter = new Vector2(x + width * 0.5f, (rz0 + rz1) * 0.5f);
                    var lot = new Lot
                    {
                        Center = Rotate(localCenter, district.Angle),
                        Half = new Vector2(w * 0.5f, h * 0.5f),
                        Angle = district.Angle
                    };
                    if (LotFits(lot, d))
                    {
                        lots.Add(lot);
                    }
                    x += width;
                }
            }
        }

        // A lot fits if all its corners are in its district, off the avenues, inside the city and out of the plaza.
        private bool LotFits(Lot lot, int district)
        {
            float clearance = _avenueWidth * 0.5f + _sidewalk;
            for (int c = 0; c < 4; c++)
            {
                var offset = new Vector2(c % 2 == 0 ? -lot.Half.x : lot.Half.x, c < 2 ? -lot.Half.y : lot.Half.y);
                Vector2 corner = lot.Center + Rotate(offset, lot.Angle);
                if (DistrictOf(corner) != district || corner.magnitude > _cityRadius || corner.magnitude < _plazaRadius ||
                    Mathf.Abs(AvenueSide(corner, 0)) < clearance || Mathf.Abs(AvenueSide(corner, 1)) < clearance)
                {
                    return false;
                }
            }
            return true;
        }

        // The biggest lots at the right distance become skyscrapers, spread apart from each other.
        private HashSet<int> PickSkyscrapers(List<Lot> lots)
        {
            var candidates = new List<int>();
            for (int i = 0; i < lots.Count; i++)
            {
                float distance = lots[i].Center.magnitude;
                if (distance >= _neighborDistance.x && distance <= _neighborDistance.y)
                {
                    candidates.Add(i);
                }
            }
            candidates.Sort((a, b) => (lots[b].Half.x * lots[b].Half.y).CompareTo(lots[a].Half.x * lots[a].Half.y));

            var picked = new HashSet<int>();
            foreach (int index in candidates)
            {
                if (picked.Count >= _neighborTowers)
                {
                    break;
                }
                bool farEnough = true;
                foreach (int other in picked)
                {
                    farEnough &= Vector2.Distance(lots[index].Center, lots[other].Center) > 70f;
                }
                if (farEnough)
                {
                    picked.Add(index);
                }
            }
            return picked;
        }

        // Three sizes: mostly small buildings, medium ones scattered, a few tall ones. Narrow lots stay small (a
        // 100 m pencil would look wrong).
        private float BuildingHeight(Lot lot)
        {
            float roll = Next();
            float narrowest = Mathf.Min(lot.Half.x, lot.Half.y) * 2f;
            float height;
            if (roll < _tallChance && narrowest > 14f)
            {
                height = Lerp(_tallHeight.x, _tallHeight.y, Next());
            }
            else if (roll < _tallChance + _mediumChance && narrowest > 10f)
            {
                height = Lerp(_mediumHeight.x, _mediumHeight.y, Next());
            }
            else
            {
                height = Lerp(_smallHeight.x, _smallHeight.y, Next());
            }
            return Mathf.Min(height, MaxHeight);
        }

        // Tallest a building may be: the street is 200 m down and the player's tower must win by the clearance.
        private float MaxHeight => -_groundTop - _clearanceBelowTower;

        // Air conditioners, water tanks and stair huts: small boxes that show how big a building really is.
        private void AddRoofDetails(MeshBuffer details, Lot roof, float roofY)
        {
            int count = (int)(Next() * 4f);
            for (int k = 0; k < count; k++)
            {
                var half = new Vector2(Lerp(0.75f, 2f, Next()), Lerp(0.75f, 2f, Next()));
                var local = new Vector2(Lerp(-roof.Half.x + half.x, roof.Half.x - half.x, Next()),
                                        Lerp(-roof.Half.y + half.y, roof.Half.y - half.y, Next()));
                var detail = new Lot { Center = roof.Center + Rotate(local, roof.Angle), Half = half, Angle = roof.Angle };
                AddOrientedBox(details, detail, roofY, roofY + Lerp(1f, 3.5f, Next()), Color.white);
            }
        }

        private void CreateSkyscraper(Lot lot)
        {
            float roof = Mathf.Min(Lerp(_neighborRoof.x, _neighborRoof.y, Next()), -_clearanceBelowTower);
            float height = roof - _groundTop;
            Quaternion turn = Quaternion.Euler(0f, lot.Angle, 0f);

            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Skyscraper";
            Destroy(tower.GetComponent<Collider>());
            tower.transform.SetParent(_root, false);
            tower.transform.localPosition = new Vector3(lot.Center.x, _groundTop + height * 0.5f, lot.Center.y);
            tower.transform.localRotation = turn;
            tower.transform.localScale = new Vector3(lot.Half.x * 2f, height, lot.Half.y * 2f);
            SetupRenderer(tower.GetComponent<MeshRenderer>(), _neighborMaterial);

            // Antenna with a red warning light on top.
            var antenna = GameObject.CreatePrimitive(PrimitiveType.Cube);
            antenna.name = "Antenna";
            Destroy(antenna.GetComponent<Collider>());
            antenna.transform.SetParent(_root, false);
            antenna.transform.localPosition = new Vector3(lot.Center.x, roof + 5f, lot.Center.y);
            antenna.transform.localScale = new Vector3(0.5f, 10f, 0.5f);
            SetupRenderer(antenna.GetComponent<MeshRenderer>(), _roofDetailMaterial);

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Beacon";
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(_root, false);
            beacon.transform.localPosition = new Vector3(lot.Center.x, roof + 10.3f, lot.Center.y);
            beacon.transform.localScale = Vector3.one * 1.2f;
            var beaconRenderer = beacon.GetComponent<MeshRenderer>();
            SetupRenderer(beaconRenderer, _beaconMaterial);
            _beacons.Add(beaconRenderer);
        }

        // Smoke rises from a few distant roofs (far from the ships, so it never hides them).
        private void PlaceSmoke(List<Vector3> roofs)
        {
            if (_smokeColumnPrefab == null)
            {
                return;
            }
            var candidates = new List<Vector3>();
            foreach (Vector3 roof in roofs)
            {
                float distance = new Vector2(roof.x, roof.z).magnitude;
                if (distance >= _smokeDistance.x && distance <= _smokeDistance.y)
                {
                    candidates.Add(roof);
                }
            }
            for (int i = 0; i < _smokeColumns && candidates.Count > 0; i++)
            {
                int index = _random.Next(candidates.Count);
                GameObject smoke = Instantiate(_smokeColumnPrefab, _root);
                smoke.transform.localPosition = candidates[index];
                // Farther columns are bigger, so they still read from 200 m up.
                smoke.transform.localScale = Vector3.one * 2f;
                candidates.RemoveAt(index);
            }
        }

        // ---------------------------------------------------------------- street map for the ground shader

        // Paints the streets into a texture: red = asphalt, green = the glow line of the street lights along each curb.
        private void PaintStreetMap()
        {
            int size = _mapResolution;
            var pixels = new Color32[size * size];
            float metersPerPixel = _mapHalfSize * 2f / size;
            // Each district's turn, precomputed: millions of pixels are turned into district coordinates.
            var cos = new float[_districts.Length];
            var sin = new float[_districts.Length];
            for (int d = 0; d < _districts.Length; d++)
            {
                cos[d] = Mathf.Cos(_districts[d].Angle * Mathf.Deg2Rad);
                sin[d] = Mathf.Sin(_districts[d].Angle * Mathf.Deg2Rad);
            }
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    var point = new Vector2((px + 0.5f) * metersPerPixel - _mapHalfSize, (py + 0.5f) * metersPerPixel - _mapHalfSize);
                    // Streets that may cover this pixel: both avenues and the two nearest lines of each district axis.
                    _streetCount = 0;
                    for (int a = 0; a < _avenues.Length; a++)
                    {
                        AddCandidate(Mathf.Abs(AvenueSide(point, a)), _avenueWidth * 0.5f);
                    }
                    int d = DistrictOf(point);
                    District district = _districts[d];
                    // Same as Rotate(point, -angle).
                    float localX = point.x * cos[d] - point.y * sin[d];
                    float localZ = point.x * sin[d] + point.y * cos[d];
                    NearestLines(district.XLines, district.XWidths, localX);
                    NearestLines(district.ZLines, district.ZWidths, localZ);
                    StreetAt(out float edge, out float lamp);

                    byte asphalt = (byte)(Mathf.Clamp01(0.5f - edge / metersPerPixel) * 255f);
                    pixels[py * size + px] = new Color32(asphalt, (byte)(lamp * 255f), 0, 255);
                }
            }

            var map = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "CityStreetMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            map.SetPixels32(pixels);
            map.Apply(true, true);
            Shader.SetGlobalTexture(CityMapId, map);
            Shader.SetGlobalFloat(CityMapHalfId, _mapHalfSize);
        }

        // Closest street line of a sorted list (binary search), and the one before it.
        private void NearestLines(List<float> lines, List<float> widths, float value)
        {
            int index = lines.BinarySearch(value);
            if (index < 0)
            {
                index = ~index;
            }
            for (int k = index - 1; k <= index; k++)
            {
                if (k >= 0 && k < lines.Count)
                {
                    AddCandidate(Mathf.Abs(value - lines[k]), widths[k] * 0.5f);
                }
            }
        }

        private void AddCandidate(float distance, float halfWidth)
        {
            _streetDistance[_streetCount] = distance;
            _streetHalfWidth[_streetCount] = halfWidth;
            _streetCount++;
        }

        // edge: signed distance to the nearest asphalt (negative inside a street). lamp: glow of the curb lights. The
        // lights of a street stop where it crosses another one, so no light lines run across the intersections.
        private void StreetAt(out float edge, out float lamp)
        {
            edge = float.MaxValue;
            lamp = 0f;
            for (int i = 0; i < _streetCount; i++)
            {
                float distance = _streetDistance[i];
                float halfWidth = _streetHalfWidth[i];
                edge = Mathf.Min(edge, distance - halfWidth);
                if (distance >= halfWidth + 1.5f)
                {
                    continue;
                }
                bool crossing = false;
                for (int j = 0; j < _streetCount; j++)
                {
                    crossing |= j != i && _streetDistance[j] < _streetHalfWidth[j] - 0.5f;
                }
                if (!crossing)
                {
                    lamp = Mathf.Max(lamp, Mathf.Exp(-Mathf.Abs(distance - halfWidth * 0.8f) * 2.5f));
                }
            }
        }

        // ---------------------------------------------------------------- meshes

        private void CreateMeshObject(string name, MeshBuffer buffer, Material material)
        {
            var mesh = new Mesh { name = name };
            if (buffer.Vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }
            mesh.SetVertices(buffer.Vertices);
            mesh.SetNormals(buffer.Normals);
            mesh.SetColors(buffer.Colors);
            mesh.SetTriangles(buffer.Triangles, 0);
            mesh.RecalculateBounds();

            var piece = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(_root, false);
            piece.GetComponent<MeshFilter>().sharedMesh = mesh;
            SetupRenderer(piece.GetComponent<MeshRenderer>(), material);
        }

        private static void SetupRenderer(MeshRenderer meshRenderer, Material material)
        {
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        // A box turned around the vertical axis, without its bottom face (never seen). Faces are listed clockwise
        // as seen from outside; turning the whole box keeps that order.
        private static void AddOrientedBox(MeshBuffer buffer, Lot lot, float yMin, float yMax, Color color)
        {
            int first = buffer.Vertices.Count;
            Quaternion turn = Quaternion.Euler(0f, lot.Angle, 0f);
            var center = new Vector3(lot.Center.x, 0f, lot.Center.y);
            Vector3 P(float x, float y, float z) => center + turn * new Vector3(x, 0f, z) + Vector3.up * y;
            float hx = lot.Half.x;
            float hz = lot.Half.y;

            AddQuad(buffer, Vector3.up, P(-hx, yMax, -hz), P(-hx, yMax, hz), P(hx, yMax, hz), P(hx, yMax, -hz));
            AddQuad(buffer, turn * Vector3.forward, P(hx, yMin, hz), P(hx, yMax, hz), P(-hx, yMax, hz), P(-hx, yMin, hz));
            AddQuad(buffer, turn * Vector3.back, P(-hx, yMin, -hz), P(-hx, yMax, -hz), P(hx, yMax, -hz), P(hx, yMin, -hz));
            AddQuad(buffer, turn * Vector3.right, P(hx, yMin, -hz), P(hx, yMax, -hz), P(hx, yMax, hz), P(hx, yMin, hz));
            AddQuad(buffer, turn * Vector3.left, P(-hx, yMin, hz), P(-hx, yMax, hz), P(-hx, yMax, -hz), P(-hx, yMin, -hz));
            for (int i = first; i < buffer.Vertices.Count; i++)
            {
                buffer.Colors.Add(color);
            }
        }

        private static void AddQuad(MeshBuffer buffer, Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = buffer.Vertices.Count;
            buffer.Vertices.Add(a);
            buffer.Vertices.Add(b);
            buffer.Vertices.Add(c);
            buffer.Vertices.Add(d);
            for (int i = 0; i < 4; i++)
            {
                buffer.Normals.Add(normal);
            }
            buffer.Triangles.Add(start);
            buffer.Triangles.Add(start + 1);
            buffer.Triangles.Add(start + 2);
            buffer.Triangles.Add(start);
            buffer.Triangles.Add(start + 2);
            buffer.Triangles.Add(start + 3);
        }

        private static MeshBuffer Buffer(Dictionary<Vector2Int, MeshBuffer> chunks, Vector2Int key)
        {
            if (!chunks.TryGetValue(key, out MeshBuffer buffer))
            {
                buffer = new MeshBuffer();
                chunks.Add(key, buffer);
            }
            return buffer;
        }

        // Turns a top-view point around the vertical axis like Quaternion.Euler(0, degrees, 0) does in 3D.
        private static Vector2 Rotate(Vector2 point, float degrees)
        {
            Vector3 turned = Quaternion.Euler(0f, degrees, 0f) * new Vector3(point.x, 0f, point.y);
            return new Vector2(turned.x, turned.z);
        }

        private float Next() => (float)_random.NextDouble();

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
