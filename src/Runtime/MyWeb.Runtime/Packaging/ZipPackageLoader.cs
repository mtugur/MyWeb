using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MyWeb.Runtime.Packaging
{
    public sealed class ZipPackageLoader : IPackageLoader
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public async Task<ParsedPackage> LoadAsync(string packagePath, CancellationToken ct = default)
        {
            if (!File.Exists(packagePath))
                throw new FileNotFoundException("Paket bulunamadı", packagePath);

            byte[] pkgBytes = await File.ReadAllBytesAsync(packagePath, ct);
            string sha256 = Convert.ToHexString(SHA256.HashData(pkgBytes));

            Manifest? manifest = null;
            List<ControllerDto> controllers = new();
            List<TagDto> tags = new();

            using var ms = new MemoryStream(pkgBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);

            string? ReadEntryText(string entryPath)
            {
                var entry = zip.GetEntry(entryPath.Replace("\\", "/"));
                if (entry == null) return null;
                using var s = entry.Open(); using var sr = new StreamReader(s, Encoding.UTF8);
                return sr.ReadToEnd();
            }

            var manifestJson = ReadEntryText("manifest.json") ?? throw new InvalidDataException("manifest.json yok");
            manifest = JsonSerializer.Deserialize<Manifest>(manifestJson, JsonOpts) ?? throw new InvalidDataException("manifest parse edilemedi");

            var ctrlJson = ReadEntryText("config/controllers.json");
            if (!string.IsNullOrWhiteSpace(ctrlJson))
                controllers = JsonSerializer.Deserialize<List<ControllerDto>>(ctrlJson!, JsonOpts) ?? new();

            var tagsJson = ReadEntryText("config/tags.json");
            if (!string.IsNullOrWhiteSpace(tagsJson))
                tags = JsonSerializer.Deserialize<List<TagDto>>(tagsJson!, JsonOpts) ?? new();

            return new ParsedPackage
            {
                Manifest = manifest,
                Controllers = controllers,
                Tags = tags,
                PackageHash = sha256
            };
        }
    }
}
