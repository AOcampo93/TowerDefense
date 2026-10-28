using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AlienInvaders.Core.Telemetry
{
    /// <summary>
    /// A small JSON object built by hand: named texts, numbers, flags and lists of texts. The play stats and the
    /// feedback are sent as JSON, but the core cannot use the engine's JSON tools, and these few values do not need a
    /// library. Numbers always use a dot (InvariantCulture), whatever the language of the headset.
    /// </summary>
    public class JsonFields
    {
        private readonly List<KeyValuePair<string, string>> _fields = new List<KeyValuePair<string, string>>();

        /// <summary>Number of values added.</summary>
        public int Count => _fields.Count;

        /// <summary>Adds a text value.</summary>
        public JsonFields With(string key, string value) => Add(key, value == null ? "null" : Quote(value));

        /// <summary>Adds a whole number.</summary>
        public JsonFields With(string key, int value) => Add(key, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Adds a number with up to three decimals.</summary>
        public JsonFields With(string key, double value) => Add(key, Number(value));

        /// <summary>Adds a true or false flag.</summary>
        public JsonFields With(string key, bool value) => Add(key, value ? "true" : "false");

        /// <summary>Adds a list of texts.</summary>
        public JsonFields With(string key, IEnumerable<string> values)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (string value in values)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                sb.Append(Quote(value ?? string.Empty));
                first = false;
            }
            return Add(key, sb.Append(']').ToString());
        }

        /// <summary>The values as a JSON object, in the order they were added.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < _fields.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append(Quote(_fields[i].Key)).Append(':').Append(_fields[i].Value);
            }
            return sb.Append('}').ToString();
        }

        /// <summary>A text between quotes, with quotes, backslashes and control characters escaped.</summary>
        public static string Quote(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>A number with up to three decimals; NaN and infinity (not valid JSON) become 0.</summary>
        public static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "0";
            }
            return Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private JsonFields Add(string key, string rawJson)
        {
            _fields.Add(new KeyValuePair<string, string>(key, rawJson));
            return this;
        }
    }
}
