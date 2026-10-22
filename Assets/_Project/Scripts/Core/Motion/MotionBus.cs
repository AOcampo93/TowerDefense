using System.Collections.Generic;

namespace AlienInvaders.Core.Motion
{
    /// <summary>
    /// Distributes every motion sample to all registered outputs. Adding the future actuator
    /// platform will only require registering one more output here.
    /// </summary>
    public class MotionBus
    {
        private readonly List<IMotionOutput> _outputs = new List<IMotionOutput>();

        public int OutputCount => _outputs.Count;

        public void Register(IMotionOutput output)
        {
            if (output != null && !_outputs.Contains(output))
            {
                _outputs.Add(output);
            }
        }

        public void Unregister(IMotionOutput output)
        {
            _outputs.Remove(output);
        }

        /// <summary>Sends the sample to every output.</summary>
        public void Publish(MotionSample sample)
        {
            for (int i = 0; i < _outputs.Count; i++)
            {
                _outputs[i].Apply(sample);
            }
        }

        /// <summary>Asks every output to go back to level (pause, end of match, emergency).</summary>
        public void ResetAll()
        {
            for (int i = 0; i < _outputs.Count; i++)
            {
                _outputs[i].ResetToLevel();
            }
        }
    }
}
