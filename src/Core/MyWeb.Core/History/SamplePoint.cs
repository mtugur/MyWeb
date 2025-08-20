namespace MyWeb.Core.History;

/// <summary>Historian’a yazılacak tek bir örnek (sample) satırı.</summary>
public sealed class SamplePoint
{
    /// <summary>Örnekleme zamanı (UTC).</summary>
    public DateTime Utc { get; init; }

    /// <summary>Tag adı (örn: tBool).</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>Değerin string temsili. (Basitlik için nvarchar olarak saklıyoruz.)</summary>
    public string? Value { get; init; }

    /// <summary>Kalite (örn: Good/Bad).</summary>
    public string Quality { get; init; } = "Good";
}

namespace MyWeb.Core.History;

/// <summary>Sampling verilerini historian’a yazmak için kuyruk arayüzü.</summary>
public interface IHistoryWriter
{
    void Enqueue(IEnumerable<SamplePoint> items);
    void Enqueue(SamplePoint item);
}
