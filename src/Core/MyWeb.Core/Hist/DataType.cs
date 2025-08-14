namespace MyWeb.Core.Hist
{
    /// <summary>
    /// Zaman-serisi veri tipleri.
    /// SQL tarafında byte (TINYINT) olarak saklayacağız.
    /// </summary>
    public enum DataType : byte
    {
        Bool = 0,
        Int = 1,
        Float = 2,
        String = 3,
        Date = 4
    }
}
