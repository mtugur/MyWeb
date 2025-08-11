using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using MyWeb.Core.Communication;
using S7.Net;

namespace MyWeb.Communication.Siemens
{
    public class SiemensCommunicationChannel : ICommunicationChannel, IDisposable
    {
        private readonly Plc _plc;
        private readonly Dictionary<string, TagDefinition> _tags = new();

        public SiemensCommunicationChannel(PlcConnectionSettings settings)
        {
            _plc = new Plc(settings.CpuType, settings.IP, settings.Rack, settings.Slot);
        }

        public bool Connect()
        {
            if (!_plc.IsConnected) _plc.Open();
            return _plc.IsConnected;
        }

        public void Disconnect()
        {
            if (_plc.IsConnected) _plc.Close();
        }

        public bool IsConnected => _plc?.IsConnected ?? false;

        public void AddTag(TagDefinition tag)
        {
            _tags[tag.Name] = tag;
        }

        public bool RemoveTag(string tagName) => _tags.Remove(tagName);

        public T ReadTag<T>(string tagName)
        {
            if (!_tags.TryGetValue(tagName, out var tag))
                throw new KeyNotFoundException($"Tag '{tagName}' bulunamadı.");
            Connect();

            var vt = tag.VarType?.ToLowerInvariant() ?? "";
            ParseDbAddress(tag.Address, out var dt, out var db, out var start);

            // — WSTRING için: 4 byte header + 2*Count bayt data —
            if (vt == "wstring")
            {
                int total = 4 + tag.Count * 2;
                var buf = _plc.ReadBytes(dt, db, start, total);
                // header: [0-1]=maxChars, [2-3]=actChars (Big endian)
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(buf, 0, 2);
                Array.Reverse(buf, 2, 2);
                ushort actualChars = BitConverter.ToUInt16(buf, 2);
                actualChars = (ushort)Math.Min(actualChars, (ushort)tag.Count);

                // Data portion: buf[4..4+2*actualChars]
                var strBytes = buf.Skip(4).Take(actualChars * 2).ToArray();
                string s = Encoding.BigEndianUnicode.GetString(strBytes);
                return (T)(object)s!;
            }

            // — STRING (1 byte header + data) —
            if (vt == "string")
            {
                int total = 2 + tag.Count;
                var buf = _plc.ReadBytes(dt, db, start, total);
                int len = Math.Min(buf.Length >= 2 ? buf[1] : 0, tag.Count);
                // Latin5/Turkish CP1254
                var cp = Encoding.GetEncoding(1254);
                var str = cp.GetString(buf, 2, len);
                return (T)(object)str!;
            }

            // — REAL / LREAL / diğer tipler… (önceki hali koruyabilirsiniz) —
            // … (diğer ReadTag<T> kodu) …

            // Basit örnek: direkt PLC.Read
            object raw = _plc.Read(tag.Address)!;
            return (T)Convert.ChangeType(raw, typeof(T));
        }

        public Dictionary<string, object> ReadTags(IEnumerable<string> tagNames) =>
            tagNames.ToDictionary(n => n, n => ReadTag<object>(n)!);

        public bool WriteTag(string tagName, object value)
        {
            if (!_tags.TryGetValue(tagName, out var tag))
                throw new KeyNotFoundException($"Tag '{tagName}' bulunamadı.");
            Connect();

            var vt = tag.VarType?.ToLowerInvariant() ?? "";
            ParseDbAddress(tag.Address, out var dt, out var db, out var start);

            object actual = value;
            if (value is JsonElement je)
            {
                // … (önceki JsonElement parse kodunuz) …
            }

            // — WSTRING yazma —
            if (vt == "wstring" && actual is string ws)
            {
                ushort max = (ushort)tag.Count;
                ushort act = (ushort)Math.Min(ws.Length, tag.Count);
                // Big-Endian Unicode
                var header = new byte[4];
                var maxB = BitConverter.GetBytes(max);
                var actB = BitConverter.GetBytes(act);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(maxB);
                    Array.Reverse(actB);
                }
                Array.Copy(maxB, 0, header, 0, 2);
                Array.Copy(actB, 0, header, 2, 2);

                var strBytes = Encoding.BigEndianUnicode.GetBytes(ws);
                var buf = new byte[4 + tag.Count * 2];
                Array.Copy(header, 0, buf, 0, 4);
                Array.Copy(strBytes, 0, buf, 4, Math.Min(strBytes.Length, tag.Count * 2));

                _plc.WriteBytes(dt, db, start, buf);
                return true;
            }

            // — STRING yazma (ASCII/CP1254) —
            if (vt == "string" && actual is string s)
            {
                var cp = Encoding.GetEncoding(1254);
                var data = cp.GetBytes(s);
                int len = Math.Min(data.Length, tag.Count);
                var buf = new byte[tag.Count + 2];
                buf[0] = (byte)tag.Count;
                buf[1] = (byte)len;
                Array.Copy(data, 0, buf, 2, len);
                _plc.WriteBytes(dt, db, start, buf);
                return true;
            }

            // — Diğer tiplere (örnek) —
            _plc.Write(tag.Address, actual);
            return true;
        }

        public void Dispose() => Disconnect();

        private void ParseDbAddress(string addr, out DataType dt, out int db, out int start)
        {
            dt = DataType.DataBlock;
            int dot = addr.IndexOf('.');
            db = int.Parse(addr.Substring(2, dot - 2));
            var after = addr.Substring(dot + 1);
            var num = new string(after.Where(char.IsDigit).ToArray());
            start = int.Parse(num);
        }
    }
}
