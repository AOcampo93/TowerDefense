using System;
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
            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                WriteJson(writer);
                return writer.ToString();
            }
        }

        /// <summary>
        /// Writes the recording to disk, creating the folder if needed. The text goes straight into the file in small
        /// pieces: building it whole first left megabytes of garbage after a long match, and the garbage collection
        /// it causes can freeze the headset for half a second right after the landing.
        /// </summary>
        public void Save(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(false)))
            {
                WriteJson(writer);
            }
        }

        private void WriteJson(TextWriter writer)
        {
            // One small buffer for every number, so a match of ten thousand samples does not create fifty thousand strings.
            var number = new char[32];
            writer.Write("{\"version\":1,\"samples\":[");
            for (int i = 0; i < _samples.Count; i++)
            {
                MotionSample s = _samples[i];
                if (i > 0)
                {
                    writer.Write(',');
                }
                writer.Write("{\"t\":");
                WriteNumber(writer, s.Time, number);
                writer.Write(",\"pitch\":");
                WriteNumber(writer, s.Pitch, number);
                writer.Write(",\"roll\":");
                WriteNumber(writer, s.Roll, number);
                writer.Write(",\"heave\":");
                WriteNumber(writer, s.Heave, number);
                writer.Write(",\"shake\":");
                WriteNumber(writer, s.Shake, number);
                writer.Write('}');
            }
            writer.Write("]}");
        }

        // Up to three decimals with a dot ("-12.5", "0.05", "3"), no matter the language of the device. Written digit
        // by digit, because formatting a float creates a new string every time, even with TryFormat.
        private static void WriteNumber(TextWriter writer, float value, char[] buffer)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                writer.Write('0');
                return;
            }
            long thousandths = (long)Math.Round(value * 1000.0, MidpointRounding.AwayFromZero);
            if (thousandths < 0)
            {
                writer.Write('-');
                thousandths = -thousandths;
            }

            // Whole part: the digits come out from the last one, so they are written back to front.
            long whole = thousandths / 1000;
            int length = 0;
            do
            {
                buffer[buffer.Length - 1 - length] = (char)('0' + (int)(whole % 10));
                whole /= 10;
                length++;
            }
            while (whole > 0);
            writer.Write(buffer, buffer.Length - length, length);

            // Decimals without the trailing zeros.
            int fraction = (int)(thousandths % 1000);
            if (fraction == 0)
            {
                return;
            }
            int digits = 3;
            while (fraction % 10 == 0)
            {
                fraction /= 10;
                digits--;
            }
            writer.Write('.');
            for (int i = digits - 1; i >= 0; i--)
            {
                buffer[i] = (char)('0' + fraction % 10);
                fraction /= 10;
            }
            writer.Write(buffer, 0, digits);
        }
    }
}
