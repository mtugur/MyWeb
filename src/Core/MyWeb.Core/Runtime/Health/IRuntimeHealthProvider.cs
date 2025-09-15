namespace MyWeb.Core.Runtime.Health;

/// <summary>
/// Uygulama çalışma zamanı sağlık bilgisi sağlayıcısı.
/// Controller veya UI buradan okur.
/// </summary>
public interface IRuntimeHealthProvider
{
    RuntimeHealthSnapshot GetSnapshot();
    void ReportGoodSample();              // son iyi örnek (ör. okuma) oldu
    void ReportError();                   // ardışık hata say
    void ResetErrors();                   // toparlanınca sıfırla
    void SetStatus(HealthStatus status, string? message = null);
}
