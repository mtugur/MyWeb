namespace MyWeb.Core.Hist
{
    /// <summary>
    /// Arşiv yazma politikası (tag bazında konfigürasyon).
    /// </summary>
    public enum ArchiveMode : byte
    {
        Always = 0, // Her taramada yaz
        ChangeOnly = 1, // Sadece değişince yaz
        Deadband = 2  // Değişim eşiği (abs/%)
    }
}
