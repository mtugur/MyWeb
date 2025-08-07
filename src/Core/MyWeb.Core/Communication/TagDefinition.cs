namespace MyWeb.Core.Communication
{
    /// <summary>
    /// PLC üzerinde okunacak/yazılacak bir tag’in temel tanımı.
    /// </summary>
    public class TagDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        /// <summary> Örn. "DataBlock", "Memory", "Input", "Output" </summary>
        public string DataType { get; set; } = string.Empty;
        /// <summary> Örn. "Bit", "Word", "Real" vb. </summary>
        public string VarType { get; set; } = string.Empty;
        public int Count { get; set; } = 1;
        public string ConnectionName { get; set; } = string.Empty;
        /// <summary> Eğer struct ise .NET tipi adı (tam isim) </summary>
        public string StructType { get; set; } = string.Empty;
    }
}
