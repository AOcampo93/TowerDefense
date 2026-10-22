using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XR;

namespace AlienInvaders.Game
{
    /// <summary>
    /// Vibrates the Quest controllers. The project runs on OpenXR, where the older OVRInput vibration call is
    /// silently ignored, so haptics go through the OpenXR controller devices of Unity's Input System instead.
    /// </summary>
    public static class ControllerHaptics
    {
        private static bool _reportedMissing;

        /// <summary>Short buzz on both controllers. Amplitude goes from 0 to 1, duration is in seconds.</summary>
        public static void PulseBoth(float amplitude, float seconds)
        {
            Pulse(UnityEngine.InputSystem.CommonUsages.LeftHand, amplitude, seconds);
            Pulse(UnityEngine.InputSystem.CommonUsages.RightHand, amplitude, seconds);
        }

        private static void Pulse(InternedString hand, float amplitude, float seconds)
        {
            XRControllerWithRumble controller = InputSystem.GetDevice<XRControllerWithRumble>(hand);
            if (controller == null)
            {
                // Logged once, so a missing controller profile is easy to spot in the headset log.
                if (!_reportedMissing)
                {
                    _reportedMissing = true;
                    Debug.Log("[Haptics] No OpenXR controller with vibration found");
                }
                return;
            }
            controller.SendImpulse(Mathf.Clamp01(amplitude), seconds);
        }
    }
}
