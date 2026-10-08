namespace RemasterGuru.Api.Checkout;

public sealed record CheckoutProduct(
    string Sku,
    string Name,
    string Description,
    int AmountCents,
    int IncludedRemasterCredits);

public static class CheckoutProductCatalog
{
    public const string RestoreBundleSku = "book-restore-bundle";
    public const string AlbumOnlySku = "book-album-only";

    public const int RestoreBundleCredits = 8;

    private static readonly CheckoutProduct[] Products =
    [
        new(
            RestoreBundleSku,
            "24-page hardcover + 8 remasters",
            "US hardcover photo book with eight AI photo repairs included.",
            8900,
            RestoreBundleCredits),
        new(
            AlbumOnlySku,
            "24-page hardcover (album only)",
            "US hardcover photo book — print your album as-is.",
            5900,
            0)
    ];

    public static IReadOnlyList<CheckoutProduct> List() => Products;

    public static CheckoutProduct? TryGet(string? sku) =>
        Products.FirstOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));
}
