using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AlienInvaders.Core.Motion
{
    /// <summary>
    /// Motion output that records every sample and saves the whole match as a JSON "motion
    /// track" (stretch challenge: writing files). In the future the same file can be replayed
    /// on the actuator platform without wearing the headset.
    /// </summary>
    public class MotionRecorder : IMotionOutput
    {
        private readonly List<MotionSample> _samples = new List<MotionSample>();

        public IReadOnlyList<MotionSample> Samples => _samples;

        public void Apply(MotionSample sample)
        {
            _samples.Add(sample);
        }

        public void ResetToLevel()
        {
            // Recording keeps going; a reset is just one more (level) sample in the track.
        }

        /// <summary>Removes all recorded samples (start of a new match).</summary>
        public void Clear()
        {
            _samples.Clear();
        }

        /// <summary>
        /// A copy of the samples recorded so far. The game saves the copy on a worker thread, so writing a whole
        /// match never freezes the headset, and the next match can start recording into this one meanwhile.
        /// </summary>
        public MotionRecorder Snapshot()
        {
            var copy = new MotionRecorder();
            copy._samples.AddRange(_samples);
            return copy;
        }

        /// <summary>
        /// Builds the JSON text by hand with InvariantCulture, so decimals always use a dot
        /// no matter the language of the device (Portuguese/Spanish devices use a comma).
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"version\":1,\"samples\":[");
            for (int i = 0; i < _samples.Count; i++)
            {
                MotionSample s = _samples[i];
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append("{\"t\":").Append(Format(s.Time))
                  .Append(",\"pitch\":").Append(Format(s.Pitch))
                  .Append(",\"roll\":").Append(Format(s.Roll))
                  .Append(",\"heave\":").Append(Format(s.Heave))
                  .Append(",\"shake\":").Append(Format(s.Shake))
                  .Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>Writes the recording to disk, creating the folder if needed.</summary>
        public void Save(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(filePath, ToJson(), Encoding.UTF8);
        }

        private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
