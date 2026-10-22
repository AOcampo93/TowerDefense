using System;
using System.Collections;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Moves the experience between the real room (AR) and the tower (VR) by fading the passthrough layer.
    /// The virtual world is switched on or off when the fade crosses the halfway point, so the player never
    /// sees their room and the city at full strength at the same time.
    /// </summary>
    public class PassthroughFader : MonoBehaviour
    {
        // Below this passthrough opacity the game counts as VR.
        private const float VrThreshold = 0.5f;

        [SerializeField] private OVRPassthroughLayer _passthrough;
        [SerializeField] private GameObject _worldVR;
        [SerializeField] private Camera _eyeCamera;
        [Tooltip("Sky behind the virtual world until the sunset skybox arrives with the final art.")]
        [SerializeField] private Color _vrBackground = new Color(0.32f, 0.12f, 0.06f, 1f);

        /// <summary>True while the virtual world is shown (passthrough below half opacity).</summary>
        public bool IsVR { get; private set; }

        /// <summary>Raised when the experience switches between AR (false) and VR (true).</summary>
        public event Action<bool> OnModeChanged;

        /// <summary>
        /// Coroutine that fades the passthrough to a target opacity (1 = real room, 0 = virtual world).
        /// It is a coroutine so the GameController can run it alongside the elevator animation.
        /// </summary>
        public IEnumerator FadeTo(float opacity, float seconds)
        {
            float start = _passthrough.textureOpacity;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetOpacity(Mathf.Lerp(start, opacity, t / seconds), false);
                yield return null;
            }
            SetOpacity(opacity, false);
        }

        /// <summary>Jumps straight to an opacity, used when a state must start in a known mode.</summary>
        public void SetImmediate(float opacity)
        {
            SetOpacity(opacity, true);
        }

        private void SetOpacity(float opacity, bool forceMode)
        {
            _passthrough.textureOpacity = opacity;
            // A fully faded layer is hidden, which also pauses the camera feed and saves GPU and battery in VR.
            _passthrough.hidden = opacity <= 0f;

            bool vr = opacity < VrThreshold;
            if (forceMode || vr != IsVR)
            {
                SetMode(vr);
            }
        }

        private void SetMode(bool vr)
        {
            IsVR = vr;
            _worldVR.SetActive(vr);
            // Passthrough only shows where the eye buffer is transparent, so AR needs a fully clear background.
            _eyeCamera.backgroundColor = vr ? _vrBackground : Color.clear;
            OnModeChanged?.Invoke(vr);
        }
    }
}
