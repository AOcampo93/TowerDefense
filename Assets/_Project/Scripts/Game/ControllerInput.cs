#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace AlienInvaders.Game
{
    /// <summary>The four flick directions of a thumbstick (menus such as the initials picker).</summary>
    public enum StickDirection
    {
        Up,
        Down,
        Left,
        Right
    }

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

        /// <summary>True only on the frame the right thumbstick is flicked in a direction. Arrow keys in the editor.</summary>
        public static bool RightStickFlicked(StickDirection direction)
        {
            if (OVRInput.GetDown(StickButton(direction), OVRInput.Controller.RTouch))
            {
                return true;
            }
#if UNITY_EDITOR
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }
            switch (direction)
            {
                case StickDirection.Up: return keyboard.upArrowKey.wasPressedThisFrame;
                case StickDirection.Down: return keyboard.downArrowKey.wasPressedThisFrame;
                case StickDirection.Left: return keyboard.leftArrowKey.wasPressedThisFrame;
                default: return keyboard.rightArrowKey.wasPressedThisFrame;
            }
#else
            return false;
#endif
        }

        private static OVRInput.Button StickButton(StickDirection direction)
        {
            switch (direction)
            {
                case StickDirection.Up: return OVRInput.Button.PrimaryThumbstickUp;
                case StickDirection.Down: return OVRInput.Button.PrimaryThumbstickDown;
                case StickDirection.Left: return OVRInput.Button.PrimaryThumbstickLeft;
                default: return OVRInput.Button.PrimaryThumbstickRight;
            }
        }
    }
}
