using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AlienInvaders.Core.Telemetry
{
    /// <summary>
    /// The JSON the Quest Games API expects. Players are only a random id made by the game on its first launch, so
    /// no personal data leaves the headset: the device model, the system language and what happened in the game.
    /// </summary>
    public static class TelemetryPayload
    {
        /// <summary>One event line: what happened, when (UTC), in which session, and its values.</summary>
        public static string Event(string type, string session, DateTime utcTime, JsonFields data)
        {
            return new StringBuilder("{")
                .Append("\"type\":").Append(JsonFields.Quote(type))
                .Append(",\"at\":").Append(JsonFields.Quote(utcTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)))
                .Append(",\"session\":").Append(JsonFields.Quote(session ?? string.Empty))
                .Append(",\"data\":").Append((data ?? new JsonFields()).ToJson())
                .Append('}')
                .ToString();
        }

        /// <summary>A batch of event lines (made by Event) with who sent them, for POST /v1/events.</summary>
        public static string Events(string game, string player, string session, string version, JsonFields device, IReadOnlyList<string> events)
        {
            var sb = new StringBuilder("{");
            sb.Append("\"game\":").Append(JsonFields.Quote(game))
              .Append(",\"player\":").Append(JsonFields.Quote(player))
              .Append(",\"session\":").Append(JsonFields.Quote(session ?? string.Empty))
              .Append(",\"version\":").Append(JsonFields.Quote(version ?? string.Empty))
              .Append(",\"device\":").Append((device ?? new JsonFields()).ToJson())
              .Append(",\"events\":[");
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append(events[i]);
            }
            return sb.Append("]}").ToString();
        }

        /// <summary>
        /// One feedback form for POST /v1/feedback: the stars (1 to 5, or 0 when not given), the topics to improve,
        /// the message, and a few values about the player's last match for context.
        /// </summary>
        public static string Feedback(string game, string player, string session, string version, int rating,
            IEnumerable<string> tags, string message, JsonFields context)
        {
            var form = new JsonFields()
                .With("game", game)
                .With("player", player)
                .With("session", session ?? string.Empty)
                .With("version", version ?? string.Empty);
            if (rating >= 1 && rating <= 5)
            {
                form.With("rating", rating);
            }
            form.With("tags", tags ?? new string[0]).With("message", message ?? string.Empty);
            // The context is an object of its own inside the form.
            string json = form.ToJson();
            return json.Substring(0, json.Length - 1) + ",\"context\":" + (context ?? new JsonFields()).ToJson() + "}";
        }
    }
}
