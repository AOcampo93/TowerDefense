using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Builds the open cylinder that carries the horizon cloud bank: a ring of clouds around the whole city that hides
    /// where it ends, like the cloud walls in the reference images. The mesh is made in code (a few hundred vertices),
    /// so no model file is needed.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CloudBankView : MonoBehaviour
    {
        [SerializeField] private float _radius = 900f;
        [Tooltip("Bottom and top of the bank (meters, relative to the player platform).")]
        [SerializeField] private float _bottom = -260f;
        [SerializeField] private float _top = 170f;
        [SerializeField] private int _segments = 96;

        private void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = BuildCylinder();
        }

        // Side of a cylinder seen from the inside; u goes once around, v from bottom to top.
        private Mesh BuildCylinder()
        {
            var vertices = new Vector3[(_segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[_segments * 6];
            for (int i = 0; i <= _segments; i++)
            {
                float u = (float)i / _segments;
                float angle = u * Mathf.PI * 2f;
                var around = new Vector3(Mathf.Sin(angle) * _radius, 0f, Mathf.Cos(angle) * _radius);
                vertices[i * 2] = around + Vector3.up * _bottom;
                vertices[i * 2 + 1] = around + Vector3.up * _top;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }
            for (int i = 0; i < _segments; i++)
            {
                int a = i * 2;
                int t = i * 6;
                triangles[t] = a;
                triangles[t + 1] = a + 1;
                triangles[t + 2] = a + 2;
                triangles[t + 3] = a + 1;
                triangles[t + 4] = a + 3;
                triangles[t + 5] = a + 2;
            }

            var mesh = new Mesh { name = "CloudBank", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
