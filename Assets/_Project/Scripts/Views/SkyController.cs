using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Turns the 360 sky very slowly so the clouds drift, and turns the sun light with it so shadows and highlights
    /// always come from the sun painted in the sky. When the tower leans, the sky and the sun lean with the city, so
    /// the player sees the whole world (city, clouds and sky together) tilt around them, as from a leaning tower,
    /// instead of the city sliding against a sky that stays still. The rotations are global shader values: the sky
    /// material asset is never changed while the game runs. Runs after CameraMotionOutput has leaned the world for
    /// this frame, so the sky never lags one frame behind the city.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class SkyController : MonoBehaviour
    {
        private static readonly int RotationId = Shader.PropertyToID("_AI_SkyRotation");
        private static readonly int TiltId = Shader.PropertyToID("_AI_SkyTilt");

        [SerializeField] private Light _sun;
        [Tooltip("The leaning world (TiltPivot): the sky and the sun lean with it.")]
        [SerializeField] private Transform _world;
        [Tooltip("Compass direction of the sun in the panorama when it is not turned (degrees from +Z towards +X).")]
        [SerializeField] private float _sunAzimuth = 38.5f;
        [SerializeField] private float _sunElevation = 45f;
        [Tooltip("Degrees per second the sky turns (the clouds drift; the sun moves with them).")]
        [SerializeField] private float _degreesPerSecond = 0.05f;

        private float _turn;

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            _turn = Mathf.Repeat(_turn + _degreesPerSecond * Time.deltaTime, 360f);
            Apply();
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(RotationId, 0f);
            Shader.SetGlobalVector(TiltId, Vector4.zero);
        }

        private void Apply()
        {
            Quaternion tilt = WorldTilt();
            // Sampling further along the panorama moves its features the other way across the sky.
            Shader.SetGlobalFloat(RotationId, _turn / 360f);
            // The sky shader turns each view direction back by the tilt to find the level sky it shows.
            Quaternion back = Quaternion.Inverse(tilt);
            Shader.SetGlobalVector(TiltId, new Vector4(back.x, back.y, back.z, back.w));
            _sun.transform.rotation = tilt * Quaternion.Euler(_sunElevation, _sunAzimuth - _turn + 180f, 0f);
        }

        // How far the world is leaning right now, as a rotation in world space (none when level).
        private Quaternion WorldTilt()
        {
            if (_world == null || _world.parent == null)
            {
                return Quaternion.identity;
            }
            return _world.rotation * Quaternion.Inverse(_world.parent.rotation);
        }
    }
}
