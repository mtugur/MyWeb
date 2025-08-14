using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MyWeb.Persistence.Common
{
    /// <summary>
    /// DB'ye yazarken ve DB'den okurken DateTime değerlerinin UTC olduğundan emin olur.
    /// Yerel/Zaman dilimi karmaşasını engeller (SCADA zaman serisinde kritik).
    /// </summary>
    public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public static readonly UtcDateTimeConverter Instance = new();

        public UtcDateTimeConverter()
            : base(
                toDb => DateTime.SpecifyKind(toDb, DateTimeKind.Utc),
                fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc))
        { }
    }
}
