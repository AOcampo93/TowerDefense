using System.Collections;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Greybox elevator cabin around the player's platform. The player is never moved: the walls grow out of
    /// the floor and light strips slide along slits in the walls, which is enough for the brain to feel the ride.
    /// The cabin has no windows, so the city is never seen moving (comfort rule: no visible motion outside).
    /// </summary>
    public class ElevatorView : MonoBehaviour
    {
        [Header("Cabin")]
        [Tooltip("Wall cubes with their pivot at the center. Their size in the scene is the closed cabin.")]
        [SerializeField] private Transform[] _walls;
        [SerializeField] private Transform _roof;

        [Header("Light strips")]
        [Tooltip("Light segments ordered slit by slit (Lights Per Slit segments for each wall).")]
        [SerializeField] private Renderer[] _lights;
        [SerializeField] private int _lightsPerSlit = 3;
        [SerializeField] private float _slitBottom = 0.15f;
        [SerializeField] private float _slitTop = 2.25f;
        [SerializeField] private float _lightLength = 0.25f;
        [SerializeField] private Material _rideLight;
        [SerializeField] private Material _alarmLight;
        [Tooltip("Meters per second the lights slide at full speed during the ascent.")]
        [SerializeField] private float _ascentLightSpeed = 3f;
        [SerializeField] private float _rescueLightSpeed = 9f;
        [Tooltip("Seconds to reach full speed and to stop again, so the ride starts and ends softly.")]
        [SerializeField] private float _rampSeconds = 2.5f;

        [Header("Haptics")]
        [SerializeField] private float _ascentPulseInterval = 0.6f;
        [SerializeField] private float _ascentPulseAmplitude = 0.3f;
        [SerializeField] private float _rescuePulseInterval = 0.2f;
        [SerializeField] private float _rescuePulseAmplitude = 0.9f;
        [SerializeField] private float _pulseSeconds = 0.08f;
        [Tooltip("Seconds the emergency cabin takes to slam shut during the rescue.")]
        [SerializeField] private float _rescueCloseSeconds = 0.4f;

        private Vector3[] _wallPositions;
        private Vector3[] _wallScales;
        private Vector3 _roofScale;
        private float _scroll;
        private bool _vibrating;

        private void Awake()
        {
            // The scene holds the closed cabin; remember it so the animations can grow from and back to it.
            _wallPositions = new Vector3[_walls.Length];
            _wallScales = new Vector3[_walls.Length];
            for (int i = 0; i < _walls.Length; i++)
            {
                _wallPositions[i] = _walls[i].localPosition;
                _wallScales[i] = _walls[i].localScale;
            }
            _roofScale = _roof.localScale;
        }

        /// <summary>Removes the cabin at once (AR calibration, or any state without an elevator).</summary>
        public void Hide()
        {
            SetClosedAmount(0f);
            SetLightsVisible(false);
            SetVibration(0f);
        }

        /// <summary>Coroutine: the walls rise out of the floor and the roof closes over the player.</summary>
        public IEnumerator Close(float seconds)
        {
            yield return AnimateCabin(0f, 1f, seconds);
        }

        /// <summary>Coroutine: the walls sink under the player's feet and the roof opens to the sky.</summary>
        public IEnumerator Open(float seconds)
        {
            SetLightsVisible(false);
            yield return AnimateCabin(1f, 0f, seconds);
        }

        /// <summary>
        /// Coroutine: the ride to the top. Lights slide down the slits (so the cabin feels like it goes up)
        /// with a soft haptic beat, as if passing floor after floor.
        /// </summary>
        public IEnumerator PlayAscent(float seconds)
        {
            yield return Ride(seconds, -1f, _ascentLightSpeed, _rideLight, _ascentPulseInterval, _ascentPulseAmplitude);
        }

        /// <summary>
        /// Coroutine: emergency descent. The cabin slams shut, red lights rush up the slits (the cabin falls)
        /// and both controllers shake hard.
        /// </summary>
        public IEnumerator PlayRescue(float seconds)
        {
            yield return AnimateCabin(0f, 1f, _rescueCloseSeconds);
            yield return Ride(seconds, 1f, _rescueLightSpeed, _alarmLight, _rescuePulseInterval, _rescuePulseAmplitude);
        }

        private IEnumerator AnimateCabin(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetClosedAmount(Mathf.SmoothStep(from, to, t / seconds));
                yield return null;
            }
            SetClosedAmount(to);
        }

        // direction: -1 = lights move down (cabin going up), +1 = lights move up (cabin going down).
        private IEnumerator Ride(float seconds, float direction, float maxSpeed, Material material,
            float pulseInterval, float pulseAmplitude)
        {
            foreach (Renderer strip in _lights)
            {
                strip.sharedMaterial = material;
            }
            SetLightsVisible(true);

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                // Speed ramps up at the start and down at the end, like a real elevator.
                float ramp = Mathf.SmoothStep(0f, 1f, Mathf.Min(t, seconds - t) / _rampSeconds);
                _scroll += direction * maxSpeed * ramp * Time.deltaTime;
                LayoutLights();

                bool pulseOn = t % pulseInterval < _pulseSeconds;
                SetVibration(pulseOn ? pulseAmplitude * ramp : 0f);
                yield return null;
            }
            SetVibration(0f);
        }

        // 0 = no cabin, 1 = closed cabin. Walls keep their base on the floor while their height changes.
        private void SetClosedAmount(float amount)
        {
            bool visible = amount > 0f;
            for (int i = 0; i < _walls.Length; i++)
            {
                Vector3 scale = _wallScales[i];
                scale.y *= amount;
                Vector3 position = _wallPositions[i];
                position.y -= (_wallScales[i].y - scale.y) * 0.5f;

                Transform wall = _walls[i];
                wall.gameObject.SetActive(visible);
                wall.localScale = scale;
                wall.localPosition = position;
            }
            _roof.gameObject.SetActive(visible);
            _roof.localScale = new Vector3(_roofScale.x * amount, _roofScale.y, _roofScale.z * amount);
        }

        private void LayoutLights()
        {
            float slitLength = _slitTop - _slitBottom;
            float cycle = slitLength + _lightLength;
            for (int i = 0; i < _lights.Length; i++)
            {
                // Segments of one slit are spread evenly; every slit shares the pattern, like floors passing by.
                float phase = (i % _lightsPerSlit) * cycle / _lightsPerSlit;
                float center = _slitBottom - _lightLength * 0.5f + Mathf.Repeat(phase + _scroll, cycle);
                // Clip each segment to the slit so it slides in and out instead of popping at the ends.
                float bottom = Mathf.Max(center - _lightLength * 0.5f, _slitBottom);
                float top = Mathf.Min(center + _lightLength * 0.5f, _slitTop);

                Transform strip = _lights[i].transform;
                Vector3 position = strip.localPosition;
                position.y = (bottom + top) * 0.5f;
                strip.localPosition = position;
                Vector3 scale = strip.localScale;
                scale.y = Mathf.Max(0f, top - bottom);
                strip.localScale = scale;
            }
        }

        private void SetLightsVisible(bool visible)
        {
            foreach (Renderer strip in _lights)
            {
                strip.enabled = visible;
            }
        }

        private void SetVibration(float amplitude)
        {
            bool on = amplitude > 0f;
            if (on == _vibrating)
            {
                return; // only talk to the controllers when a pulse starts or ends
            }
            _vibrating = on;
            OVRInput.SetControllerVibration(0.5f, amplitude, OVRInput.Controller.Touch);
        }
    }
}
