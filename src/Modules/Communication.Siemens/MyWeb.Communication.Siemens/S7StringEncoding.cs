using System;
using System.Linq;
using System.Text;

namespace MyWeb.Communication.Siemens
{
    /// <summary>
    /// Siemens S7 STRING / WSTRING kodlama/çözme yardımcıları.
    /// STRING: [0]=MaxLen, [1]=CurLen, payload (CP1254)
    /// WSTRING: [0..1]=MaxChars, [2..3]=CurChars (BE), payload = UTF-16BE
    /// </summary>
    internal static class S7StringEncoding
    {
        private static readonly Encoding Cp1254 = Encoding.GetEncoding(1254); // Program.cs'de RegisterProvider çağrılı.

        public static string DecodeString(byte[] buffer, int maxLen)
        {
            if (buffer == null || buffer.Length < 2) return string.Empty;
            int cur = Math.Min(buffer[1], maxLen);
            if (cur <= 0) return string.Empty;
            return Cp1254.GetString(buffer, 2, Math.Min(cur, buffer.Length - 2));
        }

        public static byte[] EncodeString(string s, int maxLen)
        {
            if (s == null) s = string.Empty;
            var payload = Cp1254.GetBytes(s);
            int cur = Math.Min(payload.Length, maxLen);
            var buf = new byte[maxLen + 2];
            buf[0] = (byte)maxLen;
            buf[1] = (byte)cur;
            Array.Copy(payload, 0, buf, 2, cur);
            return buf;
        }

        public static string DecodeWString(byte[] buffer, int maxChars)
        {
            if (buffer == null || buffer.Length < 4) return string.Empty;

            // Header BE: [0..1]=MaxChars, [2..3]=CurChars
            ushort cur = ReadUInt16BE(buffer, 2);
            int charCount = Math.Min(cur, (ushort)maxChars);
            int bytesNeeded = charCount * 2;

            if (buffer.Length < 4 + bytesNeeded) return string.Empty;

            var data = new byte[bytesNeeded];
            Array.Copy(buffer, 4, data, 0, bytesNeeded);

            // UTF-16 BE
            return Encoding.BigEndianUnicode.GetString(data);
        }

        public static byte[] EncodeWString(string s, int maxChars)
        {
            if (s == null) s = string.Empty;

            // S7-1500 WSTRING: UTF-16BE
            var bytes = Encoding.BigEndianUnicode.GetBytes(s);
            // bytes.Length = chars*2
            int maxBytes = maxChars * 2;
            int curBytes = Math.Min(bytes.Length, maxBytes);
            ushort curChars = (ushort)(curBytes / 2);

            var buf = new byte[4 + maxBytes];
            WriteUInt16BE(buf, 0, (ushort)maxChars);
            WriteUInt16BE(buf, 2, curChars);
            Array.Copy(bytes, 0, buf, 4, curBytes);
            return buf;
        }

        private static ushort ReadUInt16BE(byte[] b, int offset)
        {
            return (ushort)((b[offset] << 8) | b[offset + 1]);
        }

        private static void WriteUInt16BE(byte[] b, int offset, ushort value)
        {
            b[offset] = (byte)(value >> 8);
            b[offset + 1] = (byte)(value & 0xFF);
        }
    }
}
