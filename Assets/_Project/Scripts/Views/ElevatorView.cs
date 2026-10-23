using System.Collections;
using AlienInvaders.Game;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Greybox elevator cabin around the player's platform. The player is never moved: the cabin builds itself
    /// around them, light strips slide along slits in the walls and the structure of the shaft passes by above a
    /// glass dome, which is enough for the brain to feel the ride. The walls have no windows, so the city is never
    /// seen moving (comfort rule: no visible motion outside).
    /// </summary>
    public class ElevatorView : MonoBehaviour
    {
        // Share of the closing animation used by each part. They overlap so the motion never stops: the floor
        // appears first, then the walls rise and the roof closes last. Opening plays the same steps in reverse.
        private static readonly Vector2 FloorStage = new Vector2(0f, 0.15f);
        private static readonly Vector2 WallStage = new Vector2(0.1f, 0.8f);
        private static readonly Vector2 RoofStage = new Vector2(0.75f, 1f);

        [Header("Cabin")]
        [Tooltip("Wall cubes with their pivot at the center. Their size in the scene is the closed cabin.")]
        [SerializeField] private Transform[] _walls;
        [Tooltip("Glowing strip that rides the top edge of each wall while it grows or sinks (same order as Walls).")]
        [SerializeField] private Transform[] _wallEdges;
        [Tooltip("Floor plate and roof grow from the center of the cabin; their scene scale is the closed cabin.")]
        [SerializeField] private Transform _floor;
        [SerializeField] private Transform _roof;

        [Header("Shaft seen through the dome")]
        [SerializeField] private GameObject _shaft;
        [Tooltip("Shaft walls. They stop at the open end of the tunnel, where the dome shows the sky.")]
        [SerializeField] private Transform[] _shaftWalls;
        [Tooltip("Dark lid used while the end of the tunnel is still out of sight.")]
        [SerializeField] private GameObject _shaftCap;
        [Tooltip("Structural rings of the shaft. They slide down while the cabin goes up.")]
        [SerializeField] private Transform[] _rings;
        [SerializeField] private float _shaftBottom = 2.45f;
        [SerializeField] private float _shaftTop = 16.45f;

        [Header("Light strips")]
        [Tooltip("Light segments ordered slit by slit (Lights Per Slit segments for each wall).")]
        [SerializeField] private Renderer[] _lights;
        [SerializeField] private int _lightsPerSlit = 3;
        [SerializeField] private float _slitBottom = 0.15f;
        [SerializeField] private float _slitTop = 2.25f;
        [SerializeField] private float _lightLength = 0.25f;
        [SerializeField] private Material _rideLight;
        [SerializeField] private Material _alarmLight;
        [Tooltip("Meters per second the shaft slides past at full speed during the ascent.")]
        [SerializeField] private float _ascentLightSpeed = 7f;
        [SerializeField] private float _rescueLightSpeed = 14f;
        [Tooltip("Seconds to reach full speed and to stop again, so the ride starts and ends softly.")]
        [SerializeField] private float _rampSeconds = 2.5f;
        [Tooltip("The emergency cabin drops almost at once and brakes hard at the bottom.")]
        [SerializeField] private float _rescueRampSeconds = 0.6f;

        [Header("Haptics")]
        [Tooltip("Meters of shaft between two pulses (about one floor): the beat speeds up and slows down with the cabin.")]
        [SerializeField] private float _ascentMetersPerPulse = 1.75f;
        [SerializeField] private float _ascentPulseAmplitude = 0.8f;
        [SerializeField] private float _rescueMetersPerPulse = 1f;
        [SerializeField] private float _rescuePulseAmplitude = 1f;
        [SerializeField] private float _pulseSeconds = 0.1f;
        [Header("Sound")]
        [SerializeField] private GameSounds _sounds;
        [Tooltip("Machinery hum while the cabin moves (its pitch follows the speed) and the door sounds.")]
        [SerializeField] private AudioSource _machine;
        [SerializeField] private float _machineVolume = 0.6f;

        [Tooltip("Seconds the emergency cabin takes to slam shut during the rescue.")]
        [SerializeField] private float _rescueCloseSeconds = 0.4f;

        private Vector3[] _wallPositions;
        private Vector3[] _wallScales;
        private Vector3 _floorScale;
        private Vector3 _roofScale;
        private float _scroll;

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
            _floorScale = _floor.localScale;
            _roofScale = _roof.localScale;
        }

        /// <summary>Seconds the emergency cabin needs to close around the player at the start of the rescue.</summary>
        public float RescueCloseSeconds => _rescueCloseSeconds;

        /// <summary>
        /// The emergency cabin has stopped at street level: alarm lights off and no shaft above the dome, ready to
        /// open in the real room.
        /// </summary>
        public void ShowLanded()
        {
            SetLightsVisible(false);
            _shaft.SetActive(false);
        }

        /// <summary>Removes the cabin at once (AR calibration, or any state without an elevator).</summary>
        public void Hide()
        {
            SetClosedAmount(0f);
            SetLightsVisible(false);
            _shaft.SetActive(false);
            // A ride cut short by a state change must not leave the machinery humming.
            _machine.Stop();
        }

        /// <summary>
        /// Coroutine: the cabin builds itself around the player. Floor first, then the walls rise out of the real
        /// floor and the roof closes last, so in AR the player watches the elevator take shape in their room.
        /// </summary>
        public IEnumerator Close(float seconds)
        {
            yield return AnimateCabin(0f, 1f, seconds);
            PlayDoor(_sounds.DoorClose);
        }

        /// <summary>Coroutine: the roof opens to the sky and the walls sink under the player's feet.</summary>
        public IEnumerator Open(float seconds)
        {
            SetLightsVisible(false);
            _shaft.SetActive(false); // at the top there is no shaft above the cabin anymore
            PlayDoor(_sounds.DoorOpen);
            yield return AnimateCabin(1f, 0f, seconds);
        }

        /// <summary>
        /// Coroutine: the ride to the top. Lights slide down the slits and the shaft rings pass down over the dome
        /// (so the cabin feels like it goes up), with a soft haptic beat, as if passing floor after floor.
        /// </summary>
        public IEnumerator PlayAscent(float seconds)
        {
            yield return Ride(seconds, -1f, _ascentLightSpeed, _rampSeconds, _rideLight, _ascentMetersPerPulse, _ascentPulseAmplitude);
        }

        /// <summary>
        /// Coroutine: emergency descent. The cabin slams shut, red lights and the shaft rush upwards (the cabin
        /// falls) and both controllers shake hard.
        /// </summary>
        public IEnumerator PlayRescue(float seconds)
        {
            yield return AnimateCabin(0f, 1f, _rescueCloseSeconds);
            PlayDoor(_sounds.DoorClose);
            yield return Ride(seconds, 1f, _rescueLightSpeed, _rescueRampSeconds, _alarmLight, _rescueMetersPerPulse, _rescuePulseAmplitude);
        }

        private IEnumerator AnimateCabin(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                // Linear here; each part eases in and out on its own (see Stage).
                SetClosedAmount(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetClosedAmount(to);
        }

        // direction: -1 = shaft moves down (cabin going up), +1 = shaft moves up (cabin going down).
        private IEnumerator Ride(float seconds, float direction, float maxSpeed, float rampSeconds, Material material,
            float metersPerPulse, float pulseAmplitude)
        {
            foreach (Renderer strip in _lights)
            {
                strip.sharedMaterial = material;
            }
            SetLightsVisible(true);
            _shaft.SetActive(true);

            // Length of the whole ride, so the end of the tunnel reaches the dome exactly when the cabin stops.
            float distance = RideDistance(seconds, maxSpeed, rampSeconds);
            AudioClip hum = _sounds.ElevatorRide;
            if (hum != null)
            {
                _machine.clip = hum;
                _machine.loop = true;
                _machine.volume = 0f;
                _machine.Play();
            }
            float travelled = 0f;
            float nextPulse = metersPerPulse;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float ramp = Ramp(t, seconds, rampSeconds);
                float step = maxSpeed * ramp * Time.deltaTime;
                travelled += step;
                _scroll += direction * step;
                LayoutLights();
                // Going up, the open end of the tunnel comes down to meet the dome; falling, the tunnel grows above.
                LayoutShaft(direction < 0f ? distance - travelled : travelled);

                // The machinery hums louder and higher as the cabin speeds up (a falling cabin screams higher).
                _machine.volume = _machineVolume * ramp;
                _machine.pitch = Mathf.Lerp(0.6f, 1.1f, ramp) * (direction > 0f ? 1.25f : 1f);

                // One pulse every few meters, like passing floor after floor: the beat follows the speed,
                // and the pulses get stronger as the cabin reaches full speed.
                if (travelled >= nextPulse)
                {
                    nextPulse += metersPerPulse;
                    ControllerHaptics.PulseBoth(pulseAmplitude * Mathf.Lerp(0.4f, 1f, ramp), _pulseSeconds);
                }
                yield return null;
            }
            LayoutShaft(direction < 0f ? 0f : distance);
            _machine.Stop();
            _machine.pitch = 1f;
        }

        private void PlayDoor(AudioClip clip)
        {
            if (clip != null)
            {
                _machine.PlayOneShot(clip);
            }
        }

        // Speed factor from 0 to 1: it ramps up at the start and down at the end, like a real elevator.
        private static float Ramp(float t, float seconds, float rampSeconds)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(t, seconds - t) / rampSeconds);
        }

        // Meters travelled in a ride of this length (sum of speed x time over small steps).
        private static float RideDistance(float seconds, float maxSpeed, float rampSeconds)
        {
            const int Steps = 200;
            float dt = seconds / Steps;
            float distance = 0f;
            for (int i = 0; i < Steps; i++)
            {
                distance += maxSpeed * Ramp((i + 0.5f) * dt, seconds, rampSeconds) * dt;
            }
            return distance;
        }

        // 0 = no cabin, 1 = closed cabin.
        private void SetClosedAmount(float amount)
        {
            SetPlate(_floor, _floorScale, Stage(amount, FloorStage));
            SetWalls(Stage(amount, WallStage));
            SetPlate(_roof, _roofScale, Stage(amount, RoofStage));
        }

        // Progress of one part inside its share of the animation, eased so it starts and stops softly.
        private static float Stage(float amount, Vector2 range)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(range.x, range.y, amount));
        }

        // Walls keep their base on the floor while their height changes.
        private void SetWalls(float amount)
        {
            for (int i = 0; i < _walls.Length; i++)
            {
                Vector3 scale = _wallScales[i];
                scale.y *= amount;
                Vector3 position = _wallPositions[i];
                position.y -= (_wallScales[i].y - scale.y) * 0.5f;

                Transform wall = _walls[i];
                wall.gameObject.SetActive(amount > 0f);
                wall.localScale = scale;
                wall.localPosition = position;

                if (i < _wallEdges.Length)
                {
                    // The light strip marks where the wall is being "built", so the player sees it emerge.
                    Transform edge = _wallEdges[i];
                    edge.gameObject.SetActive(amount > 0f && amount < 1f);
                    Vector3 edgePosition = edge.localPosition;
                    edgePosition.y = position.y + scale.y * 0.5f;
                    edge.localPosition = edgePosition;
                }
            }
        }

        // Floor and roof grow from the center of the cabin outwards.
        private static void SetPlate(Transform plate, Vector3 fullScale, float amount)
        {
            plate.gameObject.SetActive(amount > 0f);
            plate.localScale = new Vector3(fullScale.x * amount, fullScale.y, fullScale.z * amount);
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

        // tunnelLength: meters of shaft above the roof before its open end (beyond the end the dome shows the sky).
        private void LayoutShaft(float tunnelLength)
        {
            float span = _shaftTop - _shaftBottom;
            float length = Mathf.Clamp(tunnelLength, 0f, span);
            foreach (Transform wall in _shaftWalls)
            {
                wall.gameObject.SetActive(length > 0f);
                Vector3 scale = wall.localScale;
                scale.y = length;
                wall.localScale = scale;
                Vector3 position = wall.localPosition;
                position.y = _shaftBottom + length * 0.5f;
                wall.localPosition = position;
            }
            // While the end is farther than the modeled shaft, a dark lid keeps the tunnel looking endless.
            _shaftCap.SetActive(tunnelLength > span);

            // The rings are fixed to the shaft, so they move with the same scroll as the light strips.
            float spacing = span / _rings.Length;
            float end = _shaftBottom + length;
            for (int i = 0; i < _rings.Length; i++)
            {
                Vector3 position = _rings[i].localPosition;
                position.y = _shaftBottom + Mathf.Repeat(i * spacing + _scroll, span);
                _rings[i].localPosition = position;
                _rings[i].gameObject.SetActive(position.y < end);
            }
        }

        private void SetLightsVisible(bool visible)
        {
            foreach (Renderer strip in _lights)
            {
                strip.enabled = visible;
            }
        }
    }
}
