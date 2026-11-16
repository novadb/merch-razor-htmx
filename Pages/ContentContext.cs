using System.Text.RegularExpressions;
using Merch.NovaDB;
using Merch.Site;

namespace Merch.Pages;

/// <summary>Everything a link or teaser in content can point at: pages and products, by object ID.</summary>
public sealed record Target(string Url, string Title, string? Image);

/// <summary>
/// What the partials render with. <c>Products</c>: the products a document embeds; <c>Targets</c>: every page and
/// product a link or teaser can point at; <c>Images</c>: the picture URL (<c>/media/{md5}/original{ext}</c>) of each image object
/// the document embeds. All by object ID.
/// </summary>
public sealed partial record ContentContext(Region Region, Texts Text, IReadOnlyDictionary<int, Product> Products, IReadOnlyDictionary<int, Target> Targets, IReadOnlyDictionary<int, string> Images)
{
    public ContentContext(Region region, Texts text) : this(region, text, new Dictionary<int, Product>(), new Dictionary<int, Target>(), new Dictionary<int, string>()) { }

    /// <summary>Object IDs a document refers to: embedded objects and links inside rich text.</summary>
    public static HashSet<int> ReferencedIds(VNode? node, HashSet<int>? into = null)
    {
        into ??= [];
        if (node is null) return into;
        if (node is { Type: "object", ObjectId: { } id }) into.Add(id);
        foreach (Match m in ObjectLink().Matches(node.Content ?? "")) into.Add(int.Parse(m.Groups[1].Value));
        foreach (var child in node.Children ?? []) ReferencedIds(child, into);
        return into;
    }

    /// <summary>Object IDs of the images a document embeds.</summary>
    public static HashSet<int> ImageIds(VNode? node, HashSet<int>? into = null)
    {
        into ??= [];
        if (node is null) return into;
        if (node is { Type: "image", ObjectId: { } id }) into.Add(id);
        foreach (var child in node.Children ?? []) ImageIds(child, into);
        return into;
    }

    /// <summary>
    /// The first image node in document order that renders (its object has a known picture). Compare it by reference:
    /// a record's <c>==</c> compares values, so the same image embedded twice would be equal.
    /// </summary>
    public VNode? FirstImage(VNode? node)
    {
        if (node is null) return null;
        if (node is { Type: "image", ObjectId: { } id } && Images.ContainsKey(id)) return node;
        foreach (var child in node.Children ?? [])
            if (FirstImage(child) is { } found) return found;
        return null;
    }

    /// <summary>Turns CMS links (<c>object?branch=…&amp;objectId=N</c>) into site URLs and drops editor-only attributes.</summary>
    public string RewriteLinks(string html) => EditorAttribute().Replace(
        CmsLink().Replace(Catalog.CleanHtml(html), m => $"href=\"{Targets.GetValueOrDefault(int.Parse(m.Groups[1].Value))?.Url ?? "#"}\""), "");

    [GeneratedRegex(@"objectId=(\d+)")]
    private static partial Regex ObjectLink();

    [GeneratedRegex(@"href=""/?object\?branch=[^&""]+&amp;objectId=(\d+)""")]
    private static partial Regex CmsLink();

    [GeneratedRegex(@"\sdata-nova-[a-z]+=""[^""]*""")]
    private static partial Regex EditorAttribute();
}
