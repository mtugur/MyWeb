using System.Text.Json.Serialization;
using MyWeb.Core.Hist;

namespace MyWeb.Runtime.Packaging
{
    public sealed class Manifest
    {
        [JsonPropertyName("projectKey")]  public string ProjectKey { get; set; } = null!;
        [JsonPropertyName("projectName")] public string ProjectName { get; set; } = null!;
        [JsonPropertyName("projVersion")] public string ProjVersion { get; set; } = "1.0.0";
        [JsonPropertyName("min_engine")]  public string MinEngine { get; set; } = ">=1.0";
    }

    public sealed class ControllerDto
    {
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Address { get; set; } = null!;
        public Dictionary<string, object>? Settings { get; set; }
    }

    public sealed class TagArchiveDto
    {
        public string? Mode { get; set; }
        public double? DeadbandAbs { get; set; }
        public double? DeadbandPercent { get; set; }
        public int? RetentionDays { get; set; }
        public string[]? Rollups { get; set; }
    }

    public sealed class TagDto
    {
        public string Path { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string DataType { get; set; } = "Float";
        public string? Unit { get; set; }
        public double? Scale { get; set; }
        public double? Offset { get; set; }
        public string? Address { get; set; }
        public string? DriverRef { get; set; }
        public bool? LongString { get; set; }
        public TagArchiveDto? Archive { get; set; }

        public DataType ToDataType() =>
            Enum.TryParse<DataType>(DataType, true, out var dt) ? dt : Core.Hist.DataType.Float;
    }

    public sealed class ParsedPackage
    {
        public Manifest Manifest { get; init; } = null!;
        public IReadOnlyList<ControllerDto> Controllers { get; init; } = Array.Empty<ControllerDto>();
        public IReadOnlyList<TagDto> Tags { get; init; } = Array.Empty<TagDto>();
        public string PackageHash { get; init; } = "";
    }
}
