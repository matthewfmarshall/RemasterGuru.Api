namespace RemasterGuru.Infrastructure.Storage;

public interface IBlobStorage
{
    string RootPath { get; }
    Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default);
    string GetAbsolutePath(string storageKey);
    bool Exists(string storageKey);
    bool TryDelete(string storageKey);
    Stream OpenRead(string storageKey);
}

public sealed class LocalBlobStorage : IBlobStorage
{
    public LocalBlobStorage(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string GetAbsolutePath(string storageKey) =>
        Path.Combine(RootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var path = GetAbsolutePath(storageKey);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
    }

    public bool Exists(string storageKey) => File.Exists(GetAbsolutePath(storageKey));

    public bool TryDelete(string storageKey)
    {
        var path = GetAbsolutePath(storageKey);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public Stream OpenRead(string storageKey) => File.OpenRead(GetAbsolutePath(storageKey));
}
