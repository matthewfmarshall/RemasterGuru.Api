using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RemasterGuru.Domain.Enums;

namespace RemasterGuru.Worker.Services;

public sealed record RemasterImageResult(byte[] Bytes, string ContentType);

public interface IImageRemasterService
{
    bool IsConfigured { get; }
    Task<RemasterImageResult> RemasterAsync(
        byte[] originalBytes,
        string contentType,
        RemasterPreset preset,
        TargetResolution resolution,
        string? promptOverride,
        CancellationToken cancellationToken);
}

public sealed class XaiImageRemasterService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<XaiImageRemasterService> logger) : IImageRemasterService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static int _missingKeyLogged;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GetApiKey());

    public async Task<RemasterImageResult> RemasterAsync(
        byte[] originalBytes,
        string contentType,
        RemasterPreset preset,
        TargetResolution resolution,
        string? promptOverride,
        CancellationToken cancellationToken)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (Interlocked.Exchange(ref _missingKeyLogged, 1) == 0)
            {
                logger.LogWarning(
                    "XAI_API_KEY is not configured; remaster jobs will use stub output (copy original bytes). " +
                    "Set user secret or environment variable XAI_API_KEY on the Worker.");
            }

            return new RemasterImageResult(originalBytes, contentType);
        }

        var mime = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;
        var dataUri = $"data:{mime};base64,{Convert.ToBase64String(originalBytes)}";
        var prompt = RemasterPrompts.Build(preset, resolution, promptOverride);

        var body = new
        {
            model = "grok-imagine-image-2.0",
            prompt,
            response_format = "b64_json",
            image = new { url = dataUri, type = "image_url" }
        };

        var client = httpClientFactory.CreateClient(nameof(XaiImageRemasterService));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/images/edits");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(body, mediaType: new MediaTypeHeaderValue("application/json"));

        using var response = await client.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"xAI image edit failed ({(int)response.StatusCode}): {TrimForLog(responseText)}");
        }

        var parsed = JsonSerializer.Deserialize<XaiEditResponse>(responseText, JsonOptions);
        var item = parsed?.Data?.FirstOrDefault();
        if (item is null)
        {
            throw new InvalidOperationException("xAI image edit returned no image data.");
        }

        if (!string.IsNullOrWhiteSpace(item.B64Json))
        {
            var bytes = Convert.FromBase64String(item.B64Json);
            var outType = item.MimeType ?? mime;
            return new RemasterImageResult(bytes, outType);
        }

        if (!string.IsNullOrWhiteSpace(item.Url))
        {
            var download = await client.GetAsync(item.Url, cancellationToken);
            if (!download.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Failed to download xAI image ({(int)download.StatusCode}).");
            }

            var bytes = await download.Content.ReadAsByteArrayAsync(cancellationToken);
            var outType = download.Content.Headers.ContentType?.MediaType
                ?? item.MimeType
                ?? mime;
            return new RemasterImageResult(bytes, outType);
        }

        throw new InvalidOperationException("xAI image edit response missing url and b64_json.");
    }

    private string? GetApiKey() =>
        configuration["XAI_API_KEY"] ?? configuration["Xai:ApiKey"];

    private static string TrimForLog(string text) =>
        text.Length <= 500 ? text : text[..500] + "…";

    private sealed class XaiEditResponse
    {
        [JsonPropertyName("data")]
        public List<XaiImageItem>? Data { get; set; }
    }

    private sealed class XaiImageItem
    {
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }
    }
}
