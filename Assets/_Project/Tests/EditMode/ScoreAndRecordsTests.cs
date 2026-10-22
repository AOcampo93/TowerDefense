using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AlienInvaders.Core;
using AlienInvaders.Core.Records;
using NUnit.Framework;

namespace AlienInvaders.Tests
{
    public class ScoreSystemTests
    {
        [Test]
        public void Calculate_UsesTimeKillsAndAccuracy()
        {
            // 120 s * 10 + 30 kills * 5 + 0.5 * 1000
            Assert.AreEqual(1200 + 150 + 500, ScoreSystem.Calculate(120.9f, 30, 0.5f));
        }

        [Test]
        public void Accuracy_IsZeroWithoutShots()
        {
            var score = new ScoreSystem();
            Assert.AreEqual(0f, score.Accuracy);
        }

        [Test]
        public void BuildResult_UppercasesInitials()
        {
            var score = new ScoreSystem();
            score.Tick(10f);
            score.RegisterShot(true);
            MatchResult r = score.BuildResult("art", 2, new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual("ART", r.Initials);
            Assert.AreEqual(100 + 1000, r.Score);
        }
    }

    public class HighScoreTableTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "ai_records_" + Guid.NewGuid().ToString("N") + ".txt");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        private static MatchResult Result(int score, float seconds = 0f) =>
            new MatchResult { Initials = "AAA", Score = score, SecondsSurvived = seconds };

        [Test]
        public void MissingFile_LoadsEmpty()
        {
            var table = new HighScoreTable(_path, new LineSerializer());
            table.Load();
            Assert.AreEqual(0, table.Entries.Count);
        }

        [Test]
        public void CorruptedFile_LoadsEmpty()
        {
            File.WriteAllText(_path, "this is not valid");
            var table = new HighScoreTable(_path, new LineSerializer());
            Assert.DoesNotThrow(table.Load);
            Assert.AreEqual(0, table.Entries.Count);
        }

        [Test]
        public void Add_KeepsOrder_AndOnlyTen()
        {
            var table = new HighScoreTable(_path, new LineSerializer());
            for (int i = 1; i <= 12; i++)
            {
                table.Add(Result(i * 100));
            }
            Assert.AreEqual(Balance.HighScoreEntries, table.Entries.Count);
            Assert.AreEqual(1200, table.Entries[0].Score);
            Assert.AreEqual(300, table.Entries[9].Score);
            Assert.AreEqual(-1, table.Add(Result(50)));
        }

        [Test]
        public void Tie_IsBrokenBySurvivalTime()
        {
            var table = new HighScoreTable(_path, new LineSerializer());
            table.Add(Result(500, 30f));
            int position = table.Add(Result(500, 60f));
            Assert.AreEqual(0, position);
        }

        [Test]
        public void SaveAndLoad_RoundTrip()
        {
            var table = new HighScoreTable(_path, new LineSerializer());
            table.Add(Result(900, 90f));
            table.Add(Result(400, 40f));
            table.Save();

            var reloaded = new HighScoreTable(_path, new LineSerializer());
            reloaded.Load();
            Assert.AreEqual(2, reloaded.Entries.Count);
            Assert.AreEqual(900, reloaded.Entries[0].Score);
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        /// <summary>Minimal engine-free serializer so the file logic can be tested anywhere.</summary>
        private class LineSerializer : IRecordSerializer
        {
            public string Serialize(List<MatchResult> entries)
            {
                var lines = new List<string>();
                foreach (MatchResult e in entries)
                {
                    lines.Add($"{e.Initials};{e.Score};{e.SecondsSurvived.ToString(CultureInfo.InvariantCulture)}");
                }
                return string.Join("\n", lines);
            }

            public List<MatchResult> Deserialize(string text)
            {
                var result = new List<MatchResult>();
                foreach (string line in text.Split('\n'))
                {
                    string[] parts = line.Split(';');
                    result.Add(new MatchResult
                    {
                        Initials = parts[0],
                        Score = int.Parse(parts[1], CultureInfo.InvariantCulture),
                        SecondsSurvived = float.Parse(parts[2], CultureInfo.InvariantCulture)
                    });
                }
                return result;
            }
        }
    }

    public class PackedRecordCodeTests
    {
        [Test]
        public void Fields_ShareMemoryWithRaw()
        {
            var code = new PackedRecordCode { Score = 0x01020304, Seconds = 0x0506, Wave = 0x07 };
            Assert.AreEqual(0x0007050601020304UL, code.Raw);
        }

        [Test]
        public void HexRoundTrip_IsValid()
        {
            var result = new MatchResult { Score = 2814, SecondsSurvived = 187.4f, WaveReached = 7 };
            PackedRecordCode code = PackedRecordCode.FromResult(result);

            Assert.IsTrue(PackedRecordCode.TryParse(code.ToHex(), out PackedRecordCode parsed));
            Assert.AreEqual(2814u, parsed.Score);
            Assert.AreEqual((ushort)187, parsed.Seconds);
            Assert.AreEqual((byte)7, parsed.Wave);
        }

        [Test]
        public void TamperedCode_FailsChecksum()
        {
            PackedRecordCode code = PackedRecordCode.FromResult(new MatchResult { Score = 100 });
            code.Score = 999999;
            Assert.IsFalse(PackedRecordCode.TryParse(code.ToHex(), out _));
        }
    }
}
