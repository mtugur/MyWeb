using S7.Net;

namespace MyWeb.Communication.Siemens
{
    /// <summary>
    /// appsettings.json’dan okunacak PLC bağlantı ayarları.
    /// </summary>
    public class PlcConnectionSettings
    {
        public CpuType CpuType { get; set; }
        public string IP { get; set; } = string.Empty;
        public short Rack { get; set; }
        public short Slot { get; set; }
    }
}
