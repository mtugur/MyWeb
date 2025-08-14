using MyWeb.Core.Hist;

namespace MyWeb.Persistence.Catalog.Entities
{
    /// <summary>
    /// Tag bazlı arşiv politikası ve parametreleri.
    /// </summary>
    public class TagArchiveConfig
    {
        public int Id { get; set; }
        public int TagId { get; set; }

        /// <summary>Always / ChangeOnly / Deadband.</summary>
        public ArchiveMode Mode { get; set; } = ArchiveMode.ChangeOnly;

        /// <summary>Mutlak deadband (örn. 0.5).</summary>
        public double? DeadbandAbs { get; set; }

        /// <summary>Yüzdesel deadband (örn. 0.01 = %1).</summary>
        public double? DeadbandPercent { get; set; }

        /// <summary>Bu tag için ham verinin saklama günü (örn. 365). Null ise global.</summary>
        public int? RetentionDays { get; set; }

        /// <summary>JSON list: ["1m","1h"] gibi rollup kovaları.</summary>
        public string? RollupsJson { get; set; }

        // Nav
        public Tag Tag { get; set; } = null!;
    }
}
