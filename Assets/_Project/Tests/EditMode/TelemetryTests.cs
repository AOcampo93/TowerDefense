using System;
using System.Collections.Generic;
using System.IO;
using AlienInvaders.Core.Telemetry;
using NUnit.Framework;

namespace AlienInvaders.Tests
{
    public class TelemetryTests
    {
        [Test]
        public void Fields_EscapeTextAndUseADot()
        {
            string json = new JsonFields()
                .With("message", "Say \"hi\"\nnow \\ ok")
                .With("fps", 71.23456)
                .With("wave", 13)
                .With("record", true)
                .With("tags", new[] { "SOUND", "BUGS" })
                .ToJson();
            Assert.AreEqual("{\"message\":\"Say \\\"hi\\\"\\nnow \\\\ ok\",\"fps\":71.235,\"wave\":13,\"record\":true,\"tags\":[\"SOUND\",\"BUGS\"]}", json);
        }

        [Test]
        public void Fields_InvalidNumbersBecomeZero()
        {
            Assert.AreEqual("{\"a\":0,\"b\":0}", new JsonFields().With("a", double.NaN).With("b", double.PositiveInfinity).ToJson());
        }

        [Test]
        public void Payload_EventHasTypeTimeSessionAndData()
        {
            string json = TelemetryPayload.Event("match_end", "s1", new DateTime(2026, 10, 28, 14, 5, 9, 250, DateTimeKind.Utc),
                new JsonFields().With("seconds", 371));
            Assert.AreEqual("{\"type\":\"match_end\",\"at\":\"2026-10-28T14:05:09.250Z\",\"session\":\"s1\",\"data\":{\"seconds\":371}}", json);
        }

        [Test]
        public void Payload_EventsBatchWrapsTheLines()
        {
            var lines = new List<string> { "{\"type\":\"a\"}", "{\"type\":\"b\"}" };
            string json = TelemetryPayload.Events("alien-invaders", "p1", "s1", "1.0", new JsonFields().With("model", "Quest 3"), lines);
            Assert.AreEqual("{\"game\":\"alien-invaders\",\"player\":\"p1\",\"session\":\"s1\",\"version\":\"1.0\",\"device\":{\"model\":\"Quest 3\"},\"events\":[{\"type\":\"a\"},{\"type\":\"b\"}]}", json);
        }

        [Test]
        public void Payload_FeedbackLeavesOutAMissingRating()
        {
            string json = TelemetryPayload.Feedback("alien-invaders", "p1", "s1", "1.0", 0, new[] { "COMFORT" }, "More waves", new JsonFields().With("wave", 9));
            StringAssert.DoesNotContain("rating", json);
            StringAssert.Contains("\"tags\":[\"COMFORT\"],\"message\":\"More waves\",\"context\":{\"wave\":9}}", json);
            StringAssert.Contains("\"rating\":4", TelemetryPayload.Feedback("g", "p", "s", "1", 4, null, null, null));
        }

        [Test]
        public void Queue_DropsTheOldestWhenFull()
        {
            var queue = new TelemetryQueue(3);
            for (int i = 1; i <= 5; i++)
            {
                queue.Add("{\"n\":" + i + "}");
            }
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual("{\"n\":3}", queue.Peek(1)[0]);
            queue.Remove(2);
            Assert.AreEqual("{\"n\":5}", queue.Peek(10)[0]);
        }

        [Test]
        public void Queue_SurvivesASaveAndALoad_AndIgnoresBrokenFiles()
        {
            string path = Path.Combine(Path.GetTempPath(), "ai_telemetry_" + Guid.NewGuid().ToString("N") + ".jsonl");
            try
            {
                var queue = new TelemetryQueue(10);
                queue.Add("{\"type\":\"a\"}");
                queue.Add("{\"type\":\"b\"}");
                queue.Save(path);
                var loaded = new TelemetryQueue(10);
                Assert.IsTrue(loaded.Load(path));
                Assert.AreEqual(2, loaded.Count);

                File.WriteAllText(path, "not json\n{\"type\":\"c\"}\n");
                var partial = new TelemetryQueue(10);
                partial.Load(path);
                Assert.AreEqual(1, partial.Count);
                Assert.IsFalse(new TelemetryQueue(10).Load(path + ".missing"));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
