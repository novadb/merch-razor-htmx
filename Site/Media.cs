using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Merch.NovaDB;
using Microsoft.Net.Http.Headers;
using SkiaSharp;

namespace Merch.Site;

/// <summary>
/// Media proxy with a disk cache. The Delivery login never reaches the browser, so pictures are served from here; but a
/// picture is fetched from NovaDB only once. The upstream route is content-addressed and cannot change under its name
/// (<c>files/{md5}{extension}</c>: ETag = MD5), so a cached file is never revalidated. A file's cache key is its URL below <c>/media</c>:
/// <c>{md5}/original{ext}</c> and, scaled, <c>{md5}/{width}{ext}.webp</c>.
/// </summary>
public sealed partial class Media(Delivery delivery, IConfiguration config)
{
    /// <summary>Widths of the scaled variants (<c>/media/{md5}/{width}{ext}.webp</c>): a fixed list, so the cache cannot be filled with arbitrary sizes.</summary>
    public static readonly int[] Widths = [400, 800, 1200, 1600];

    /// <summary>An image file's name is its content address: the MD5 (GUID form, or 32 hex digits) and the extension.</summary>
    [GeneratedRegex("^([0-9a-f]{32}|[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12})\\.[a-z0-9]{1,5}$")]
    public static partial Regex FileName();

    /// <summary>The <c>file</c> of <c>/media/{md5}/original{ext}</c>.</summary>
    [GeneratedRegex(@"^original(?<ext>\.[a-z0-9]{1,5})$")]
    public static partial Regex OriginalFile();

    /// <summary>The <c>file</c> of <c>/media/{md5}/{width}{ext}.webp</c>; whether the width is a known one is for the caller.</summary>
    [GeneratedRegex(@"^(?<width>\d+)(?<ext>\.[a-z0-9]{1,5})\.webp$")]
    public static partial Regex VariantFile();

    sealed record Meta(string ContentType, string ETag);

    /// <summary><c>MEDIA_CACHE_DIR</c>, by default a folder of this site in the system's temp directory. Delete it to reset the cache.</summary>
    string CacheDir => config["MEDIA_CACHE_DIR"] is { Length: > 0 } dir ? dir : Path.Combine(Path.GetTempPath(), "merch-razor-htmx-media");

    /// <summary>Files being built, by cache key: concurrent requests for the same picture share one download.</summary>
    readonly ConcurrentDictionary<string, Lazy<Task<Meta?>>> building = new();

    /// <summary>Serves <c>/media/{md5}/original{ext}</c>, fetching <c>files/{md5}{ext}</c> first if it is not cached yet; 404 if NovaDB has no such file. The caller has validated both parts as a file name (<see cref="FileName"/>), which makes the cache key safe.</summary>
    public Task<IResult> ServeOriginalAsync(HttpContext context, string md5, string ext) =>
        RespondAsync(context, OriginalKey(md5, ext), () => DownloadAsync(md5, ext));

    /// <summary>Serves <c>/media/{md5}/{width}{ext}.webp</c>, the file scaled to one of <see cref="Widths"/>; the caller has validated all three.</summary>
    public Task<IResult> ServeScaledAsync(HttpContext context, string md5, string ext, int width) =>
        RespondAsync(context, VariantKey(md5, ext, width), () => ScaleAsync(md5, ext, width));

    static string OriginalKey(string md5, string ext) => $"{md5}/original{ext}";

    static string VariantKey(string md5, string ext, int width) => $"{md5}/{width}{ext}.webp";

    async Task<IResult> RespondAsync(HttpContext context, string key, Func<Task<Meta?>> build)
    {
        if (await EnsureAsync(key, build) is not { } meta) return Results.NotFound();
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        // Answers If-None-Match with a 304 by itself.
        return TypedResults.PhysicalFile(Path.Combine(CacheDir, key), meta.ContentType, entityTag: EntityTagHeaderValue.Parse(meta.ETag));
    }

    /// <summary>Cached meta for <paramref name="key"/>, building the file first (once, however many requests ask) if it is not there yet.</summary>
    async Task<Meta?> EnsureAsync(string key, Func<Task<Meta?>> build)
    {
        if (await ReadMetaAsync(key) is { } cached) return cached;
        var pending = building.GetOrAdd(key, _ => new Lazy<Task<Meta?>>(build));
        try { return await pending.Value; }
        finally { building.TryRemove(KeyValuePair.Create(key, pending)); }
    }

    /// <summary>Downloads <c>files/{md5}{ext}</c> into the cache. The meta file is written last: where it exists, the bytes do.</summary>
    async Task<Meta?> DownloadAsync(string md5, string ext)
    {
        using var upstream = await delivery.GetFileAsync($"{md5}{ext}");
        if (upstream.StatusCode == HttpStatusCode.NotFound) return null;
        var etag = upstream.Headers.ETag?.ToString();
        var contentType = upstream.Content.Headers.ContentType?.ToString();
        if (!upstream.IsSuccessStatusCode || etag is null || contentType is null)
            throw new HttpRequestException($"Delivery {(int)upstream.StatusCode} for files/{md5}{ext} (ETag {etag}, Content-Type {contentType})");

        var key = OriginalKey(md5, ext);
        await WriteAtomicAsync(key, async temp =>
        {
            await using var file = File.Create(temp);
            await upstream.Content.CopyToAsync(file);
        });
        return await WriteMetaAsync(key, new Meta(contentType, etag));
    }

    /// <summary>Scales the cached original (fetched first if missing) to <paramref name="width"/> as WebP (quality 80), without enlarging. Meta is written last, as in <see cref="DownloadAsync"/>.</summary>
    async Task<Meta?> ScaleAsync(string md5, string ext, int width)
    {
        var originalKey = OriginalKey(md5, ext);
        if (await EnsureAsync(originalKey, () => DownloadAsync(md5, ext)) is not { } original) return null;
        var key = VariantKey(md5, ext, width);
        await WriteAtomicAsync(key, temp =>
        {
            using var source = SKBitmap.Decode(Path.Combine(CacheDir, originalKey)) ?? throw new InvalidDataException($"Cannot decode {originalKey}");
            using var scaled = source.Width > width
                ? source.Resize(new SKImageInfo(width, (int)Math.Round(source.Height * (double)width / source.Width)), new SKSamplingOptions(SKCubicResampler.CatmullRom))
                : source.Copy();
            using var webp = scaled.Encode(SKEncodedImageFormat.Webp, 80);
            using var file = File.Create(temp);
            webp.SaveTo(file);
            return Task.CompletedTask;
        });
        return await WriteMetaAsync(key, new Meta("image/webp", $"\"{original.ETag.Replace("\"", "")}-w{width}\""));
    }

    async Task<Meta?> ReadMetaAsync(string key)
    {
        try
        {
            await using var file = File.OpenRead(Path.Combine(CacheDir, $"{key}.json"));
            return await JsonSerializer.DeserializeAsync<Meta>(file, JsonSerializerOptions.Web);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    async Task<Meta> WriteMetaAsync(string key, Meta meta)
    {
        await WriteAtomicAsync($"{key}.json", async temp => await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(meta, JsonSerializerOptions.Web)));
        return meta;
    }

    /// <summary>Writes the cache file <paramref name="name"/> (a path below the cache folder, whose subfolder is created) through a temporary file and a rename, so no reader ever sees half of it.</summary>
    async Task WriteAtomicAsync(string name, Func<string, Task> write)
    {
        var file = Path.Combine(CacheDir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = $"{file}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            await write(temp);
            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            File.Delete(temp); // nothing left after the move; after a failure, the half-written file
        }
    }
}
