using RemasterGuru.Domain.Entities;

namespace RemasterGuru.Api.Print;

public interface IPrintFulfillmentProvider
{
    Task<PrintFulfillmentSubmitResult> SubmitOrderAsync(
        Order order,
        PrintAlbumContext albumContext,
        CancellationToken cancellationToken = default);
}

public sealed record PrintFulfillmentSubmitResult(string LabOrderId, string? RequestPayloadJson);
