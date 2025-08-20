namespace MyWeb.Runtime
{
    /// <summary>
    /// appsettings.* altındaki "ConnectionStrings" bölümüne bağlanan opsiyon sınıfı.
    /// CatalogDb: Uygulama katalog/veri tabanı
    /// HistorianDb: Tarih (history) yazımı için kullanılan DB
    /// </summary>
    public sealed class DbConnOptions
    {
        public string? CatalogDb { get; set; }
        public string? HistorianDb { get; set; }
    }
}
