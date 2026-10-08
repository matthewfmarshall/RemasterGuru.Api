namespace RemasterGuru.Api.Print;

public sealed record PrintAlbumContext(
    Guid AlbumId,
    string Title,
    string TemplateId,
    int AssetCount);
