using System.Text.Json;
using RemasterGuru.Domain.Entities;

namespace RemasterGuru.Api.Print;

public sealed class RpiPrintFulfillmentProvider(
    IConfiguration configuration,
    ILogger<RpiPrintFulfillmentProvider> logger) : IPrintFulfillmentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<PrintFulfillmentSubmitResult> SubmitOrderAsync(
        Order order,
        PrintAlbumContext albumContext,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Rpi:ApiKey"];
        var baseUrl = configuration["Rpi:BaseUrl"];
        var isDevStub = string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl);

        var payload = new
        {
            provider = "rpi",
            mode = isDevStub ? "stub" : "stub_pending_real_api",
            orderId = order.Id,
            albumId = albumContext.AlbumId,
            albumTitle = albumContext.Title,
            templateId = albumContext.TemplateId,
            assetCount = albumContext.AssetCount,
            sku = order.Sku,
            shippingAddress = order.ShippingAddressJson
        };

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var labOrderId = $"rpi-stub-{Guid.NewGuid():N}";

        if (isDevStub)
        {
            logger.LogInformation(
                "RPI dev stub submit for order {OrderId}: labOrderId={LabOrderId} payload={Payload}",
                order.Id,
                labOrderId,
                payloadJson);
        }
        else
        {
            logger.LogInformation(
                "RPI credentials configured but B9 uses stub only; order {OrderId} labOrderId={LabOrderId} baseUrl={BaseUrl}",
                order.Id,
                labOrderId,
                baseUrl);
        }

        return Task.FromResult(new PrintFulfillmentSubmitResult(labOrderId, payloadJson));
    }
}
