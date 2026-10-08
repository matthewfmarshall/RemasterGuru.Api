using System.Text.Json;
using RemasterGuru.Api.Albums;
using RemasterGuru.Api.Auth;
using RemasterGuru.Api.Contracts;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure.Imaging;
using RemasterGuru.Infrastructure.Repositories;
using RemasterGuru.Infrastructure.Storage;

namespace RemasterGuru.Api.Endpoints;

public static class ApiV1Endpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");

        MapAlbums(api);
        MapAssets(api);
        MapRemasterJobs(api);
        MapCredits(api);
        MapOrders(api);
        MapInternalUpload(app);

        return api;
    }

    private static void MapAlbums(RouteGroupBuilder api)
    {
        api.MapGet("/albums", async (ICurrentUser user, IAlbumRepository albums, CancellationToken ct) =>
        {
            var list = await albums.ListForUserAsync(user.UserId, ct);
            return Results.Json(list.Select(ContractMaps.ToAlbumDto));
        });

        api.MapPost("/albums", async (
            CreateAlbumRequest body,
            ICurrentUser user,
            IAlbumRepository albums,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Title))
            {
                return Results.Problem("Title is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var now = DateTimeOffset.UtcNow;
            var album = new Album
            {
                Id = Guid.NewGuid(),
                UserId = user.UserId,
                Title = body.Title.Trim(),
                TemplateId = body.TemplateId ?? "hardcover-24",
                Status = AlbumStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now
            };
            await albums.AddAsync(album, ct);
            return Results.Json(ContractMaps.ToAlbumDto(album), statusCode: StatusCodes.Status201Created);
        });

        api.MapGet("/albums/{albumId:guid}", async (
            Guid albumId,
            ICurrentUser user,
            IAlbumRepository albums,
            IAssetRepository assets,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(albumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var assetList = await assets.ListForAlbumAsync(albumId, user.UserId, ct);
            return Results.Json(new
            {
                album = ContractMaps.ToAlbumDto(album),
                pageSummary = new { assetCount = assetList.Count }
            });
        });

        api.MapPatch("/albums/{albumId:guid}", async (
            Guid albumId,
            PatchAlbumRequest body,
            ICurrentUser user,
            IAlbumRepository albums,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(albumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            if (body.Title is not null)
            {
                album.Title = body.Title.Trim();
            }

            if (body.TemplateId is not null)
            {
                album.TemplateId = body.TemplateId;
            }

            if (body.Status is not null)
            {
                album.Status = ContractMaps.ParseAlbumStatus(body.Status);
            }

            album.UpdatedAt = DateTimeOffset.UtcNow;
            await albums.SaveChangesAsync(ct);
            return Results.Json(ContractMaps.ToAlbumDto(album));
        });

        api.MapDelete("/albums/{albumId:guid}", async (
            Guid albumId,
            ICurrentUser user,
            IAlbumRepository albums,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(albumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            album.DeletedAt = DateTimeOffset.UtcNow;
            album.UpdatedAt = DateTimeOffset.UtcNow;
            await albums.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        api.MapGet("/albums/{albumId:guid}/layout", async (
            Guid albumId,
            ICurrentUser user,
            IAlbumRepository albums,
            IAssetRepository assets,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(albumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var apiBase = config["Api:PublicBaseUrl"] ?? "http://localhost:5055";
            return Results.Json(await BuildAlbumLayoutPayloadAsync(album, assets, apiBase, user.UserId, ct));
        });

        api.MapPatch("/albums/{albumId:guid}/layout", async (
            Guid albumId,
            PatchAlbumLayoutRequest body,
            ICurrentUser user,
            IAlbumRepository albums,
            IAssetRepository assets,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(albumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            if (body.OrderedAssetIds is null || body.OrderedAssetIds.Count == 0)
            {
                return Results.Problem("orderedAssetIds is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var assetList = await assets.ListForAlbumAsync(albumId, user.UserId, ct);
            if (body.OrderedAssetIds.Count != assetList.Count)
            {
                return Results.Problem(
                    "orderedAssetIds must include every photo in the album exactly once.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var albumAssetIds = assetList.Select(a => a.Id).ToHashSet();
            if (body.OrderedAssetIds.Any(id => !albumAssetIds.Contains(id)))
            {
                return Results.Problem(
                    "orderedAssetIds contains unknown asset ids for this album.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (body.OrderedAssetIds.Distinct().Count() != body.OrderedAssetIds.Count)
            {
                return Results.Problem(
                    "orderedAssetIds must not contain duplicates.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            for (var i = 0; i < body.OrderedAssetIds.Count; i++)
            {
                var asset = assetList.First(a => a.Id == body.OrderedAssetIds[i]);
                asset.OrderIndex = i;
            }

            album.UpdatedAt = DateTimeOffset.UtcNow;
            await albums.SaveChangesAsync(ct);
            await assets.SaveChangesAsync(ct);

            var apiBase = config["Api:PublicBaseUrl"] ?? "http://localhost:5055";
            return Results.Json(await BuildAlbumLayoutPayloadAsync(album, assets, apiBase, user.UserId, ct));
        });

        api.MapGet("/albums/{albumId:guid}/print-readiness", async (
            Guid albumId,
            ICurrentUser user,
            IAlbumRepository albums,
            IAssetRepository assets,
            IUploadSessionRepository sessions,
            IBlobStorage blobs,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(albumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var assetList = await assets.ListForAlbumAsync(albumId, user.UserId, ct);
            var byteSizes = await sessions.GetCompletedByteSizesForAssetsAsync(assetList.Select(a => a.Id), ct);
            var readiness = PrintReadinessEvaluator.Evaluate(assetList, byteSizes, blobs);

            return Results.Json(new
            {
                templateId = album.TemplateId,
                pageCount = AlbumTemplateCatalog.GetPageCount(album.TemplateId),
                slotsFilled = assetList.Count,
                minLongEdgePx = AlbumTemplateCatalog.FullPageMinLongEdgePx,
                softPhotoCount = readiness.SoftPhotoCount,
                warningCount = readiness.WarningCount,
                warnings = readiness.Warnings.Select(w => new
                {
                    assetId = w.AssetId,
                    severity = w.Severity,
                    code = w.Code,
                    message = w.Message
                })
            });
        });
    }

    private static async Task<object> BuildAlbumLayoutPayloadAsync(
        Album album,
        IAssetRepository assets,
        string apiBase,
        Guid userId,
        CancellationToken ct)
    {
        var assetList = await assets.ListForAlbumAsync(album.Id, userId, ct);
        var pageCount = AlbumTemplateCatalog.GetPageCount(album.TemplateId);

        return new
        {
            albumId = album.Id,
            templateId = album.TemplateId,
            pageCount,
            slotsFilled = assetList.Count,
            orderedAssetIds = assetList.Select(a => a.Id).ToList(),
            assets = assetList.Select(a => ContractMaps.ToAssetDto(a, apiBase))
        };
    }

    private static void MapAssets(RouteGroupBuilder api)
    {
        api.MapPost("/assets/upload-sessions", async (
            CreateUploadSessionRequest body,
            ICurrentUser user,
            IAlbumRepository albums,
            IAssetRepository assets,
            IUploadSessionRepository sessions,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(body.AlbumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var assetId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var storageKey = $"blobs/{assetId}/original";
            var now = DateTimeOffset.UtcNow;
            var orderIndex = await assets.CountForAlbumAsync(body.AlbumId, user.UserId, ct);

            var asset = new Asset
            {
                Id = assetId,
                AlbumId = body.AlbumId,
                UserId = user.UserId,
                OrderIndex = orderIndex,
                CreatedAt = now
            };

            var version = new AssetVersion
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                Kind = AssetVersionKind.Original,
                StorageKey = storageKey,
                ContentType = body.ContentType ?? "application/octet-stream",
                CreatedAt = now
            };
            asset.Versions.Add(version);
            asset.ActiveVersionId = version.Id;

            await assets.AddAsync(asset, ct);

            var session = new UploadSession
            {
                Id = sessionId,
                UserId = user.UserId,
                AlbumId = body.AlbumId,
                AssetId = assetId,
                FileName = body.FileName ?? "upload",
                ContentType = body.ContentType ?? "application/octet-stream",
                ByteSize = body.ByteSize,
                StorageKey = storageKey,
                ExpiresAt = now.AddHours(1),
                CreatedAt = now
            };
            await sessions.AddAsync(session, ct);

            var baseUrl = config["Api:PublicBaseUrl"] ?? "http://localhost:5055";
            var uploadUrl = $"{baseUrl.TrimEnd('/')}/api/v1/internal/upload/{sessionId}";

            return Results.Json(new
            {
                sessionId,
                uploadUrl,
                assetId,
                expiresAt = session.ExpiresAt
            }, statusCode: StatusCodes.Status201Created);
        });

        api.MapGet("/albums/{albumId:guid}/assets", async (
            Guid albumId,
            ICurrentUser user,
            IAlbumRepository albums,
            IAssetRepository assets,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(albumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var apiBase = config["Api:PublicBaseUrl"] ?? "http://localhost:5055";
            var list = await assets.ListForAlbumAsync(albumId, user.UserId, ct);
            return Results.Json(list.Select(a => ContractMaps.ToAssetDto(a, apiBase)));
        });

        api.MapPost("/albums/{albumId:guid}/assets", async (
            Guid albumId,
            RegisterAssetRequest body,
            ICurrentUser user,
            IAlbumRepository albums,
            IAssetRepository assets,
            IUploadSessionRepository sessions,
            IBlobStorage blobs,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(albumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var session = await sessions.GetForUserAsync(body.SessionId, user.UserId, ct);
            if (session is null || session.AlbumId != albumId)
            {
                return Results.Problem("Upload session not found.", statusCode: StatusCodes.Status404NotFound);
            }

            if (session.IsCompleted)
            {
                return Results.Problem("Upload session already completed.", statusCode: StatusCodes.Status409Conflict);
            }

            if (!blobs.Exists(session.StorageKey))
            {
                return Results.Problem(
                    "Upload bytes not found. Complete the PUT to the upload URL before registering.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var asset = await assets.GetWithVersionsForUserAsync(session.AssetId, user.UserId, ct);
            if (asset is null)
            {
                return Results.Problem("Asset not found.", statusCode: StatusCodes.Status404NotFound);
            }

            if (body.Caption is not null)
            {
                asset.Caption = body.Caption;
            }

            var original = asset.Versions.FirstOrDefault(v => v.Kind == AssetVersionKind.Original)
                ?? asset.Versions.OrderBy(v => v.CreatedAt).FirstOrDefault();
            if (original is not null && blobs.Exists(original.StorageKey)
                && (original.Width == 0 || original.Height == 0))
            {
                await using var dimensionStream = blobs.OpenRead(original.StorageKey);
                if (ImageDimensionProbe.TryGetDimensions(dimensionStream, out var width, out var height))
                {
                    original.Width = width;
                    original.Height = height;
                }
            }

            session.IsCompleted = true;
            await sessions.SaveChangesAsync(ct);
            await assets.SaveChangesAsync(ct);

            var apiBase = config["Api:PublicBaseUrl"] ?? "http://localhost:5055";
            return Results.Json(ContractMaps.ToAssetDto(asset, apiBase));
        });

        api.MapPatch("/assets/{assetId:guid}", async (
            Guid assetId,
            PatchAssetRequest body,
            ICurrentUser user,
            IAssetRepository assets,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var asset = await assets.GetWithVersionsForUserAsync(assetId, user.UserId, ct);
            if (asset is null)
            {
                return Results.Problem("Asset not found.", statusCode: StatusCodes.Status404NotFound);
            }

            if (body.Caption is not null)
            {
                asset.Caption = string.IsNullOrWhiteSpace(body.Caption) ? null : body.Caption.Trim();
            }

            if (body.DisplayVersion is not null)
            {
                var displayVersion = ContractMaps.ParseDisplayVersion(body.DisplayVersion);
                if (displayVersion == AssetDisplayVersion.Restored
                    && !asset.Versions.Any(v => v.Kind == AssetVersionKind.Restored))
                {
                    return Results.Problem(
                        "No restored version is available for this photo yet.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                asset.DisplayVersion = displayVersion;
            }

            await assets.SaveChangesAsync(ct);
            var apiBase = config["Api:PublicBaseUrl"] ?? "http://localhost:5055";
            return Results.Json(ContractMaps.ToAssetDto(asset, apiBase));
        });

        api.MapGet("/assets/{assetId:guid}/original", async (
            Guid assetId,
            ICurrentUser user,
            IAssetRepository assets,
            IBlobStorage blobs,
            CancellationToken ct) =>
        {
            var asset = await assets.GetWithVersionsForUserAsync(assetId, user.UserId, ct);
            if (asset is null)
            {
                return Results.Problem("Asset not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var original = asset.Versions.FirstOrDefault(v => v.Kind == AssetVersionKind.Original)
                ?? asset.Versions.OrderBy(v => v.CreatedAt).FirstOrDefault();
            if (original is null || !blobs.Exists(original.StorageKey))
            {
                return Results.Problem("Original file not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var stream = blobs.OpenRead(original.StorageKey);
            return Results.Stream(stream, original.ContentType);
        });

        api.MapGet("/assets/{assetId:guid}/restored", async (
            Guid assetId,
            ICurrentUser user,
            IAssetRepository assets,
            IBlobStorage blobs,
            CancellationToken ct) =>
        {
            var asset = await assets.GetWithVersionsForUserAsync(assetId, user.UserId, ct);
            if (asset is null)
            {
                return Results.Problem("Asset not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var restored = asset.Versions
                .Where(v => v.Kind == AssetVersionKind.Restored)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefault();

            if (restored is null && asset.ActiveVersionId is not null)
            {
                restored = asset.Versions.FirstOrDefault(v =>
                    v.Id == asset.ActiveVersionId && v.Kind == AssetVersionKind.Restored);
            }

            if (restored is null || !blobs.Exists(restored.StorageKey))
            {
                return Results.Problem("Restored file not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var stream = blobs.OpenRead(restored.StorageKey);
            return Results.Stream(stream, restored.ContentType);
        });

        api.MapGet("/assets/{assetId:guid}", async (
            Guid assetId,
            ICurrentUser user,
            IAssetRepository assets,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var asset = await assets.GetWithVersionsForUserAsync(assetId, user.UserId, ct);
            if (asset is null)
            {
                return Results.Problem("Asset not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var apiBase = config["Api:PublicBaseUrl"] ?? "http://localhost:5055";
            return Results.Json(ContractMaps.ToAssetDto(asset, apiBase));
        });

        api.MapDelete("/assets/{assetId:guid}", async (
            Guid assetId,
            ICurrentUser user,
            IAssetRepository assets,
            CancellationToken ct) =>
        {
            var asset = await assets.GetForUserAsync(assetId, user.UserId, ct);
            if (asset is null)
            {
                return Results.Problem("Asset not found.", statusCode: StatusCodes.Status404NotFound);
            }

            asset.DeletedAt = DateTimeOffset.UtcNow;
            await assets.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static void MapRemasterJobs(RouteGroupBuilder api)
    {
        api.MapPost("/assets/{assetId:guid}/remaster-jobs", async (
            Guid assetId,
            CreateRemasterJobRequest body,
            ICurrentUser user,
            IAssetRepository assets,
            IUserRepository users,
            ICreditRepository credits,
            IRemasterJobRepository jobs,
            CancellationToken ct) =>
        {
            var asset = await assets.GetWithVersionsForUserAsync(assetId, user.UserId, ct);
            if (asset is null)
            {
                return Results.Problem("Asset not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var preset = ContractMaps.ParsePreset(body.Preset);
            var targetResolution = ContractMaps.ParseTargetResolution(body.TargetResolution);
            var userEntity = await users.GetByIdAsync(user.UserId, ct)
                ?? await users.GetOrCreateAsync(user.UserId, ct);

            var freeTaste = targetResolution == TargetResolution.OneK && !userEntity.FreeTasteUsed;
            var creditCharged = false;

            if (freeTaste)
            {
                userEntity.FreeTasteUsed = true;
                await users.SaveChangesAsync(ct);
            }
            else
            {
                var balance = await credits.GetBalanceAsync(user.UserId, ct);
                if (balance < 1)
                {
                    return Results.Problem(
                        "Insufficient credits.",
                        statusCode: StatusCodes.Status402PaymentRequired);
                }

                creditCharged = true;
            }

            var jobId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var job = new RemasterJob
            {
                Id = jobId,
                AssetId = assetId,
                UserId = user.UserId,
                Status = RemasterJobStatus.Queued,
                Preset = preset,
                TargetResolution = targetResolution,
                PromptOverride = body.PromptOverride,
                CreditCharged = creditCharged,
                CreatedAt = now
            };

            await jobs.AddAsync(job, ct);

            if (creditCharged)
            {
                await credits.AddEntryAsync(new CreditLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = user.UserId,
                    Amount = -1,
                    Reason = CreditLedgerReason.RemasterJob,
                    ReferenceId = jobId,
                    CreatedAt = now
                }, ct);
            }

            return Results.Json(ContractMaps.ToJobDto(job), statusCode: StatusCodes.Status201Created);
        });

        api.MapGet("/remaster-jobs/{jobId:guid}", async (
            Guid jobId,
            ICurrentUser user,
            IRemasterJobRepository jobs,
            CancellationToken ct) =>
        {
            var job = await jobs.GetForUserAsync(jobId, user.UserId, ct);
            if (job is null)
            {
                return Results.Problem("Job not found.", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Json(ContractMaps.ToJobDto(job));
        });

        api.MapGet("/assets/{assetId:guid}/remaster-jobs", async (
            Guid assetId,
            ICurrentUser user,
            IAssetRepository assets,
            IRemasterJobRepository jobs,
            CancellationToken ct) =>
        {
            var asset = await assets.GetForUserAsync(assetId, user.UserId, ct);
            if (asset is null)
            {
                return Results.Problem("Asset not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var list = await jobs.ListForAssetAsync(assetId, user.UserId, ct);
            return Results.Json(list.Select(ContractMaps.ToJobDto));
        });
    }

    private static void MapCredits(RouteGroupBuilder api)
    {
        api.MapGet("/credits/balance", async (
            ICurrentUser user,
            IUserRepository users,
            ICreditRepository credits,
            CancellationToken ct) =>
        {
            var userEntity = await users.GetOrCreateAsync(user.UserId, ct);
            var balance = await credits.GetBalanceAsync(user.UserId, ct);
            return Results.Json(new
            {
                balance,
                freeTasteUsed = userEntity.FreeTasteUsed
            });
        });

        api.MapGet("/credits/ledger", async (
            ICurrentUser user,
            ICreditRepository credits,
            int? skip,
            int? take,
            CancellationToken ct) =>
        {
            var entries = await credits.GetLedgerAsync(user.UserId, skip ?? 0, Math.Clamp(take ?? 50, 1, 200), ct);
            return Results.Json(entries.Select(ContractMaps.ToLedgerDto));
        });

        api.MapPost("/credits/grants", async (
            GrantCreditsRequest body,
            ICurrentUser user,
            ICreditRepository credits,
            IWebHostEnvironment env,
            CancellationToken ct) =>
        {
            if (!env.IsDevelopment())
            {
                return Results.Problem("Grants are only available in Development.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (body.Amount <= 0)
            {
                return Results.Problem("Amount must be positive.", statusCode: StatusCodes.Status400BadRequest);
            }

            var entry = new CreditLedgerEntry
            {
                Id = Guid.NewGuid(),
                UserId = user.UserId,
                Amount = body.Amount,
                Reason = ContractMaps.ParseCreditReason(body.Reason),
                CreatedAt = DateTimeOffset.UtcNow
            };
            await credits.AddEntryAsync(entry, ct);
            return Results.Json(ContractMaps.ToLedgerDto(entry), statusCode: StatusCodes.Status201Created);
        });
    }

    private static void MapOrders(RouteGroupBuilder api)
    {
        api.MapPost("/albums/{albumId:guid}/orders", async (
            Guid albumId,
            CreateOrderRequest body,
            ICurrentUser user,
            IAlbumRepository albums,
            IOrderRepository orders,
            CancellationToken ct) =>
        {
            var album = await albums.GetForUserAsync(albumId, user.UserId, ct);
            if (album is null)
            {
                return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
            }

            var sku = body.Sku ?? "BOOK_RESTORE_24";
            var amountCents = sku switch
            {
                "BOOK_RESTORE_24" => 8900,
                _ => 8900
            };

            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = user.UserId,
                AlbumId = albumId,
                Sku = sku,
                Status = OrderStatus.PendingPayment,
                AmountCents = amountCents,
                ShippingAddressJson = JsonSerializer.Serialize(
                    body.ShippingAddress ?? new ShippingAddressDto(null, null, null, null, null, null, "US"),
                    JsonOptions),
                CreatedAt = DateTimeOffset.UtcNow
            };

            await orders.AddAsync(order, ct);
            return Results.Json(ContractMaps.ToOrderDto(order), statusCode: StatusCodes.Status201Created);
        });

        api.MapGet("/orders/{orderId:guid}", async (
            Guid orderId,
            ICurrentUser user,
            IOrderRepository orders,
            CancellationToken ct) =>
        {
            var order = await orders.GetForUserAsync(orderId, user.UserId, ct);
            if (order is null)
            {
                return Results.Problem("Order not found.", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Json(ContractMaps.ToOrderDto(order));
        });

        api.MapGet("/orders", async (
            ICurrentUser user,
            IOrderRepository orders,
            CancellationToken ct) =>
        {
            var list = await orders.ListForUserAsync(user.UserId, ct);
            return Results.Json(list.Select(ContractMaps.ToOrderDto));
        });
    }

    private static void MapInternalUpload(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/internal/upload/{sessionId:guid}", async (
            Guid sessionId,
            HttpRequest request,
            IUploadSessionRepository sessions,
            IBlobStorage blobs,
            CancellationToken ct) =>
        {
            var session = await sessions.GetByIdAsync(sessionId, ct);
            if (session is null)
            {
                return Results.Problem("Upload session not found.", statusCode: StatusCodes.Status404NotFound);
            }

            if (session.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return Results.Problem("Upload session expired.", statusCode: StatusCodes.Status410Gone);
            }

            await blobs.SaveAsync(session.StorageKey, request.Body, ct);
            return Results.NoContent();
        });
    }

    public sealed record CreateAlbumRequest(string Title, string? TemplateId);
    public sealed record PatchAlbumRequest(string? Title, string? TemplateId, string? Status);
    public sealed record PatchAlbumLayoutRequest(IReadOnlyList<Guid>? OrderedAssetIds);
    public sealed record PatchAssetRequest(string? Caption, string? DisplayVersion);
    public sealed record CreateUploadSessionRequest(Guid AlbumId, string? FileName, string? ContentType, long ByteSize);
    public sealed record RegisterAssetRequest(Guid SessionId, string? Caption);
    public sealed record CreateRemasterJobRequest(string? Preset, string? TargetResolution, string? PromptOverride);
    public sealed record GrantCreditsRequest(int Amount, string? Reason);
    public sealed record CreateOrderRequest(string? Sku, ShippingAddressDto? ShippingAddress);
    public sealed record ShippingAddressDto(
        string? Name,
        string? Line1,
        string? Line2,
        string? City,
        string? State,
        string? PostalCode,
        string? Country);
}
