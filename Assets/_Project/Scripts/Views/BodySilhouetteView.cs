using System.Collections.Generic;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// The player's own body in VR, as a faint glow with a thin luminous blue edge (SilhouetteTranslucent shader). The
    /// Quest 3 cameras track the upper body and the system estimates the legs (OVRBody, full body joint set); this view
    /// draws a capsule between pairs of joints: torso, arms, every finger and the legs. The torso and the
    /// palms are flat capsules turned with the line of the shoulders, hips or knuckles, wider than deep like a real
    /// body, so the outline reads as a person instead of a row of tubes. So the blaster is held by a hand and the radar
    /// sits on a wrist instead of floating in the air. In AR the real body is visible, so the silhouette only shows in VR.
    /// If body tracking is unavailable (not allowed, or no data), nothing is drawn.
    /// </summary>
    public class BodySilhouetteView : MonoBehaviour
    {
        // One capsule between two joints. A round piece has one radius; a flat piece is Radius wide (half width) and
        // Depth thick (half thickness), with its width along the line between two other joints (SideFrom to SideTo).
        private struct Segment
        {
            public OVRSkeleton.BoneId From;
            public OVRSkeleton.BoneId To;
            public float Radius;
            public float Depth;
            public OVRSkeleton.BoneId SideFrom;
            public OVRSkeleton.BoneId SideTo;

            public Segment(OVRSkeleton.BoneId from, OVRSkeleton.BoneId to, float radius)
                : this(from, to, radius, radius, OVRSkeleton.BoneId.Invalid, OVRSkeleton.BoneId.Invalid)
            {
            }

            public Segment(OVRSkeleton.BoneId from, OVRSkeleton.BoneId to, float radius, float depth,
                OVRSkeleton.BoneId sideFrom, OVRSkeleton.BoneId sideTo)
            {
                From = from;
                To = to;
                Radius = radius;
                Depth = depth;
                SideFrom = sideFrom;
                SideTo = sideTo;
            }
        }

        [SerializeField] private OVRBody _body;
        [SerializeField] private OVRSkeleton _skeleton;
        [SerializeField] private PassthroughFader _passthrough;
        [Tooltip("Center eye: pieces whose surface comes this close to it are hidden, so the body never blocks the view.")]
        [SerializeField] private Transform _head;
        [SerializeField] private Mesh _segmentMesh;
        [Tooltip("Depth-only material drawn first, so the see-through body shows a single surface.")]
        [SerializeField] private Material _depthMaterial;
        [SerializeField] private Material _material;
        [SerializeField] private float _headClearance = 0.15f;

        private static readonly Segment[] Layout = BuildLayout();

        private readonly List<Transform> _pieces = new List<Transform>();
        private Transform[] _from;
        private Transform[] _to;
        private Transform[] _sideFrom;
        private Transform[] _sideTo;
        private Transform _root;
        private bool _bound;

        /// <summary>Testing aid: draws the silhouette from bound bones even without live tracking data.</summary>
        public bool ForceShow { get; set; }

        private void Awake()
        {
            _root = new GameObject("Silhouette").transform;
            _root.SetParent(transform, false);
            _root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            OVRPermissionsRequester.PermissionGranted += HandlePermissionGranted;
        }

        private void OnDisable()
        {
            OVRPermissionsRequester.PermissionGranted -= HandlePermissionGranted;
        }

        // On the first launch OVRBody starts before the player allows body tracking, fails and switches itself off;
        // it is switched back on as soon as the permission is granted.
        private void HandlePermissionGranted(string permission)
        {
            if (permission == OVRPermissionsRequester.BodyTrackingPermission && !_body.enabled)
            {
                _body.enabled = true;
            }
        }

        private void LateUpdate()
        {
            bool tracked = _skeleton.IsInitialized && _skeleton.IsDataValid;
            if (tracked && !_bound)
            {
                BindBones(_skeleton.Bones);
            }

            bool show = _bound && _passthrough.IsVR && (tracked || ForceShow);
            if (_root.gameObject.activeSelf != show)
            {
                _root.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            Vector3 head = _head.position;
            for (int i = 0; i < _pieces.Count; i++)
            {
                Transform piece = _pieces[i];
                if (_from[i] == null || _to[i] == null)
                {
                    continue;
                }
                Vector3 a = _from[i].position;
                Vector3 b = _to[i].position;
                float radius = Layout[i].Radius;
                // Seen from the eyes the upper chest sits right under the chin: hide what would fill the view.
                bool nearHead = Vector3.Distance(a, head) - radius < _headClearance ||
                                Vector3.Distance(b, head) - radius < _headClearance;
                if (piece.gameObject.activeSelf == nearHead)
                {
                    piece.gameObject.SetActive(!nearHead);
                }
                if (nearHead)
                {
                    continue;
                }
                Vector3 side = _sideFrom[i] != null && _sideTo[i] != null
                    ? _sideTo[i].position - _sideFrom[i].position
                    : Vector3.zero;
                PlaceCapsule(piece, a, b, radius, Layout[i].Depth, side);
            }
        }

        /// <summary>
        /// Links every segment to its two joint transforms and creates the capsules (once, when tracking starts).
        /// Public so an editor preview can feed it a posed test skeleton.
        /// </summary>
        public void BindBones(IList<OVRBone> bones)
        {
            var byId = new Dictionary<OVRSkeleton.BoneId, Transform>();
            foreach (OVRBone bone in bones)
            {
                if (bone != null && bone.Transform != null)
                {
                    byId[bone.Id] = bone.Transform;
                }
            }

            _from = new Transform[Layout.Length];
            _to = new Transform[Layout.Length];
            _sideFrom = new Transform[Layout.Length];
            _sideTo = new Transform[Layout.Length];
            for (int i = 0; i < Layout.Length; i++)
            {
                byId.TryGetValue(Layout[i].From, out _from[i]);
                byId.TryGetValue(Layout[i].To, out _to[i]);
                byId.TryGetValue(Layout[i].SideFrom, out _sideFrom[i]);
                byId.TryGetValue(Layout[i].SideTo, out _sideTo[i]);
                if (i >= _pieces.Count)
                {
                    _pieces.Add(CreatePiece(i));
                }
            }
            _bound = true;
        }

        private Transform CreatePiece(int index)
        {
            var piece = new GameObject("Segment" + index, typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(_root, false);
            piece.GetComponent<MeshFilter>().sharedMesh = _segmentMesh;
            var meshRenderer = piece.GetComponent<MeshRenderer>();
            // Two materials on one mesh draw it twice: depth for the whole body first (earlier queue), then color.
            meshRenderer.sharedMaterials = new[] { _depthMaterial, _material };
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return piece.transform;
        }

        // Unity's capsule is 2 units tall and 1 wide along Y: stretch it from joint a to joint b, rounding past both
        // ends by the radius so neighboring pieces overlap at the joints. A flat piece turns its width (local X)
        // along the side direction and its thickness (local Z) to the front.
        private static void PlaceCapsule(Transform piece, Vector3 a, Vector3 b, float radius, float depth, Vector3 side)
        {
            Vector3 direction = b - a;
            float length = direction.magnitude;
            Vector3 up = length > 0.0001f ? direction / length : Vector3.up;
            Vector3 width = Vector3.ProjectOnPlane(side, up);
            piece.position = (a + b) * 0.5f;
            piece.rotation = width.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(Vector3.Cross(width.normalized, up), up)
                : Quaternion.FromToRotation(Vector3.up, up);
            piece.localScale = new Vector3(radius * 2f, (length + radius * 2f) * 0.5f, depth * 2f);
        }

        // The body as a list of joint pairs. Head and neck are left out: the camera sits inside them.
        private static Segment[] BuildLayout()
        {
            var layout = new List<Segment>
            {
                // Torso: flat, as wide as the hips below and the shoulders above
                new Segment(OVRSkeleton.BoneId.FullBody_Hips, OVRSkeleton.BoneId.FullBody_SpineLower, 0.15f, 0.1f,
                    OVRSkeleton.BoneId.FullBody_LeftUpperLeg, OVRSkeleton.BoneId.FullBody_RightUpperLeg),
                new Segment(OVRSkeleton.BoneId.FullBody_SpineLower, OVRSkeleton.BoneId.FullBody_SpineMiddle, 0.14f, 0.095f,
                    OVRSkeleton.BoneId.FullBody_LeftUpperLeg, OVRSkeleton.BoneId.FullBody_RightUpperLeg),
                new Segment(OVRSkeleton.BoneId.FullBody_SpineMiddle, OVRSkeleton.BoneId.FullBody_SpineUpper, 0.15f, 0.1f,
                    OVRSkeleton.BoneId.FullBody_LeftArmUpper, OVRSkeleton.BoneId.FullBody_RightArmUpper),
                new Segment(OVRSkeleton.BoneId.FullBody_SpineUpper, OVRSkeleton.BoneId.FullBody_Chest, 0.16f, 0.1f,
                    OVRSkeleton.BoneId.FullBody_LeftArmUpper, OVRSkeleton.BoneId.FullBody_RightArmUpper),

                // Arms from the shoulder joint (the collarbones would hang right under the chin)
                new Segment(OVRSkeleton.BoneId.FullBody_LeftArmUpper, OVRSkeleton.BoneId.FullBody_LeftArmLower, 0.045f),
                new Segment(OVRSkeleton.BoneId.FullBody_LeftArmLower, OVRSkeleton.BoneId.FullBody_LeftHandWrist, 0.035f),
                new Segment(OVRSkeleton.BoneId.FullBody_RightArmUpper, OVRSkeleton.BoneId.FullBody_RightArmLower, 0.045f),
                new Segment(OVRSkeleton.BoneId.FullBody_RightArmLower, OVRSkeleton.BoneId.FullBody_RightHandWrist, 0.035f),

                // Legs (estimated by the system from the upper body)
                new Segment(OVRSkeleton.BoneId.FullBody_Hips, OVRSkeleton.BoneId.FullBody_LeftUpperLeg, 0.08f),
                new Segment(OVRSkeleton.BoneId.FullBody_LeftUpperLeg, OVRSkeleton.BoneId.FullBody_LeftLowerLeg, 0.065f),
                new Segment(OVRSkeleton.BoneId.FullBody_LeftLowerLeg, OVRSkeleton.BoneId.FullBody_LeftFootAnkle, 0.05f),
                new Segment(OVRSkeleton.BoneId.FullBody_LeftFootAnkle, OVRSkeleton.BoneId.FullBody_LeftFootBall, 0.045f),
                new Segment(OVRSkeleton.BoneId.FullBody_Hips, OVRSkeleton.BoneId.FullBody_RightUpperLeg, 0.08f),
                new Segment(OVRSkeleton.BoneId.FullBody_RightUpperLeg, OVRSkeleton.BoneId.FullBody_RightLowerLeg, 0.065f),
                new Segment(OVRSkeleton.BoneId.FullBody_RightLowerLeg, OVRSkeleton.BoneId.FullBody_RightFootAnkle, 0.05f),
                new Segment(OVRSkeleton.BoneId.FullBody_RightFootAnkle, OVRSkeleton.BoneId.FullBody_RightFootBall, 0.045f),
            };
            AddHand(layout, true);
            AddHand(layout, false);
            return layout.ToArray();
        }

        // Palm plus three bones per finger. Bone ids of each hand are consecutive in the same order, so the right
        // hand is the left hand shifted by a fixed offset.
        private static void AddHand(List<Segment> layout, bool left)
        {
            int shift = left ? 0 : OVRSkeleton.BoneId.FullBody_RightHandWrist - OVRSkeleton.BoneId.FullBody_LeftHandWrist;
            OVRSkeleton.BoneId Id(OVRSkeleton.BoneId leftId) => leftId + shift;

            // A flat palm, as wide as the knuckles, and a thin bar across the knuckles.
            layout.Add(new Segment(Id(OVRSkeleton.BoneId.FullBody_LeftHandWrist), Id(OVRSkeleton.BoneId.FullBody_LeftHandMiddleProximal), 0.035f, 0.014f,
                Id(OVRSkeleton.BoneId.FullBody_LeftHandIndexProximal), Id(OVRSkeleton.BoneId.FullBody_LeftHandLittleProximal)));
            layout.Add(new Segment(Id(OVRSkeleton.BoneId.FullBody_LeftHandIndexProximal), Id(OVRSkeleton.BoneId.FullBody_LeftHandLittleProximal), 0.014f));

            OVRSkeleton.BoneId[] fingerStarts =
            {
                OVRSkeleton.BoneId.FullBody_LeftHandThumbMetacarpal,  // Metacarpal, Proximal, Distal, Tip
                OVRSkeleton.BoneId.FullBody_LeftHandIndexProximal,    // Proximal, Intermediate, Distal, Tip
                OVRSkeleton.BoneId.FullBody_LeftHandMiddleProximal,
                OVRSkeleton.BoneId.FullBody_LeftHandRingProximal,
                OVRSkeleton.BoneId.FullBody_LeftHandLittleProximal
            };
            foreach (OVRSkeleton.BoneId start in fingerStarts)
            {
                float radius = start == OVRSkeleton.BoneId.FullBody_LeftHandThumbMetacarpal ? 0.012f : 0.01f;
                for (int joint = 0; joint < 3; joint++)
                {
                    layout.Add(new Segment(Id(start + joint), Id(start + joint + 1), radius));
                }
            }
        }
    }
}
