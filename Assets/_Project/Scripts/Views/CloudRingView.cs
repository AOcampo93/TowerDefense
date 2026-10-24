using System.Collections.Generic;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The clouds all around the horizon, so the line between the ground and the sky disappears in every direction:
    /// a low band of wide clouds right on the horizon, and huge storm clouds (cumulonimbus) of different shapes and
    /// sizes rising from it. Every cloud is a flat picture (one cell of a cloud atlas) standing still in the world and
    /// turned towards the tower, all merged into one mesh built once from a fixed seed. They never
    /// turn with the player's head, so they stay put like real clouds. Clouds facing the setting sun are tinted pink,
    /// the others violet.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CloudRingView : MonoBehaviour
    {
        // Height of the flat base inside each cloud picture (share of the picture, from its bottom edge).
        private const float BaseInPicture = 0.12f;

        [SerializeField] private int _seed = 31;

        [Header("Cloud atlas")]
        [Tooltip("Cells of the cloud atlas: columns and rows. Cells are numbered from the bottom left, row by row.")]
        [SerializeField] private Vector2Int _atlasCells = new Vector2Int(4, 2);
        [Tooltip("How many cells hold a cloud, counted from the first; the cells after them are empty.")]
        [SerializeField] private int _cloudShapes = 8;
        [Tooltip("Cells with wide, flat clouds, used for the low band on the horizon.")]
        [SerializeField] private int[] _bandShapes = { 0, 1, 2 };

        [Header("Storm clouds")]
        [SerializeField] private int _clouds = 40;
        [Tooltip("Distance range from the tower (meters).")]
        [SerializeField] private Vector2 _distance = new Vector2(620f, 880f);
        [Tooltip("Height range of the cloud bases, relative to the player (the street is at -200).")]
        [SerializeField] private Vector2 _baseHeight = new Vector2(-250f, -205f);
        [Tooltip("Size range of the storm clouds (meters).")]
        [SerializeField] private Vector2 _cloudSize = new Vector2(260f, 520f);

        [Header("Low band on the horizon")]
        [SerializeField] private int _bandClouds = 70;
        [SerializeField] private Vector2 _bandDistance = new Vector2(700f, 900f);
        [SerializeField] private Vector2 _bandBase = new Vector2(-245f, -220f);
        [SerializeField] private Vector2 _bandSize = new Vector2(200f, 360f);

        [Header("Color")]
        [Tooltip("Compass direction of the sun (degrees from +Z towards +X), to light the clouds facing it.")]
        [SerializeField] private float _sunAzimuth = 117.4f;
        [SerializeField] private Color _litColor = new Color(1f, 0.74f, 0.78f);
        [SerializeField] private Color _shadeColor = new Color(0.72f, 0.62f, 0.92f);

        private struct Cloud
        {
            public float Azimuth;
            public float Distance;
            public float BaseY;
            public float Size;
            public int Shape;
            public bool Mirrored;
        }

        private System.Random _random;

        private void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = BuildMesh();
        }

        private Mesh BuildMesh()
        {
            _random = new System.Random(_seed);
            var clouds = new List<Cloud>();
            for (int c = 0; c < _bandClouds; c++)
            {
                float azimuth = (c + Lerp(-0.5f, 0.5f, Next())) * 360f / _bandClouds;
                clouds.Add(NewCloud(azimuth, _bandDistance, _bandBase, _bandSize, _bandShapes[_random.Next(_bandShapes.Length)]));
            }
            for (int c = 0; c < _clouds; c++)
            {
                float azimuth = (c + Lerp(-0.45f, 0.45f, Next())) * 360f / _clouds;
                clouds.Add(NewCloud(azimuth, _distance, _baseHeight, _cloudSize, _random.Next(Mathf.Min(_cloudShapes, _atlasCells.x * _atlasCells.y))));
            }
            // Transparent pictures must be drawn from the farthest to the nearest; the player is always at the center.
            clouds.Sort((a, b) => b.Distance.CompareTo(a.Distance));

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            foreach (Cloud cloud in clouds)
            {
                AddPicture(cloud, vertices, uvs, colors, triangles);
            }
            var mesh = new Mesh { name = "CloudRing" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private Cloud NewCloud(float azimuth, Vector2 distance, Vector2 baseY, Vector2 size, int shape)
        {
            return new Cloud
            {
                Azimuth = azimuth,
                Distance = Lerp(distance.x, distance.y, Next()),
                BaseY = Lerp(baseY.x, baseY.y, Next()),
                Size = Lerp(size.x, size.y, Next()),
                Shape = shape,
                Mirrored = Next() < 0.5f
            };
        }

        // One upright picture facing the tower, its flat base at the cloud's base height.
        private void AddPicture(Cloud cloud, List<Vector3> vertices, List<Vector2> uvs, List<Color> colors, List<int> triangles)
        {
            float radians = cloud.Azimuth * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            Vector3 right = Vector3.Cross(Vector3.up, outward) * (cloud.Size * 0.5f);
            Vector3 bottom = outward * cloud.Distance + Vector3.up * (cloud.BaseY - cloud.Size * BaseInPicture);
            Vector3 up = Vector3.up * cloud.Size;

            int first = vertices.Count;
            vertices.Add(bottom - right);
            vertices.Add(bottom - right + up);
            vertices.Add(bottom + right + up);
            vertices.Add(bottom + right);

            // Cell of the atlas, a hair inside its borders so neighbors never bleed in.
            const float Inset = 0.002f;
            float cellWidth = 1f / _atlasCells.x;
            float cellHeight = 1f / _atlasCells.y;
            float u0 = (cloud.Shape % _atlasCells.x) * cellWidth + Inset;
            float u1 = u0 + cellWidth - Inset * 2f;
            float v0 = (cloud.Shape / _atlasCells.x) * cellHeight + Inset;
            float v1 = v0 + cellHeight - Inset * 2f;
            if (cloud.Mirrored)
            {
                (u0, u1) = (u1, u0);
            }
            uvs.Add(new Vector2(u0, v0));
            uvs.Add(new Vector2(u0, v1));
            uvs.Add(new Vector2(u1, v1));
            uvs.Add(new Vector2(u1, v0));

            // Clouds facing the low sun are lit warm, the others stay in violet shade.
            float facing = Mathf.Clamp01(Mathf.Cos((cloud.Azimuth - _sunAzimuth) * Mathf.Deg2Rad));
            Color tint = Color.Lerp(_shadeColor, _litColor, facing * facing);
            for (int i = 0; i < 4; i++)
            {
                colors.Add(tint);
            }
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        private float Next() => (float)_random.NextDouble();

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
