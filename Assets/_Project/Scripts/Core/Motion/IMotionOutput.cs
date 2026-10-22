namespace AlienInvaders.Core.Motion
{
    /// <summary>
    /// Anything that can "play" the tower's motion: the virtual camera rig, controller haptics,
    /// a file recorder and, in the future, a physical actuator platform. The game logic never
    /// knows which outputs exist; it only publishes MotionSamples.
    /// </summary>
    public interface IMotionOutput
    {
        /// <summary>Applies one motion sample.</summary>
        void Apply(MotionSample sample);

        /// <summary>Returns smoothly to a level, motionless state.</summary>
        void ResetToLevel();
    }
}
