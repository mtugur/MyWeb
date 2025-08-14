namespace MyWeb.Persistence.Catalog.Entities
{
    /// <summary>
    /// PLC/OPC/Modbus vb. kontrolcü tanımı (adres ve sürücü ayarları).
    /// </summary>
    public class Controller
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }

        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!;     // örn. "Siemens.S7", "OPCUA", "Modbus"
        public string Address { get; set; } = null!;  // örn. "192.168.0.10" ya da endpoint URL

        /// <summary>JSON olarak ek ayarlar (rack/slot, timeout, scan profili vb.).</summary>
        public string? SettingsJson { get; set; }

        // Nav
        public Project Project { get; set; } = null!;
    }
}
