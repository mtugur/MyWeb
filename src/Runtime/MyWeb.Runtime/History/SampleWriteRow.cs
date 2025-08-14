using System.Data;
using MyWeb.Core.Hist;

namespace MyWeb.Runtime.History
{
    /// <summary>Bulk insert için satır modeli.</summary>
    public sealed class SampleWriteRow
    {
        public int ProjectId { get; init; }
        public int TagId     { get; init; }
        public System.DateTime Utc  { get; init; }
        public DataType DataType { get; init; }
        public double? ValueNumeric { get; init; }
        public string? ValueText   { get; init; }
        public bool?   ValueBool   { get; init; }
        public short   Quality     { get; init; } = 0;
        public byte?   Source      { get; init; } = 1;

        public static DataTable CreateTable()
        {
            var t = new DataTable();
            t.Columns.Add("ProjectId",    typeof(int));
            t.Columns.Add("TagId",        typeof(int));
            t.Columns.Add("Utc",          typeof(System.DateTime));
            t.Columns.Add("DataType",     typeof(byte));
            t.Columns.Add("ValueNumeric", typeof(double));
            t.Columns.Add("ValueText",    typeof(string));
            t.Columns.Add("ValueBool",    typeof(bool));
            t.Columns.Add("Quality",      typeof(short));
            t.Columns.Add("Source",       typeof(byte));
            return t;
        }

        public void AddTo(DataTable t)
            => t.Rows.Add(ProjectId, TagId, Utc, (byte)DataType, ValueNumeric, ValueText, ValueBool, Quality, Source);
    }
}
