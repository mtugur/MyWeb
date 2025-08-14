namespace MyWeb.Runtime.Packaging
{
    public interface IPackageLoader
    {
        Task<ParsedPackage> LoadAsync(string packagePath, CancellationToken ct = default);
    }
}
