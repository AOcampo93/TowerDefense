using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Builds the city 200 m below the player when the game loads, from a fixed seed (same city every time).
    /// Vertigo comes from three things the eye can measure: thousands of buildings shrinking towards the horizon,
    /// a few skyscrapers next to the tower whose facades fall all the way to the street, and rooftop details close
    /// enough to show the scale. Buildings follow the street grid of the ground (blocks of 40 m), are taller near
    /// the tower, and are merged into a few big meshes so the headset draws the whole city in a few dozen calls.
    /// It lives on an object that is active from the start, so the city is built while the game loads and not in
    /// the middle of the elevator ride (the city's own root stays hidden until VR).
    /// </summary>
    public class CityGenerator : MonoBehaviour
    {
        [Tooltip("Parent of everything generated (inside the leaning part of the world).")]
        [SerializeField] private Transform _root;

        [Header("Layout (meters, relative to the player platform)")]
        [SerializeField] private int _seed = 2026;
        [SerializeField] private float _groundTop = -200f;
        [Tooltip("Must match the street grid of the ground material.")]
        [SerializeField] private float _blockSize = 40f;
        [SerializeField] private float _streetWidth = 9f;
        [SerializeField] private float _sidewalk = 2f;
        [SerializeField] private float _cityRadius = 850f;
        [Tooltip("No regular buildings this close to the tower axis (a small plaza around its base).")]
        [SerializeField] private float _plazaRadius = 15f;
        [SerializeField] private float _emptyBlockChance = 0.06f;
        [Tooltip("Blocks per side of each merged mesh. Bigger chunks = fewer draw calls, coarser culling.")]
        [SerializeField] private int _chunkBlocks = 6;

        [Header("Heights")]
        [SerializeField] private float _tallestNearTower = 140f;
        [SerializeField] private float _tallestAtEdge = 25f;
        [SerializeField] private float _outlierChance = 0.05f;

        [Header("Skyscrapers next to the tower")]
        [SerializeField] private int _neighborTowers = 5;
        [SerializeField] private Vector2 _neighborDistance = new Vector2(35f, 80f);
        [Tooltip("Height of their roofs relative to the player (always below the ships' flight zone).")]
        [SerializeField] private Vector2 _neighborRoof = new Vector2(-45f, -18f);
        [SerializeField] private Vector2 _neighborWidth = new Vector2(14f, 20f);
        [SerializeField] private float _beaconBlinkSeconds = 1.2f;

        [Header("Details")]
        [Tooltip("Rooftop machinery only where it can be seen.")]
        [SerializeField] private float _roofDetailRadius = 300f;
        [SerializeField] private GameObject _smokeColumnPrefab;
        [SerializeField] private int _smokeColumns = 6;
        [SerializeField] private Vector2 _smokeDistance = new Vector2(120f, 450f);

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

        // Facade tints picked per building (the shader multiplies the concrete texture by them).
        private static readonly Color[] Palette =
        {
            new Color(1f, 1f, 1f),
            new Color(0.97f, 0.93f, 0.85f),
            new Color(0.84f, 0.86f, 0.88f),
            new Color(0.8f, 0.86f, 0.95f),
            new Color(0.92f, 0.86f, 0.76f),
            new Color(0.68f, 0.7f, 0.74f)
        };

        private readonly List<Renderer> _beacons = new List<Renderer>();
        private System.Random _random;
        private float _beaconTimer;

        private void Awake()
        {
            Generate();
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

        /// <summary>Creates every building, the neighbor skyscrapers and the smoke columns.</summary>
        public void Generate()
        {
            _random = new System.Random(_seed);
            var neighbors = PlaceNeighborTowers();
            var chunks = new Dictionary<Vector2Int, MeshBuffer>();
            var roofChunks = new Dictionary<Vector2Int, MeshBuffer>();
            var roofs = new List<Vector3>();

            int blocks = Mathf.CeilToInt(_cityRadius / _blockSize);
            for (int bx = -blocks; bx < blocks; bx++)
            {
                for (int bz = -blocks; bz < blocks; bz++)
                {
                    // Streets run along multiples of the block size, so the tower stands on a crossing.
                    var blockMin = new Vector2(bx * _blockSize, bz * _blockSize);
                    Vector2 center = blockMin + Vector2.one * (_blockSize * 0.5f);
                    if (center.magnitude > _cityRadius || Next() < _emptyBlockChance)
                    {
                        continue;
                    }
                    var chunk = new Vector2Int(Mathf.FloorToInt((float)bx / _chunkBlocks), Mathf.FloorToInt((float)bz / _chunkBlocks));
                    FillBlock(blockMin, neighbors, Buffer(chunks, chunk), Buffer(roofChunks, chunk), roofs);
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
            foreach (Rect footprint in neighbors)
            {
                CreateNeighborTower(footprint);
            }
            PlaceSmoke(roofs);
        }

        // Uses a block for one big building or splits it into 2 x 2 lots, one building on each.
        private void FillBlock(Vector2 blockMin, List<Rect> neighbors, MeshBuffer buildings, MeshBuffer details, List<Vector3> roofs)
        {
            float margin = _streetWidth * 0.5f + _sidewalk;
            float inner = _blockSize - margin * 2f;
            int split = Next() < 0.35 ? 1 : 2;
            float lot = inner / split;

            for (int i = 0; i < split; i++)
            {
                for (int j = 0; j < split; j++)
                {
                    float gap = Lerp(0.6f, 2.5f, Next());
                    float width = (lot - gap) * Lerp(0.7f, 1f, Next());
                    float depth = (lot - gap) * Lerp(0.7f, 1f, Next());
                    var lotMin = new Vector2(blockMin.x + margin + i * lot, blockMin.y + margin + j * lot);
                    var footprint = new Rect(lotMin.x + (lot - width) * 0.5f, lotMin.y + (lot - depth) * 0.5f, width, depth);
                    float distance = footprint.center.magnitude;
                    if (distance < _plazaRadius || Overlaps(footprint, neighbors))
                    {
                        continue;
                    }

                    float height = BuildingHeight(distance);
                    var min = new Vector3(footprint.xMin, _groundTop, footprint.yMin);
                    var max = new Vector3(footprint.xMax, _groundTop + height, footprint.yMax);
                    Color tint = Palette[_random.Next(Palette.Length)] * Lerp(0.9f, 1.05f, Next());
                    AddBox(buildings, min, max, tint);
                    roofs.Add(new Vector3(footprint.center.x, max.y, footprint.center.y));

                    if (distance < _roofDetailRadius)
                    {
                        AddRoofDetails(details, footprint, max.y);
                    }
                }
            }
        }

        // Taller downtown around the tower, lower towards the edge, with a few tall exceptions.
        private float BuildingHeight(float distance)
        {
            float tallest = Lerp(_tallestNearTower, _tallestAtEdge, Mathf.Clamp01((distance - 20f) / 500f));
            float height = tallest * Lerp(0.35f, 1f, Next());
            if (Next() < _outlierChance)
            {
                height *= 1.5f;
            }
            // Never taller than the neighbor skyscrapers: the player must always look down on the city.
            return Mathf.Min(height, -_groundTop + _neighborRoof.x);
        }

        // Air conditioners, water tanks and stair huts: small boxes that show how big a building really is.
        private void AddRoofDetails(MeshBuffer details, Rect roof, float roofY)
        {
            int count = (int)(Next() * 4f);
            for (int k = 0; k < count; k++)
            {
                float w = Lerp(1.5f, 4f, Next());
                float d = Lerp(1.5f, 4f, Next());
                float h = Lerp(1f, 3.5f, Next());
                float x = Lerp(roof.xMin + w * 0.5f + 0.5f, roof.xMax - w * 0.5f - 0.5f, Next());
                float z = Lerp(roof.yMin + d * 0.5f + 0.5f, roof.yMax - d * 0.5f - 0.5f, Next());
                AddBox(details, new Vector3(x - w * 0.5f, roofY, z - d * 0.5f), new Vector3(x + w * 0.5f, roofY + h, z + d * 0.5f), Color.white);
            }
        }

        // Footprints of the skyscrapers next to the tower, spread around it so one is in view wherever the player looks.
        private List<Rect> PlaceNeighborTowers()
        {
            var footprints = new List<Rect>();
            float start = (float)(Next() * 360.0);
            for (int i = 0; i < _neighborTowers; i++)
            {
                float azimuth = (start + 360f / _neighborTowers * i + Lerp(-20f, 20f, Next())) * Mathf.Deg2Rad;
                float distance = Lerp(_neighborDistance.x, _neighborDistance.y, Next());
                float width = Lerp(_neighborWidth.x, _neighborWidth.y, Next());
                var center = new Vector2(Mathf.Sin(azimuth), Mathf.Cos(azimuth)) * distance;
                footprints.Add(new Rect(center.x - width * 0.5f, center.y - width * 0.5f, width, width));
            }
            return footprints;
        }

        private void CreateNeighborTower(Rect footprint)
        {
            float roof = Lerp(_neighborRoof.x, _neighborRoof.y, Next());
            float height = roof - _groundTop;

            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Skyscraper";
            Destroy(tower.GetComponent<Collider>());
            tower.transform.SetParent(_root, false);
            tower.transform.localPosition = new Vector3(footprint.center.x, _groundTop + height * 0.5f, footprint.center.y);
            tower.transform.localScale = new Vector3(footprint.width, height, footprint.height);
            SetupRenderer(tower.GetComponent<MeshRenderer>(), _neighborMaterial);

            // Antenna with a red warning light on top.
            var antenna = GameObject.CreatePrimitive(PrimitiveType.Cube);
            antenna.name = "Antenna";
            Destroy(antenna.GetComponent<Collider>());
            antenna.transform.SetParent(_root, false);
            antenna.transform.localPosition = new Vector3(footprint.center.x, roof + 5f, footprint.center.y);
            antenna.transform.localScale = new Vector3(0.5f, 10f, 0.5f);
            SetupRenderer(antenna.GetComponent<MeshRenderer>(), _roofDetailMaterial);

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Beacon";
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(_root, false);
            beacon.transform.localPosition = new Vector3(footprint.center.x, roof + 10.3f, footprint.center.y);
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

        // A box without its bottom face (never seen). Each face is listed clockwise as seen from outside.
        private static void AddBox(MeshBuffer buffer, Vector3 min, Vector3 max, Color color)
        {
            int first = buffer.Vertices.Count;
            AddQuad(buffer, Vector3.up, new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z));
            AddQuad(buffer, Vector3.forward, new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z));
            AddQuad(buffer, Vector3.back, new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z));
            AddQuad(buffer, Vector3.right, new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z));
            AddQuad(buffer, Vector3.left, new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z));
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

        private static bool Overlaps(Rect footprint, List<Rect> others)
        {
            foreach (Rect other in others)
            {
                var padded = new Rect(other.xMin - 3f, other.yMin - 3f, other.width + 6f, other.height + 6f);
                if (padded.Overlaps(footprint))
                {
                    return true;
                }
            }
            return false;
        }

        private float Next() => (float)_random.NextDouble();

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
