using System.Text.Json;
using System.Text.RegularExpressions;
using Merch.NovaDB;

namespace Merch.Site;

/// <summary>Pictures by file object ID → the unchangeable URLs of the media proxy (<see cref="Media"/>).</summary>
public sealed partial class Images(Delivery delivery)
{
    /// <summary>
    /// Picture URLs (<c>/media/{md5}/original{extension}</c>) by file object ID, from objects the API delivered (the <c>refs</c> of
    /// a read with <c>resolve</c>): a content object only holds the file object's ID, the hash that names the file is on
    /// that object. An image without hash or extension has no file and is left out.
    /// </summary>
    public static Dictionary<int, string> From(IEnumerable<NovaObject<JsonElement>> objs) => From(objs.Select(o => new NovaObject<ImageProps>(o.Meta, o.Props.Deserialize<ImageProps>(JsonSerializerOptions.Web)!)));

    static Dictionary<int, string> From(IEnumerable<NovaObject<ImageProps>> objs) => objs
        .Where(o => o.Props is { Md5.Length: > 0, Extension.Length: > 0 })
        .ToDictionary(o => o.Meta.Id, o => $"/media/{o.Props.Md5}/original{o.Props.Extension}");

    /// <summary>The images of pages and documents, whose IDs are known beforehand (a document node names its image by ID). The type is language-neutral, <c>en-US</c> only satisfies the request.</summary>
    public async Task<Dictionary<int, string>> ByIdsAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0) return [];
        return From((await delivery.ListAsync<ImageProps>(Schema.Types.Image, new("en-US", Filter: $"meta.id = {string.Join(", ", ids)}"))).Objs);
    }

    /// <summary>The folder (<c>/media/{md5}</c>) and extension of a picture URL <c>/media/{md5}/original{ext}</c> that has scaled variants (<c>/media/{md5}/{width}{ext}.webp</c>, which keep the extension because NovaDB serves a file only under it): not SVG, which is vector, nor a URL that is not a media file.</summary>
    static (string Folder, string Ext)? Scalable(string src) =>
        OriginalUrl().Match(src) is { Success: true } m && m.Groups["ext"].Value != ".svg" ? (m.Groups["folder"].Value, m.Groups["ext"].Value) : null;

    [GeneratedRegex(@"^(?<folder>/media/[^/]+)/original(?<ext>\.[a-z0-9]{1,5})$")]
    private static partial Regex OriginalUrl();

    /// <summary><c>srcset</c> of a picture URL: all its scaled variants; null for SVG and for a URL that is not a media file.</summary>
    public static string? SrcSet(string src) =>
        Scalable(src) is var (folder, ext) ? string.Join(", ", Media.Widths.Select(w => $"{folder}/{w}{ext}.webp {w}w")) : null;

    /// <summary>The smallest scaled variant of a picture URL, for small pictures such as the typeahead's; SVG stays as it is.</summary>
    public static string SmallSrc(string src) => Scalable(src) is var (folder, ext) ? $"{folder}/{Media.Widths[0]}{ext}.webp" : src;
}
