using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;

namespace RemasterGuru.Api.Contracts;

public static class ContractMaps
{
    public static string ToApi(AlbumStatus status) => status switch
    {
        AlbumStatus.Draft => "draft",
        AlbumStatus.ReadyForPrint => "ready_for_print",
        AlbumStatus.Ordered => "ordered",
        _ => "draft"
    };

    public static AlbumStatus ParseAlbumStatus(string? value) => value switch
    {
        "ready_for_print" => AlbumStatus.ReadyForPrint,
        "ordered" => AlbumStatus.Ordered,
        _ => AlbumStatus.Draft
    };

    public static string ToApi(AssetVersionKind kind) => kind switch
    {
        AssetVersionKind.Original => "original",
        AssetVersionKind.Restored => "restored",
        AssetVersionKind.Edit => "edit",
        _ => "original"
    };

    public static string ToApi(RemasterJobStatus status) => status switch
    {
        RemasterJobStatus.Queued => "queued",
        RemasterJobStatus.Running => "running",
        RemasterJobStatus.Succeeded => "succeeded",
        RemasterJobStatus.Failed => "failed",
        _ => "queued"
    };

    public static RemasterPreset ParsePreset(string? value) => value switch
    {
        "fade" => RemasterPreset.Fade,
        "conservative" => RemasterPreset.Conservative,
        _ => RemasterPreset.Damage
    };

    public static string ToApi(RemasterPreset preset) => preset switch
    {
        RemasterPreset.Fade => "fade",
        RemasterPreset.Conservative => "conservative",
        _ => "damage"
    };

    public static TargetResolution ParseTargetResolution(string? value) => value switch
    {
        "2k" => TargetResolution.TwoK,
        _ => TargetResolution.OneK
    };

    public static string ToApi(TargetResolution resolution) => resolution switch
    {
        TargetResolution.TwoK => "2k",
        _ => "1k"
    };

    public static string ToApi(CreditLedgerReason reason) => reason switch
    {
        CreditLedgerReason.RemasterJob => "remaster_job",
        CreditLedgerReason.Purchase => "purchase",
        CreditLedgerReason.BookBundle => "book_bundle",
        _ => "grant"
    };

    public static CreditLedgerReason ParseCreditReason(string? value) => value switch
    {
        "remaster_job" => CreditLedgerReason.RemasterJob,
        "purchase" => CreditLedgerReason.Purchase,
        "book_bundle" => CreditLedgerReason.BookBundle,
        _ => CreditLedgerReason.Grant
    };

    public static string ToApi(OrderStatus status) => status switch
    {
        OrderStatus.Draft => "draft",
        OrderStatus.PendingPayment => "pending_payment",
        OrderStatus.Paid => "paid",
        OrderStatus.SubmittedToLab => "submitted_to_lab",
        OrderStatus.Shipped => "shipped",
        OrderStatus.Cancelled => "cancelled",
        _ => "draft"
    };

    public static object ToAlbumDto(Album album) => new
    {
        id = album.Id,
        title = album.Title,
        templateId = album.TemplateId,
        status = ToApi(album.Status),
        createdAt = album.CreatedAt,
        updatedAt = album.UpdatedAt
    };

    public static object ToAssetDto(Asset asset, string? apiPublicBaseUrl = null)
    {
        var original = asset.Versions.FirstOrDefault(v => v.Kind == AssetVersionKind.Original)
            ?? asset.Versions.OrderBy(v => v.CreatedAt).FirstOrDefault();

        string? thumbnailUrl = null;
        if (apiPublicBaseUrl is not null)
        {
            thumbnailUrl = $"{apiPublicBaseUrl.TrimEnd('/')}/api/v1/assets/{asset.Id}/original";
        }

        return new
        {
            id = asset.Id,
            albumId = asset.AlbumId,
            orderIndex = asset.OrderIndex,
            caption = asset.Caption,
            thumbnailUrl,
            original = original is null
                ? null
                : new
                {
                    storageKey = original.StorageKey,
                    width = original.Width,
                    height = original.Height,
                    contentType = original.ContentType
                },
            activeVersionId = asset.ActiveVersionId,
            versions = asset.Versions
                .OrderBy(v => v.CreatedAt)
                .Select(v => new
                {
                    id = v.Id,
                    kind = ToApi(v.Kind),
                    storageKey = v.StorageKey,
                    createdAt = v.CreatedAt
                })
        };
    }

    public static object ToJobDto(RemasterJob job) => new
    {
        id = job.Id,
        assetId = job.AssetId,
        status = ToApi(job.Status),
        preset = ToApi(job.Preset),
        creditCharged = job.CreditCharged,
        resultVersionId = job.ResultVersionId,
        error = job.Error,
        createdAt = job.CreatedAt,
        completedAt = job.CompletedAt
    };

    public static object ToLedgerDto(CreditLedgerEntry entry) => new
    {
        id = entry.Id,
        amount = entry.Amount,
        reason = ToApi(entry.Reason),
        referenceId = entry.ReferenceId,
        createdAt = entry.CreatedAt
    };

    public static object ToOrderDto(Order order) => new
    {
        id = order.Id,
        albumId = order.AlbumId,
        sku = order.Sku,
        status = ToApi(order.Status),
        stripeCheckoutSessionId = order.StripeCheckoutSessionId,
        labOrderId = order.LabOrderId,
        trackingUrl = order.TrackingUrl,
        amountCents = order.AmountCents,
        createdAt = order.CreatedAt
    };
}
