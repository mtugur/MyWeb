namespace MyWeb.Persistence.Catalog.Entities
{
    /// <summary>
    /// Projenin uygulanmış sürüm geçmişi (audit/geri dönüş için).
    /// </summary>
    public class ProjectVersion
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }

        /// <summary>Uygulanan proje sürümü (örn. 1.2.3).</summary>
        public string Version { get; set; } = null!;

        /// <summary>Bu sürümün uygulanma UTC zamanı.</summary>
        public DateTime AppliedUtc { get; set; }

        /// <summary>Paket veya manifest bütünlük hash'i.</summary>
        public string Hash { get; set; } = null!;

        // Nav
        public Project Project { get; set; } = null!;
    }
}
