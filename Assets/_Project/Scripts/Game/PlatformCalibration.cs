using System;
using UnityEngine;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Lines up the virtual platform with the player. The start screen asks them to hold the right controller
    /// against their chest (or over the center of a real platform) and pull the trigger: the game origin
    /// (GameRoot) moves there, so they stand in the middle of the platform, and turns to face where they are
    /// looking. The result is saved, so the next session starts from the same spot until it is confirmed again.
    /// </summary>
    public class PlatformCalibration : MonoBehaviour
    {
        private const string KeyX = "calibration.x";
        private const string KeyZ = "calibration.z";
        private const string KeyYaw = "calibration.yaw";

        [SerializeField] private OVRCameraRig _cameraRig;
        [SerializeField] private Transform _gameRoot;
        [Tooltip("AR panel with the instructions, shown only while calibrating.")]
        [SerializeField] private GameObject _panel;

        private bool _waitingForTrigger;
        private int _beginFrame;

        /// <summary>Raised after the player confirms the platform position with the trigger.</summary>
        public event Action OnCalibrated;

        /// <summary>True when a previous calibration is stored on this device.</summary>
        public bool HasSavedCalibration => PlayerPrefs.HasKey(KeyYaw);

        /// <summary>Shows the instructions and waits for the right trigger.</summary>
        public void Begin()
        {
            _panel.SetActive(true);
            _waitingForTrigger = true;
            // The trigger press that started calibration (the "Play again" button) must not also calibrate.
            _beginFrame = Time.frameCount;
        }

        /// <summary>Hides the instructions and stops listening to the trigger.</summary>
        public void End()
        {
            _panel.SetActive(false);
            _waitingForTrigger = false;
        }

        private void Update()
        {
            if (_waitingForTrigger && Time.frameCount != _beginFrame && ControllerInput.RightTriggerDown())
            {
                Calibrate();
            }
        }

        /// <summary>
        /// Uses the right controller as the platform center and the head direction as "forward".
        /// Only the yaw of the head is kept: the platform must stay level even if the player looks down.
        /// </summary>
        public void Calibrate()
        {
            Vector3 center = _cameraRig.rightHandAnchor.position;
            Vector3 look = _cameraRig.centerEyeAnchor.forward;
            look.y = 0f;
            float yaw = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;

            Apply(center.x, center.z, yaw);
            PlayerPrefs.SetFloat(KeyX, center.x);
            PlayerPrefs.SetFloat(KeyZ, center.z);
            PlayerPrefs.SetFloat(KeyYaw, yaw);
            PlayerPrefs.Save();
            Debug.Log($"[Calibration] Platform at ({center.x:F2}, {center.z:F2}), yaw {yaw:F0} deg");

            End();
            OnCalibrated?.Invoke();
        }

        /// <summary>Restores the last saved calibration. Returns false when there is none.</summary>
        public bool LoadSaved()
        {
            if (!HasSavedCalibration)
            {
                return false;
            }
            Apply(PlayerPrefs.GetFloat(KeyX), PlayerPrefs.GetFloat(KeyZ), PlayerPrefs.GetFloat(KeyYaw));
            return true;
        }

        private void Apply(float x, float z, float yaw)
        {
            // The rig uses a floor-level origin, so the tracking space height is the real floor.
            float floor = _cameraRig.trackingSpace.position.y;
            _gameRoot.SetPositionAndRotation(new Vector3(x, floor, z), Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
