using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The asphalt, sidewalks and crosswalks of the city, painted with the pieces of a road pack (optional store
    /// asset, see the README). The pack's own tiles are square modules, but this city has streets of three widths,
    /// four turned grids and two diagonal avenues, so instead of placing the modules, each street becomes a long
    /// strip that repeats one piece of the pack's atlas along it. Everything goes into one mesh drawn without writing
    /// depth, in order: sidewalks, streets, crossings and crosswalks, then the avenues. Each layer simply paints over
    /// the ones before it, so crossings need no cutting. Without the pack, the streets painted on the ground stay.
    /// </summary>
    public partial class CityGenerator
    {
        private static readonly int AtlasId = Shader.PropertyToID("_Atlas");

        /// <summary>
        /// Where one road piece lies in the atlas, so a strip can repeat it: the atlas position at the start of a tile
        /// on its first edge, the step over one tile along the road, and the step from its first edge to the second.
        /// Width and length are the piece's real size in meters, so streets use it at its real scale.
        /// </summary>
        [System.Serializable]
        private struct AtlasPiece
        {
            public Vector2 Origin;
            public Vector2 Along;
            public Vector2 Across;
            public float Width;
            public float Length;
            [Tooltip("Average color of the piece (sRGB): far away the texture melts into it.")]
            public Color Average;

            public AtlasPiece(Vector2 origin, Vector2 along, Vector2 across, float width, float length, Color average)
            {
                Origin = origin;
                Along = along;
                Across = across;
                Width = width;
                Length = length;
                Average = average;
            }
        }

        // A straight line on the ground (top view): the points p where Vector2.Dot(Normal, p) == Value.
        private struct GroundLine
        {
            public Vector2 Normal;
            public float Value;

            public GroundLine(Vector2 normal, float value)
            {
                Normal = normal;
                Value = value;
            }
        }

        // One street or avenue: its center line and its asphalt width. Distances along it are measured from Origin.
        private struct RoadLine
        {
            public Vector2 Origin;
            public Vector2 Direction;
            // Direction turned a quarter to the right (seen from above).
            public Vector2 Side;
            public float Width;

            public RoadLine(Vector2 origin, Vector2 direction, float width)
            {
                Origin = origin;
                Direction = direction;
                Side = new Vector2(direction.y, -direction.x);
                Width = width;
            }

            // A line across the road at this distance along it.
            public GroundLine CutAt(float along) => new GroundLine(Direction, Vector2.Dot(Direction, Origin) + along);
        }

        // Geometry of the road strips, in drawing order.
        private class RoadBuffer
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Tiles = new List<Vector2>();
            public readonly List<Vector2> Origins = new List<Vector2>();
            public readonly List<Vector2> Alongs = new List<Vector2>();
            public readonly List<Vector2> Acrosses = new List<Vector2>();
            public readonly List<Color> Averages = new List<Color>();
            public readonly List<int> Triangles = new List<int>();
        }

        [Header("Road pack pieces (optional)")]
        [Tooltip("Material with the road pack's atlas. Empty, or without its atlas: the streets painted on the ground stay.")]
        [SerializeField] private Material _roadMaterial;
        [Tooltip("Height of the road strips over the ground (meters).")]
        [SerializeField] private float _roadLift = 0.1f;
        [Tooltip("Streets at least this wide get the four lane piece.")]
        [SerializeField] private float _fourLaneWidth = 12f;
        [Tooltip("Length of a crosswalk along the street (meters).")]
        [SerializeField] private float _crosswalkLength = 2f;
        [Tooltip("Pieces of the Urban Road Pack Lite atlas (weathered version).")]
        [SerializeField] private AtlasPiece _sidewalkPiece = new AtlasPiece(new Vector2(0.2178f, 0.9068f),
            new Vector2(0f, 0.0821f), new Vector2(-0.0202f, 0f), 2f, 8f, new Color(0.48f, 0.38f, 0.28f));
        [SerializeField] private AtlasPiece _twoLanePiece = new AtlasPiece(new Vector2(0.7219f, 0.9345f),
            new Vector2(-0.0822f, 0f), new Vector2(0f, -0.1644f), 16.27f, 8f, new Color(0.46f, 0.46f, 0.455f));
        [SerializeField] private AtlasPiece _fourLanePiece = new AtlasPiece(new Vector2(0.0822f, 0.856f),
            new Vector2(-0.0822f, 0f), new Vector2(0f, -0.1644f), 16.27f, 8f, new Color(0.46f, 0.46f, 0.455f));
        [SerializeField] private AtlasPiece _crosswalkPiece = new AtlasPiece(new Vector2(0.8832f, 0.2768f),
            new Vector2(-0.0205f, 0f), new Vector2(0f, -0.1643f), 16.27f, 2.03f, new Color(0.76f, 0.74f, 0.65f));
        [SerializeField] private AtlasPiece _plainAsphaltPiece = new AtlasPiece(new Vector2(0.7168f, 0.9261f),
            new Vector2(-0.0719f, 0f), new Vector2(0f, -0.0606f), 6f, 7f, new Color(0.455f, 0.45f, 0.445f));

        // Streets end where the street map ends; beyond, the ground's plain grid is deep in the fog anyway.
        private float RoadRadius => _mapHalfSize;

        /// <summary>Builds the road strips of every street and avenue, if the road pack is in the project.</summary>
        private void BuildRoads()
        {
            if (_roadMaterial == null || _roadMaterial.GetTexture(AtlasId) == null)
            {
                return;
            }
            var streets = new List<RoadLine>();
            var limits = new List<(GroundLine Start, GroundLine End, float From, float To)>();
            for (int d = 0; d < _districts.Length; d++)
            {
                District district = _districts[d];
                Vector2 alongZ = Rotate(Vector2.up, district.Angle);
                Vector2 alongX = Rotate(Vector2.right, district.Angle);
                for (int i = 0; i < district.XLines.Count; i++)
                {
                    AddDistrictStreet(streets, limits, d, new RoadLine(Rotate(new Vector2(district.XLines[i], 0f), district.Angle), alongZ, district.XWidths[i]));
                }
                for (int j = 0; j < district.ZLines.Count; j++)
                {
                    AddDistrictStreet(streets, limits, d, new RoadLine(Rotate(new Vector2(0f, district.ZLines[j]), district.Angle), alongX, district.ZWidths[j]));
                }
            }
            var avenues = new List<RoadLine>();
            var avenueLimits = new List<(GroundLine Start, GroundLine End, float From, float To)>();
            foreach (Avenue avenue in _avenues)
            {
                var road = new RoadLine(avenue.Point, new Vector2(avenue.Normal.y, -avenue.Normal.x), _avenueWidth);
                if (CircleLimits(road, out float from, out float to))
                {
                    avenues.Add(road);
                    avenueLimits.Add((road.CutAt(from), road.CutAt(to), from, to));
                }
            }

            var buffer = new RoadBuffer();
            // 1. Sidewalks of everything (crossing streets cover them where they meet).
            for (int k = 0; k < streets.Count; k++)
            {
                AddSidewalks(buffer, streets[k], limits[k].Start, limits[k].End);
            }
            for (int k = 0; k < avenues.Count; k++)
            {
                AddSidewalks(buffer, avenues[k], avenueLimits[k].Start, avenueLimits[k].End);
            }
            // 2. Asphalt of the streets.
            for (int k = 0; k < streets.Count; k++)
            {
                AddAsphalt(buffer, streets[k], limits[k].Start, limits[k].End, limits[k].From, limits[k].To);
            }
            // 3. Plain asphalt over every crossing (no lane marks across it) and a crosswalk on each of its sides.
            for (int d = 0; d < _districts.Length; d++)
            {
                AddCrossings(buffer, d);
            }
            // 4. The avenues over the ends of the streets, and a plain patch where they cross each other.
            for (int k = 0; k < avenues.Count; k++)
            {
                AddAsphalt(buffer, avenues[k], avenueLimits[k].Start, avenueLimits[k].End, avenueLimits[k].From, avenueLimits[k].To);
            }
            if (avenues.Count == 2)
            {
                RoadLine first = avenues[0];
                RoadLine second = avenues[1];
                float half = second.Width * 0.5f;
                var start = new GroundLine(second.Side, Vector2.Dot(second.Side, second.Origin) - half);
                var end = new GroundLine(second.Side, Vector2.Dot(second.Side, second.Origin) + half);
                if (Vector2.Dot(second.Side, first.Direction) < 0f)
                {
                    (start, end) = (end, start);
                }
                AddRoadBand(buffer, first, -first.Width * 0.5f, first.Width * 0.5f, start, end, _plainAsphaltPiece, 0f, 1f, 0f);
            }

            var mesh = new Mesh { name = "CityRoads" };
            if (buffer.Vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }
            mesh.SetVertices(buffer.Vertices);
            mesh.SetColors(buffer.Averages);
            mesh.SetUVs(0, buffer.Tiles);
            mesh.SetUVs(1, buffer.Origins);
            mesh.SetUVs(2, buffer.Alongs);
            mesh.SetUVs(3, buffer.Acrosses);
            mesh.SetTriangles(buffer.Triangles, 0);
            mesh.RecalculateBounds();
            var roads = new GameObject("Roads", typeof(MeshFilter), typeof(MeshRenderer));
            roads.transform.SetParent(_root, false);
            roads.GetComponent<MeshFilter>().sharedMesh = mesh;
            SetupRenderer(roads.GetComponent<MeshRenderer>(), _roadMaterial);
        }

        // A street of a district, from where it enters its district (or the city) to where it leaves it.
        private void AddDistrictStreet(List<RoadLine> streets, List<(GroundLine, GroundLine, float, float)> limits, int d, RoadLine road)
        {
            if (!CircleLimits(road, out float from, out float to))
            {
                return;
            }
            GroundLine start = road.CutAt(from);
            GroundLine end = road.CutAt(to);
            for (int a = 0; a < _avenues.Length; a++)
            {
                // The street stops at the edge of the avenue's asphalt, on its own district's side.
                float sign = ((d >> a) & 1) == 1 ? 1f : -1f;
                var edge = new GroundLine(_avenues[a].Normal * sign,
                    _avenueWidth * 0.5f + sign * Vector2.Dot(_avenues[a].Normal, _avenues[a].Point));
                float facing = Vector2.Dot(edge.Normal, road.Direction);
                if (Mathf.Abs(facing) < 0.001f)
                {
                    if (Vector2.Dot(edge.Normal, road.Origin) < edge.Value)
                    {
                        return;
                    }
                    continue;
                }
                float at = AlongRoad(road, 0f, edge);
                if (facing > 0f && at > from)
                {
                    from = at;
                    start = edge;
                }
                else if (facing < 0f && at < to)
                {
                    to = at;
                    end = edge;
                }
            }
            if (to - from > road.Width)
            {
                streets.Add(road);
                limits.Add((start, end, from, to));
            }
        }

        // Where the road's center line enters and leaves the circle of the city.
        private bool CircleLimits(RoadLine road, out float from, out float to)
        {
            float b = Vector2.Dot(road.Origin, road.Direction);
            float c = road.Origin.sqrMagnitude - RoadRadius * RoadRadius;
            float disc = b * b - c;
            float root = Mathf.Sqrt(Mathf.Max(0f, disc));
            from = -b - root;
            to = -b + root;
            return disc > 0f;
        }

        private void AddSidewalks(RoadBuffer buffer, RoadLine road, GroundLine start, GroundLine end)
        {
            float half = road.Width * 0.5f;
            // The piece goes from the building side (0) to the curb (1).
            AddRoadBand(buffer, road, -half - _sidewalk, -half, start, end, _sidewalkPiece, 0f, 1f, _sidewalkPiece.Length);
            AddRoadBand(buffer, road, half, half + _sidewalk, start, end, _sidewalkPiece, 1f, 0f, _sidewalkPiece.Length);
        }

        // Asphalt at the piece's real scale (a window of it as wide as the street), stretched only when the street is
        // wider than the piece.
        private void AddAsphalt(RoadBuffer buffer, RoadLine road, GroundLine start, GroundLine end, float from, float to)
        {
            AtlasPiece piece = road.Width >= _fourLaneWidth ? _fourLanePiece : _twoLanePiece;
            Window(piece, road.Width, out float s0, out float s1, out float tileLength);
            AddRoadBand(buffer, road, -road.Width * 0.5f, road.Width * 0.5f, start, end, piece, s0, s1, tileLength);
        }

        // Every crossing of the district well inside it: a plain square, and a crosswalk on each street around it.
        private void AddCrossings(RoadBuffer buffer, int d)
        {
            District district = _districts[d];
            Vector2 alongZ = Rotate(Vector2.up, district.Angle);
            Vector2 alongX = Rotate(Vector2.right, district.Angle);
            for (int i = 0; i < district.XLines.Count; i++)
            {
                var xStreet = new RoadLine(Rotate(new Vector2(district.XLines[i], 0f), district.Angle), alongZ, district.XWidths[i]);
                for (int j = 0; j < district.ZLines.Count; j++)
                {
                    var zStreet = new RoadLine(Rotate(new Vector2(0f, district.ZLines[j]), district.Angle), alongX, district.ZWidths[j]);
                    Vector2 center = Rotate(new Vector2(district.XLines[i], district.ZLines[j]), district.Angle);
                    float margin = _avenueWidth * 0.5f + _sidewalk + Mathf.Max(xStreet.Width, zStreet.Width) + _crosswalkLength;
                    if (center.magnitude > RoadRadius - margin || DistanceToAvenues(center, d) < margin)
                    {
                        continue;
                    }
                    // Along the X street the crossing is at the Z line, and the other way around.
                    float atZ = district.ZLines[j];
                    float atX = district.XLines[i];
                    float halfX = xStreet.Width * 0.5f;
                    float halfZ = zStreet.Width * 0.5f;
                    AddRoadBand(buffer, xStreet, -halfX, halfX, xStreet.CutAt(atZ - halfZ), xStreet.CutAt(atZ + halfZ),
                        _plainAsphaltPiece, 0f, 1f, 0f);
                    AddCrosswalks(buffer, xStreet, atZ - halfZ, atZ + halfZ);
                    AddCrosswalks(buffer, zStreet, atX - halfX, atX + halfX);
                }
            }
        }

        // How far a point of district d is from the nearest avenue asphalt edge.
        private float DistanceToAvenues(Vector2 point, int d)
        {
            float distance = float.MaxValue;
            for (int a = 0; a < _avenues.Length; a++)
            {
                float sign = ((d >> a) & 1) == 1 ? 1f : -1f;
                distance = Mathf.Min(distance, sign * AvenueSide(point, a) - _avenueWidth * 0.5f);
            }
            return distance;
        }

        private void AddCrosswalks(RoadBuffer buffer, RoadLine road, float crossingStart, float crossingEnd)
        {
            Window(_crosswalkPiece, road.Width, out float s0, out float s1, out _);
            float half = road.Width * 0.5f;
            AddRoadBand(buffer, road, -half, half, road.CutAt(crossingStart - _crosswalkLength), road.CutAt(crossingStart),
                _crosswalkPiece, s0, s1, 0f);
            AddRoadBand(buffer, road, -half, half, road.CutAt(crossingEnd), road.CutAt(crossingEnd + _crosswalkLength),
                _crosswalkPiece, s0, s1, 0f);
        }

        // The part of a piece a road of this width shows: a window of the piece's real size centered on it, or the whole
        // piece stretched (and its tiles stretched as much) when the road is wider.
        private static void Window(AtlasPiece piece, float width, out float s0, out float s1, out float tileLength)
        {
            float share = Mathf.Min(1f, width / piece.Width);
            s0 = 0.5f - share * 0.5f;
            s1 = 0.5f + share * 0.5f;
            tileLength = piece.Length * Mathf.Max(1f, width / piece.Width);
        }

        // Distance along the road where one of its edges (at this offset across it) meets the line.
        private static float AlongRoad(RoadLine road, float offset, GroundLine line)
        {
            Vector2 edgeStart = road.Origin + road.Side * offset;
            return (line.Value - Vector2.Dot(line.Normal, edgeStart)) / Vector2.Dot(line.Normal, road.Direction);
        }

        // One strip of a road, from `from` to `to` meters across its center line, between two lines that may cut it
        // at a slant. The piece's window s0..s1 goes across it; with a tile length the piece repeats along the strip,
        // without one it is stretched once over it.
        private void AddRoadBand(RoadBuffer buffer, RoadLine road, float from, float to, GroundLine start, GroundLine end,
            AtlasPiece piece, float s0, float s1, float tileLength)
        {
            float fromStart = AlongRoad(road, from, start);
            float fromEnd = AlongRoad(road, from, end);
            float toStart = AlongRoad(road, to, start);
            float toEnd = AlongRoad(road, to, end);
            if (fromEnd <= fromStart || toEnd <= toStart)
            {
                return;
            }
            float first = Mathf.Min(fromStart, toStart);
            float length = Mathf.Max(fromEnd, toEnd) - first;
            int index = buffer.Vertices.Count;
            Color average = piece.Average.linear;
            void Corner(float across, float along, float s)
            {
                Vector2 point = road.Origin + road.Side * across + road.Direction * along;
                buffer.Vertices.Add(new Vector3(point.x, _groundTop + _roadLift, point.y));
                // Repeated: tiles counted from the road's origin, so they run on across crossings. Once: 0 to 1.
                float tiles = tileLength > 0f ? along / tileLength : (along - first) / length * 0.999f;
                buffer.Tiles.Add(new Vector2(s, tiles));
                buffer.Origins.Add(piece.Origin);
                buffer.Alongs.Add(piece.Along);
                buffer.Acrosses.Add(piece.Across);
                buffer.Averages.Add(average);
            }
            Corner(from, fromStart, s0);
            Corner(from, fromEnd, s0);
            Corner(to, toEnd, s1);
            Corner(to, toStart, s1);
            buffer.Triangles.Add(index);
            buffer.Triangles.Add(index + 1);
            buffer.Triangles.Add(index + 2);
            buffer.Triangles.Add(index);
            buffer.Triangles.Add(index + 2);
            buffer.Triangles.Add(index + 3);
        }
    }
}
