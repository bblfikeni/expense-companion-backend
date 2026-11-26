using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Core.Interfaces;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Companion.Api.Features.Images;

public static class ImageEndpoints
{
    private const int MaximumBytes = 5 * 1024 * 1024;
    private const int MultipartOverheadBytes = 64 * 1024;

    public static void MapImageEndpoints(this WebApplication app, IConfiguration configuration)
    {
        var configured = configuration.GetValue<int?>("Images:MaxBytes") ?? MaximumBytes;
        if (configured is <= 0 or > MaximumBytes)
            throw new InvalidOperationException("Images:MaxBytes must be between 1 and 5242880.");
        var cacheMaxAge = configuration.GetValue<int?>("Images:CacheMaxAgeSeconds") ?? 300;
        if (cacheMaxAge < 0)
            throw new InvalidOperationException("Images:CacheMaxAgeSeconds must be non-negative.");

        var group = app.MapGroup("/api/expenses/{expenseId:guid}/images").RequireAuthorization().WithTags("Images");
        group.MapPost("/", async (Guid expenseId, HttpContext context, IImageStore store, CancellationToken ct) =>
            await Upload(expenseId, context, store, configured, ct))
            .WithName("UploadExpenseImage")
            .RequireAuthorization("images:write")
            .Accepts<ImageUpload>("multipart/form-data", "*/*")
            .Produces<ImageResponse>(201).Produces<ImageResponse>(200)
            .Produces(401).Produces(403).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415);
        group.MapGet("/", async (Guid expenseId, ClaimsPrincipal user, IImageStore store, CancellationToken ct) =>
        {
            var result = await store.ListAsync(Profile(user), expenseId, ct);
            return result.Status == ImageStoreStatus.Success ? Results.Ok(result.Value!.Select(ImageResponse.From)) : Error(result.Status);
        }).WithName("ListExpenseImages").RequireAuthorization("images:read").Produces<IReadOnlyList<ImageResponse>>().Produces(401).Produces(403).ProducesProblem(404);
        group.MapGet("/{imageId:guid}/content", async (Guid expenseId, Guid imageId, HttpContext context, IImageStore store, CancellationToken ct) =>
        {
            var result = await store.GetAsync(Profile(context.User), expenseId, imageId, ct);
            if (result.Status != ImageStoreStatus.Success) return Error(result.Status);
            var image = result.Value!;
            var etag = new EntityTagHeaderValue('"' + image.Metadata.Sha256 + '"');
            context.Response.GetTypedHeaders().ETag = etag;
            context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue { Private = true, MaxAge = TimeSpan.FromSeconds(cacheMaxAge) };
            context.Response.Headers.ContentDisposition = "inline";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            var conditions = context.Request.GetTypedHeaders().IfNoneMatch;
            if (conditions != null && conditions.Any(tag => tag.Tag == "*" || tag.Compare(etag, useStrongComparison: false)))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            return Results.File(image.Content, image.Metadata.ContentType);
        }).WithName("GetExpenseImage").RequireAuthorization("images:read").Produces(200, contentType: "image/jpeg").Produces(200, contentType: "image/png").Produces(200, contentType: "image/webp").Produces(304).Produces(401).Produces(403).ProducesProblem(404);
        group.MapPatch("/{imageId:guid}", async (Guid expenseId, Guid imageId, HttpContext context, IImageStore store, CancellationToken ct) =>
        {
            var profile = Profile(context.User);
            var access = await store.ListAsync(profile, expenseId, ct);
            if (access.Status != ImageStoreStatus.Success) return Error(access.Status);
            if (!context.Request.HasJsonContentType()) return Results.Problem(statusCode: 415, title: "Expected application/json");
            JsonElement body;
            try { body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body, cancellationToken: ct); }
            catch (JsonException) { return Results.Problem(statusCode: 400, title: "Invalid metadata"); }
            if (body.ValueKind != JsonValueKind.Object) return Results.Problem(statusCode: 400, title: "Invalid metadata");
            string? label = null;
            int? page = null;
            var hasLabel = false;
            var hasPage = false;
            foreach (var property in body.EnumerateObject())
            {
                if (property.NameEquals("label") && !hasLabel && property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null)
                {
                    hasLabel = true;
                    label = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
                }
                else if (property.NameEquals("pageNo") && !hasPage && property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var value))
                {
                    hasPage = true;
                    page = value;
                }
                else return Results.Problem(statusCode: 400, title: "Invalid metadata");
            }
            if ((!hasLabel && !hasPage) || !ValidLabel(label) || page is < 1)
                return Results.Problem(statusCode: 400, title: "Invalid metadata");
            var result = await store.UpdateAsync(profile, expenseId, imageId, hasLabel, label, page, ct);
            return result.Status == ImageStoreStatus.Success ? Results.Ok(ImageResponse.From(result.Value!)) : Error(result.Status);
        }).WithName("UpdateExpenseImage").RequireAuthorization("images:write").Accepts<ImagePatch>("application/json", "*/*").Produces<ImageResponse>().Produces(401).Produces(403).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(415);
        group.MapDelete("/{imageId:guid}", async (Guid expenseId, Guid imageId, ClaimsPrincipal user, IImageStore store, CancellationToken ct) =>
        {
            var status = await store.DeleteAsync(Profile(user), expenseId, imageId, ct);
            return status == ImageStoreStatus.Success ? Results.NoContent() : Error(status);
        }).WithName("DeleteExpenseImage").RequireAuthorization("images:write").Produces(204).Produces(401).Produces(403).ProducesProblem(404).ProducesProblem(409);
    }

    private static async Task<IResult> Upload(Guid expenseId, HttpContext context, IImageStore store, int limit, CancellationToken ct)
    {
        var access = await store.ListAsync(Profile(context.User), expenseId, ct);
        if (access.Status != ImageStoreStatus.Success) return Error(access.Status);
        var requestLimit = limit + MultipartOverheadBytes;
        var maxBodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxBodySize is { IsReadOnly: false }) maxBodySize.MaxRequestBodySize = requestLimit;
        else if (maxBodySize is { IsReadOnly: true }) throw new InvalidOperationException("The image request body has already been read.");
        if (context.Request.ContentLength > requestLimit) return TooLarge();
        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var media) ||
            !media.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
            !HeaderUtilities.RemoveQuotes(media.Boundary).HasValue || HeaderUtilities.RemoveQuotes(media.Boundary).Length > 70)
            return Results.Problem(statusCode: 415, title: "Expected multipart/form-data");
        try
        {
            var reader = new MultipartReader(HeaderUtilities.RemoveQuotes(media.Boundary).Value!, new LimitedStream(context.Request.Body, requestLimit));
            byte[]? content = null;
            string? contentType = null, label = null;
            int? page = null;
            var seenLabel = false;
            var seenPage = false;
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(ct)) != null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) || !disposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
                    return Results.Problem(statusCode: 400, title: "Invalid multipart section");
                var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
                var isFile = disposition.FileName.HasValue || disposition.FileNameStar.HasValue;
                if (isFile && name == "file" && content == null)
                {
                    contentType = section.ContentType?.ToLowerInvariant();
                    using var buffer = new MemoryStream();
                    await new LimitedStream(section.Body, limit).CopyToAsync(buffer, ct);
                    content = buffer.ToArray();
                }
                else if (!isFile && (name == "label" && !seenLabel || name == "pageNo" && !seenPage))
                {
                    using var text = new StreamReader(section.Body);
                    var value = await text.ReadToEndAsync(ct);
                    if (name == "label") { seenLabel = true; label = value; }
                    else { seenPage = true; if (!int.TryParse(value, out var number) || number < 1) return Results.Problem(statusCode: 400, title: "Invalid page number"); page = number; }
                }
                else return Results.Problem(statusCode: 400, title: "Expected one file and valid metadata");
            }
            if (content == null || content.Length == 0 || !ValidLabel(label)) return Results.Problem(statusCode: 400, title: "Invalid image or metadata");
            if (contentType is not ("image/jpeg" or "image/png" or "image/webp")) return Results.Problem(statusCode: 415, title: "Unsupported image type");
            if (!ImageHeader.TryIdentify(content, contentType, out var dimensions)) return Results.Problem(statusCode: 415, title: "Invalid image header");
            var result = await store.SaveAsync(Profile(context.User), expenseId, contentType, content, dimensions.Width, dimensions.Height, label, page, ct);
            if (result.Status == ImageStoreStatus.AlreadyExists) return Results.Ok(ImageResponse.From(result.Value!));
            if (result.Status != ImageStoreStatus.Success) return Error(result.Status);
            return Results.Created($"/api/expenses/{expenseId}/images/{result.Value!.ImageId}/content", ImageResponse.From(result.Value));
        }
        catch (ImageLimitException) { return TooLarge(); }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge) { return TooLarge(); }
        catch (InvalidDataException) { return Results.Problem(statusCode: 400, title: "Invalid multipart request"); }
    }

    private static Guid Profile(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static bool ValidLabel(string? label) => label is null or "Receipt" or "ProofOfPayment";
    private static IResult TooLarge() => Results.Problem(statusCode: 413, title: "Image request too large");
    private static IResult Error(ImageStoreStatus status) => status switch
    {
        ImageStoreStatus.NotFound => Results.Problem(statusCode: 404, title: "Image or expense not found"),
        ImageStoreStatus.InvalidPage => Results.Problem(statusCode: 400, title: "Invalid page number"),
        ImageStoreStatus.InvalidStatus or ImageStoreStatus.LimitReached => Results.Problem(statusCode: 409, title: "Expense cannot be modified"),
        _ => Results.Problem(statusCode: 500, title: "Image store error")
    };
}

public sealed record ImageResponse([property: JsonPropertyName("public_id")] Guid PublicId, int PageNo, string ContentType, int SizeBytes, string Sha256, string? Label, int Width, int Height, DateTimeOffset CreatedAt, Guid UploadedByProfileId)
{
    public static ImageResponse From(ImageMetadata image) => new(image.ImageId, image.PageNo, image.ContentType, image.SizeBytes, image.Sha256, image.Label, image.Width, image.Height, image.CreatedAt, image.UploadedByProfileId);
}

public sealed class ImageUpload
{
    public IFormFile File { get; set; } = null!;
    public string? Label { get; set; }
    public int? PageNo { get; set; }
}

public sealed class ImagePatch
{
    public string? Label { get; set; }
    public int? PageNo { get; set; }
}