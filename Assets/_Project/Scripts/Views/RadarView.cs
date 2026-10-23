using AlienInvaders.Core;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Tactical watch on the left wrist (diegetic radar, no HUD). It is a small disc that turns towards the eyes, with
    /// the player in the middle and "where I am looking" always at the top: red blips are ships (blinking while they
    /// aim at the tower), purple blips are portals (pulsing, also while they open). Ships beyond the range stick to
    /// the edge, so threats behind the player are never invisible.
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
        [SerializeField] private Transform _shipBlipPrefab;
        [SerializeField] private Transform _portalBlipPrefab;

        [Header("Scale")]
        [Tooltip("Radius of the disc in meters (a 10 cm watch).")]
        [SerializeField] private float _radius = 0.045f;
        [Tooltip("Meters of the real world shown from the center to the edge.")]
        [SerializeField] private float _range = 50f;
        [SerializeField] private int _maxShipBlips = 56;

        [Header("Feel")]
        [SerializeField] private float _fighterBlipSize = 0.005f;
        [SerializeField] private float _bomberBlipSize = 0.008f;
        [SerializeField] private float _portalBlipSize = 0.011f;
        [SerializeField] private float _aimBlinkRate = 6f;
        [SerializeField] private float _portalPulseRate = 1.5f;

        private Transform[] _shipBlips;
        private Transform[] _portalBlips;

        private void Awake()
        {
            // All blips are created while loading and reused every frame (no allocations during the fight).
            _shipBlips = CreateBlips(_shipBlipPrefab, _maxShipBlips);
            _portalBlips = CreateBlips(_portalBlipPrefab, Balance.MaxPortals);
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
            _display.SetActive(state == GameState.Arrival || _game.InFight);
        }

        private void LateUpdate()
        {
            if (!_display.activeSelf)
            {
                return;
            }

            // Face the eyes, keeping the head's "up" so the top of the disc is always straight ahead.
            _face.rotation = Quaternion.LookRotation(_face.position - _head.position, _head.up);

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
