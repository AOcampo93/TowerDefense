using System;
using System.Collections.Generic;
using System.IO;

namespace AlienInvaders.Core.Telemetry
{
    /// <summary>
    /// Play stats waiting to be sent, one JSON line each, in the order they happened. The headset is often offline,
    /// so the queue is saved to a file and what could not be sent goes out the next time there is internet. It never
    /// grows past a limit: when full, the oldest lines are dropped. Reading a missing or broken file just gives an
    /// empty queue, so stats can never stop the game.
    /// </summary>
    public class TelemetryQueue
    {
        private readonly List<string> _pending = new List<string>();
        private readonly int _capacity;

        /// <summary>A queue that keeps at most this many lines.</summary>
        public TelemetryQueue(int capacity)
        {
            _capacity = Math.Max(1, capacity);
        }

        /// <summary>Lines waiting to be sent.</summary>
        public int Count => _pending.Count;

        /// <summary>Adds a line at the end, dropping the oldest when the queue is full.</summary>
        public void Add(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }
            // One line per entry in the file.
            _pending.Add(json.Replace('\n', ' ').Replace('\r', ' '));
            if (_pending.Count > _capacity)
            {
                _pending.RemoveRange(0, _pending.Count - _capacity);
            }
        }

        /// <summary>A copy of the oldest lines, at most this many (they stay in the queue until removed).</summary>
        public List<string> Peek(int max)
        {
            return _pending.GetRange(0, Math.Min(Math.Max(0, max), _pending.Count));
        }

        /// <summary>Removes the oldest lines, after they were sent.</summary>
        public void Remove(int count)
        {
            _pending.RemoveRange(0, Math.Min(Math.Max(0, count), _pending.Count));
        }

        /// <summary>Writes the queue to a file (a temporary file first, so a crash never leaves half a file).</summary>
        public void Save(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string temp = path + ".tmp";
            File.WriteAllLines(temp, _pending);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temp, path);
        }

        /// <summary>Adds the lines saved in a file. Returns false (and adds nothing) if it is missing or unreadable.</summary>
        public bool Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }
                foreach (string line in File.ReadAllLines(path))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    {
                        Add(trimmed);
                    }
                }
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
