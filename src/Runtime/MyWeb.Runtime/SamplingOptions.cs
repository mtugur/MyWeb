namespace MyWeb.Runtime;

/// <summary>
/// Numune (sampling) seçenekleri: periyot ve örneklenecek etiket listesi.
/// Step-2'de gerçek PLC okuma ve SQL yazma eklenecek.
/// </summary>
public sealed class SamplingOptions
{
    /// <summary>Örnekleme periyodu (ms). Varsayılan: 1000 ms</summary>
    public int PollMs { get; set; } = 1000;

    /// <summary>Örneklenmesi planlanan tag adları. Örn: ["tBool", "tReal"]</summary>
    public string[] SampledTags { get; set; } = new string[0];
}
