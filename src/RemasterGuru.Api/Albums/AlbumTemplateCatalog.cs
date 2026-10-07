namespace RemasterGuru.Api.Albums;

public static class AlbumTemplateCatalog
{
    public const int FullPageMinLongEdgePx = 1200;
    public const long SmallFileByteThreshold = 200_000;

    public static int GetPageCount(string templateId) => templateId switch
    {
        "hardcover-24" => 24,
        _ => 24
    };
}
