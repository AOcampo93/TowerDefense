using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace AlienInvaders.Core.Records
{
    /// <summary>
    /// A shareable "record code" that packs a result into a single 64-bit number.
    ///
    /// C# has no 'union' keyword like C/C++. LayoutKind.Explicit + FieldOffset lets several
    /// fields share the same bytes of memory, which is how a C-style union is emulated:
    /// writing Score/Seconds/Wave changes Raw, and writing Raw changes all the fields.
    ///
    /// Memory layout (8 bytes):
    ///   [0..3] Score   [4..5] Seconds   [6] Wave   [7] Checksum
    ///   [0...............7] Raw
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct PackedRecordCode
    {
        [FieldOffset(0)] public ulong Raw;
        [FieldOffset(0)] public uint Score;
        [FieldOffset(4)] public ushort Seconds;
        [FieldOffset(6)] public byte Wave;
        [FieldOffset(7)] public byte Checksum;

        /// <summary>Packs a match result (values are clamped to the size of each field).</summary>
        public static PackedRecordCode FromResult(MatchResult result)
        {
            var code = new PackedRecordCode
            {
                Score = (uint)Math.Max(0, result.Score),
                Seconds = (ushort)Math.Min(ushort.MaxValue, Math.Max(0, (int)result.SecondsSurvived)),
                Wave = (byte)Math.Min(byte.MaxValue, Math.Max(0, result.WaveReached))
            };
            code.Checksum = code.ComputeChecksum();
            return code;
        }

        /// <summary>Simple checksum (sum of the first 7 bytes) to detect typos in a shared code.</summary>
        public byte ComputeChecksum()
        {
            ulong value = Raw;
            int sum = 0;
            for (int i = 0; i < 7; i++)
            {
                sum += (int)((value >> (i * 8)) & 0xFF);
            }
            return (byte)(sum & 0xFF);
        }

        public bool IsValid => Checksum == ComputeChecksum();

        /// <summary>16-character hexadecimal text shown on the results screen.</summary>
        public string ToHex() => Raw.ToString("X16", CultureInfo.InvariantCulture);

        /// <summary>Parses a code typed by a player. Returns false if it is malformed or fails the checksum.</summary>
        public static bool TryParse(string hex, out PackedRecordCode code)
        {
            code = default;
            if (string.IsNullOrWhiteSpace(hex) ||
                !ulong.TryParse(hex.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong raw))
            {
                return false;
            }

            code.Raw = raw;
            return code.IsValid;
        }
    }
}
