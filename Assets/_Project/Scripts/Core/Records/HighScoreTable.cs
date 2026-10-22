using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AlienInvaders.Core.Records
{
    /// <summary>
    /// Local Top 10 saved to a JSON file on the headset (stretch challenge: read and write files).
    /// Reading never crashes the game: a missing or corrupted file just means an empty table.
    /// </summary>
    public class HighScoreTable
    {
        private readonly string _filePath;
        private readonly IRecordSerializer _serializer;
        private readonly List<MatchResult> _entries = new List<MatchResult>();

        public IReadOnlyList<MatchResult> Entries => _entries;

        /// <param name="filePath">Full path, e.g. Application.persistentDataPath + "/records.json".</param>
        public HighScoreTable(string filePath, IRecordSerializer serializer)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>Loads the table from disk. Missing or broken files result in an empty table.</summary>
        public void Load()
        {
            _entries.Clear();

            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                string text = File.ReadAllText(_filePath, Encoding.UTF8);
                List<MatchResult> loaded = _serializer.Deserialize(text);
                if (loaded != null)
                {
                    _entries.AddRange(loaded);
                }
                SortAndTrim();
            }
            catch (Exception)
            {
                // Corrupted file: start fresh instead of breaking the results screen.
                _entries.Clear();
            }
        }

        /// <summary>True if the score is good enough to enter the table.</summary>
        public bool Qualifies(MatchResult result)
        {
            if (_entries.Count < Balance.HighScoreEntries)
            {
                return true;
            }
            return Compare(result, _entries[_entries.Count - 1]) < 0;
        }

        /// <summary>
        /// Inserts the result in order and keeps only the best 10.
        /// Returns the position (0 = first place) or -1 if it did not qualify.
        /// </summary>
        public int Add(MatchResult result)
        {
            if (!Qualifies(result))
            {
                return -1;
            }

            int index = 0;
            while (index < _entries.Count && Compare(_entries[index], result) <= 0)
            {
                index++;
            }

            // The list is already sorted, so inserting at the right index keeps it sorted.
            _entries.Insert(index, result);
            if (_entries.Count > Balance.HighScoreEntries)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }
            return index;
        }

        /// <summary>
        /// Saves atomically: write to a temporary file first and then replace the real one,
        /// so a crash in the middle of saving never destroys the existing records.
        /// </summary>
        public void Save()
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, _serializer.Serialize(new List<MatchResult>(_entries)), Encoding.UTF8);

            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            File.Move(tempPath, _filePath);
        }

        /// <summary>
        /// Ranking rule: higher score first, then longer survival, then more kills.
        /// Returns a negative number when a ranks above b.
        /// </summary>
        public static int Compare(MatchResult a, MatchResult b)
        {
            if (a.Score != b.Score) return b.Score.CompareTo(a.Score);
            if (Math.Abs(a.SecondsSurvived - b.SecondsSurvived) > 0.001f) return b.SecondsSurvived.CompareTo(a.SecondsSurvived);
            return b.ShipsDestroyed.CompareTo(a.ShipsDestroyed);
        }

        private void SortAndTrim()
        {
            _entries.Sort(Compare);
            if (_entries.Count > Balance.HighScoreEntries)
            {
                _entries.RemoveRange(Balance.HighScoreEntries, _entries.Count - Balance.HighScoreEntries);
            }
        }
    }
}
