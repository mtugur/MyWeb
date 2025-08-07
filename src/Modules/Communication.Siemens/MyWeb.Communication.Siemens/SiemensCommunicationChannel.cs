using System;
using System.Collections.Generic;
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
            if (settings == null) throw new ArgumentNullException(nameof(settings));
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

        public bool IsConnected => _plc != null && _plc.IsConnected;

        public void AddTag(TagDefinition tag)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            _tags[tag.Name] = tag;
        }

        public bool RemoveTag(string tagName) => _tags.Remove(tagName);

        public T ReadTag<T>(string tagName)
        {
            if (!_tags.TryGetValue(tagName, out var tag))
                throw new KeyNotFoundException($"Tag '{tagName}' bulunamadı.");
            if (!Connect())
                throw new InvalidOperationException("PLC’ye bağlanılamadı.");

            object? raw = _plc.Read(tag.Address);
            if (raw is null)
                return default!;

            // VarType’a göre doğru dönüşüm
            object value = tag.VarType?.ToLowerInvariant() switch
            {
                "bit" => Convert.ToBoolean(raw),
                "byte" => Convert.ToByte(raw),
                "word" => Convert.ToUInt16(raw),
                "int" => unchecked((short)Convert.ToUInt16(raw)),
                "dword" => Convert.ToUInt32(raw),
                "udint" => Convert.ToUInt32(raw),
                "dint" => unchecked((int)Convert.ToUInt32(raw)),
                "real" => Convert.ToSingle(raw),
                "lreal" => Convert.ToDouble(raw),
                "string" => Convert.ToString(raw) ?? string.Empty,
                "sint" => unchecked((sbyte)Convert.ToByte(raw)),  // <<< SInt okuma
                _ => raw
            };

            return (T)Convert.ChangeType(value, typeof(T));
        }

        public Dictionary<string, object> ReadTags(IEnumerable<string> tagNames)
        {
            var result = new Dictionary<string, object>();
            foreach (var name in tagNames)
            {
                try
                {
                    result[name] = ReadTag<object>(name)!;
                }
                catch
                {
                    result[name] = null!;
                }
            }
            return result;
        }

        public bool WriteTag(string tagName, object value)
        {
            if (!_tags.TryGetValue(tagName, out var tag))
                throw new KeyNotFoundException($"Tag '{tagName}' bulunamadı.");
            if (!Connect())
                throw new InvalidOperationException("PLC’ye bağlanılamadı.");

            // 1) Gelen JsonElement'i gerçek .NET tipine çevir
            object actualValue = value;
            if (value is JsonElement je)
            {
                actualValue = tag.VarType?.ToLowerInvariant() switch
                {
                    "bit" => je.GetBoolean(),
                    "byte" => je.GetByte(),
                    "word" => je.GetInt32(),
                    "int" => je.GetInt32(),
                    "dword" => je.GetUInt32(),
                    "udint" => je.GetUInt32(),
                    "dint" => je.GetInt32(),
                    "real" => (float)je.GetDouble(),
                    "lreal" => je.GetDouble(),
                    "string" => je.GetString() ?? string.Empty,
                    "sint" => (sbyte)je.GetInt32(),          // <<< SInt JSON parse
                    _ => throw new NotSupportedException($"Desteklenmeyen VarType: '{tag.VarType}'")
                };
            }

            // 2) VarType’a göre .NET tipine çevirip yaz
            object writeVal = tag.VarType?.ToLowerInvariant() switch
            {
                "bit" => Convert.ToBoolean(actualValue),
                "byte" => Convert.ToByte(actualValue),
                "word" => Convert.ToUInt16(actualValue),
                "int" => Convert.ToInt16(actualValue),
                "dword" => Convert.ToUInt32(actualValue),
                "udint" => Convert.ToUInt32(actualValue),
                "dint" => Convert.ToInt32(actualValue),
                "real" => Convert.ToSingle(actualValue),
                "lreal" => Convert.ToDouble(actualValue),
                "string" => Convert.ToString(actualValue) ?? string.Empty,
                "sint" => unchecked((byte)Convert.ToSByte(actualValue)), // <<< SInt yazma
                _ => throw new NotSupportedException($"Desteklenmeyen VarType: '{tag.VarType}'")
            };

            _plc.Write(tag.Address, writeVal);
            return true;
        }

        public void Dispose()
        {
            Disconnect();
            // _plc.Dispose() gerek yok
        }
    }
}
