using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// Turns the 360 sky very slowly so the clouds drift, and turns the sun light with it so shadows and highlights
    /// always come from the sun painted in the sky. The rotation is a global shader value: the sky material asset is
    /// never changed while the game runs.
    /// </summary>
    public class SkyController : MonoBehaviour
    {
        private static readonly int RotationId = Shader.PropertyToID("_AI_SkyRotation");

        [SerializeField] private Light _sun;
        [Tooltip("Compass direction of the sun in the panorama when it is not turned (degrees from +Z towards +X).")]
        [SerializeField] private float _sunAzimuth = 38.5f;
        [SerializeField] private float _sunElevation = 45f;
        [Tooltip("Degrees per second the sky turns (the clouds drift; the sun moves with them).")]
        [SerializeField] private float _degreesPerSecond = 0.05f;

        private float _turn;

        /// <summary>Degrees the sky has turned so far, so the clouds around the horizon can drift with it.</summary>
        public float Turn => _turn;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            _turn = Mathf.Repeat(_turn + _degreesPerSecond * Time.deltaTime, 360f);
            Apply();
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(RotationId, 0f);
        }

        private void Apply()
        {
            // Sampling further along the panorama moves its features the other way across the sky.
            Shader.SetGlobalFloat(RotationId, _turn / 360f);
            _sun.transform.rotation = Quaternion.Euler(_sunElevation, _sunAzimuth - _turn + 180f, 0f);
        }
    }
}
