#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace AlienInvaders.Game
{
    /// <summary>
    /// Single place that reads the Quest controllers. Every script asks the same questions
    /// ("was the right trigger pressed?"), so if the Meta SDK input API changes only this file changes.
    /// In the Unity editor the keyboard stands in for the trigger, which lets the whole game flow
    /// be tested without a headset.
    /// </summary>
    public static class ControllerInput
    {
        // How far the trigger has to be pulled to count as held (0 = released, 1 = fully pressed).
        private const float TriggerHeldThreshold = 0.5f;

        /// <summary>True every frame the right index trigger is held down (continuous fire).</summary>
        public static bool RightTriggerHeld()
        {
            if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) > TriggerHeldThreshold)
            {
                return true;
            }
#if UNITY_EDITOR
            // Editor stand-in: holding Space holds the right trigger.
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.isPressed;
#else
            return false;
#endif
        }

        /// <summary>True only on the frame the right index trigger goes down (calibration, buttons).</summary>
        public static bool RightTriggerDown()
        {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                return true;
            }
#if UNITY_EDITOR
            // Editor stand-in: Space acts as the right trigger.
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
