using Azure;

namespace MyWeb.Persistence.Catalog.Entities
{
    /// <summary>
    /// Proje üst kaydı: paket kimliği ve temel meta.
    /// Catalog şemasında tutulur; historian verisinden ayrıdır.
    /// </summary>
    public class Project
    {
        public int Id { get; set; }

        /// <summary>Dağıtımda benzersiz anahtar (manifest.key gibi düşün).</summary>
        public string Key { get; set; } = null!;

        public string Name { get; set; } = null!;

        /// <summary>Proje semantik sürümü (örn. 1.0.0).</summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>Kaydın oluşturulma UTC tarihi.</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>Çalışmak için gereken minimum engine sürümü (örn. >=1.0).</summary>
        public string MinEngine { get; set; } = ">=1.0";

        // Navs
        public ICollection<ProjectVersion> Versions { get; set; } = new List<ProjectVersion>();
        public ICollection<Controller> Controllers { get; set; } = new List<Controller>();
        public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    }
}
