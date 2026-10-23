using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Tactical watch on the left wrist (diegetic radar, no HUD). WristMount wears it on the back of the wrist like a
    /// real watch; the dial shows the player in the middle and "where I am looking" always at 12 o'clock: red blips
    /// are ships (blinking while they aim at the tower), purple blips are portals (pulsing, also while they open).
    /// Ships beyond the range stick to the edge, so threats behind the player are never invisible. A ring around the
    /// dial shows the tower's integrity (green, yellow, red, blinking when critical), so the player's only "life" is
    /// on the wrist too.
    /// </summary>
    public class RadarView : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private ShipSpawner _ships;
        [SerializeField] private PortalSpawner _portals;
        [Tooltip("Center eye: the radar faces it and rotates with its heading.")]
        [SerializeField] private Transform _head;
        [Tooltip("Shown only during the fight.")]
        [SerializeField] private GameObject _display;
        [Tooltip("Disc that holds the blips; local +Y is 'ahead'.")]
        [SerializeField] private Transform _face;
        [Tooltip("Turn the dial towards the eyes every frame (off: it lies on the wrist like a real watch).")]
        [SerializeField] private bool _faceTheEyes;
        [SerializeField] private Transform _shipBlipPrefab;
        [SerializeField] private Transform _portalBlipPrefab;

        [Header("Scale")]
        [Tooltip("Radius of the disc in meters (a 10 cm watch).")]
        [SerializeField] private float _radius = 0.055f;
        [Tooltip("Meters of the real world shown from the center to the edge.")]
        [SerializeField] private float _range = 150f;
        [SerializeField] private int _maxShipBlips = 56;

        [Header("Feel")]
        [SerializeField] private float _fighterBlipSize = 0.007f;
        [SerializeField] private float _bomberBlipSize = 0.011f;
        [SerializeField] private float _portalBlipSize = 0.015f;
        [SerializeField] private float _aimBlinkRate = 6f;
        [SerializeField] private float _portalPulseRate = 1.5f;

        [Header("Integrity ring")]
        [Tooltip("Arc around the dial, in the dial's local space; full circle = full integrity.")]
        [SerializeField] private LineRenderer _integrityRing;
        [SerializeField] private float _ringRadius = 0.062f;
        [SerializeField] private int _ringSegments = 64;
        [SerializeField] private Color _healthyColor = new Color(0.2f, 0.95f, 0.3f);
        [SerializeField] private Color _damagedColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private Color _criticalColor = new Color(1f, 0.1f, 0.05f);
        [SerializeField] private float _criticalBlinkRate = 2f;
        [Tooltip("Integrity points per second the ring drains towards the real value.")]
        [SerializeField] private float _drainSpeed = 30f;

        private Transform[] _shipBlips;
        private Transform[] _portalBlips;
        private Vector3[] _ringPoints;
        private float _shownIntegrity = Balance.MaxIntegrity;
        private float _drawnIntegrity = -1f;

        private void Awake()
        {
            // All blips are created while loading and reused every frame (no allocations during the fight).
            _shipBlips = CreateBlips(_shipBlipPrefab, _maxShipBlips);
            _portalBlips = CreateBlips(_portalBlipPrefab, Balance.MaxPortals);
            _ringPoints = new Vector3[_ringSegments + 1];
            _display.SetActive(false);
        }

        private void OnEnable()
        {
            _game.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            _game.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            bool visible = state == GameState.Arrival || _game.InFight;
            if (visible && !_display.activeSelf)
            {
                // A new match starts with a full ring.
                _shownIntegrity = _game.Tower.Integrity;
                _drawnIntegrity = -1f;
            }
            _display.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (!_display.activeSelf)
            {
                return;
            }

            if (_faceTheEyes)
            {
                // Face the eyes, keeping the head's "up" so the top of the disc is always straight ahead.
                _face.rotation = Quaternion.LookRotation(_face.position - _head.position, _head.up);
            }
            else
            {
                // The dial lies flat on the wrist, but its picture turns like a phone screen so "ahead" is always at
                // the top as the player reads it, however the wrist is turned.
                Vector3 intoWrist = _display.transform.forward;
                Vector3 top = Vector3.ProjectOnPlane(_head.up, intoWrist);
                if (top.sqrMagnitude > 0.0001f)
                {
                    _face.rotation = Quaternion.LookRotation(intoWrist, top);
                }
            }

            // Everything is drawn relative to where the player looks (only the heading counts, not looking down).
            Vector3 forward = _head.forward;
            forward.y = 0f;
            Quaternion toHeading = forward.sqrMagnitude > 0.0001f
                ? Quaternion.Inverse(Quaternion.LookRotation(forward))
                : Quaternion.identity;

            int shown = 0;
            for (int i = 0; i < _ships.ActiveShips.Count && shown < _shipBlips.Length; i++)
            {
                ShipController ship = _ships.ActiveShips[i];
                bool aiming = ship.Ship.Phase == ShipPhase.Aiming;
                bool visible = !aiming || Mathf.Repeat(Time.time * _aimBlinkRate, 1f) < 0.6f;
                float size = ship.Ship.Type == ShipType.Bomber ? _bomberBlipSize : _fighterBlipSize;
                PlaceBlip(_shipBlips[shown], ship.transform.position, toHeading, size, visible);
                shown++;
            }
            HideFrom(_shipBlips, shown);

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * _portalPulseRate * 2f * Mathf.PI);
            int portals = Mathf.Min(_portals.Portals.Count, _portalBlips.Length);
            for (int i = 0; i < portals; i++)
            {
                PortalView portal = _portals.Portals[i];
                // A portal that is still opening shows small and grows with it.
                float size = _portalBlipSize * Mathf.Lerp(0.4f, 1f, portal.Openness) * pulse;
                PlaceBlip(_portalBlips[i], portal.transform.position, toHeading, size, true);
            }
            HideFrom(_portalBlips, portals);

            UpdateIntegrityRing();
        }

        // Clockwise arc from the top of the dial; its length is the integrity.
        private void UpdateIntegrityRing()
        {
            _shownIntegrity = Mathf.MoveTowards(_shownIntegrity, _game.Tower.Integrity, _drainSpeed * Time.deltaTime);
            if (Mathf.Abs(_shownIntegrity - _drawnIntegrity) > 0.05f)
            {
                _drawnIntegrity = _shownIntegrity;
                float sweep = Mathf.Clamp01(_shownIntegrity / Balance.MaxIntegrity) * Mathf.PI * 2f;
                for (int i = 0; i <= _ringSegments; i++)
                {
                    float angle = sweep * i / _ringSegments;
                    _ringPoints[i] = new Vector3(Mathf.Sin(angle) * _ringRadius, Mathf.Cos(angle) * _ringRadius, -0.002f);
                }
                _integrityRing.positionCount = _ringSegments + 1;
                _integrityRing.SetPositions(_ringPoints);
            }

            bool blinkOff = _game.Tower.IsCritical && Mathf.Repeat(Time.time * _criticalBlinkRate, 1f) >= 0.5f;
            Color color = _shownIntegrity >= 50f
                ? Color.Lerp(_damagedColor, _healthyColor, Mathf.InverseLerp(50f, 75f, _shownIntegrity))
                : Color.Lerp(_criticalColor, _damagedColor, Mathf.InverseLerp(25f, 50f, _shownIntegrity));
            _integrityRing.enabled = !blinkOff && _shownIntegrity > 0f;
            _integrityRing.startColor = color;
            _integrityRing.endColor = color;
        }

        // Puts a blip on the disc for a world position: right = right of the player's heading, up = ahead.
        private void PlaceBlip(Transform blip, Vector3 worldPosition, Quaternion toHeading, float size, bool visible)
        {
            Vector3 offset = toHeading * (worldPosition - _head.position);
            Vector2 flat = new Vector2(offset.x, offset.z) / _range * _radius;
            // Out of range: pinned to the rim, still pointing at the threat.
            flat = Vector2.ClampMagnitude(flat, _radius);

            blip.gameObject.SetActive(visible);
            blip.localPosition = new Vector3(flat.x, flat.y, -0.002f);
            blip.localScale = new Vector3(size, size, size);
        }

        private Transform[] CreateBlips(Transform prefab, int count)
        {
            var blips = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                blips[i] = Instantiate(prefab, _face);
                blips[i].gameObject.SetActive(false);
            }
            prefab.gameObject.SetActive(false);
            return blips;
        }

        private static void HideFrom(Transform[] blips, int first)
        {
            for (int i = first; i < blips.Length; i++)
            {
                if (blips[i].gameObject.activeSelf)
                {
                    blips[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
