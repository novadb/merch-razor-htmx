namespace Merch.Site;

/// <summary>What a product's SEO attributes ask of search engines; <c>Title</c> replaces the product's name in the page title.</summary>
public sealed record SeoHints(string? Title, string? Description, bool NoIndex, bool NoFollow);

/// <summary>Canonical URL, language/region variants (<c>rel="canonical"</c>, <c>rel="alternate" hreflang</c>), description and robots hint for search engines.</summary>
/// <param name="Robots">Value of <c>&lt;meta name="robots"&gt;</c>, e.g. <c>noindex, nofollow</c>; null: the default.</param>
public sealed record Seo(string Canonical, IReadOnlyList<(string Hreflang, string Href)> Alternates, string? Description = null, string? Robots = null)
{
    /// <summary>
    /// <paramref name="path"/> is the page's clean URL (no filters, sorting, tracking parameters). <paramref name="variants"/>
    /// holds the same content in every region that has it, the page itself included: every variant lists all of them,
    /// <c>x-default</c> is <c>/</c>, which picks a region. A page that asks not to be indexed lists no variants: it is not part of the language set.
    /// </summary>
    public static Seo Of(HttpRequest request, IConfiguration config, string path, IReadOnlyDictionary<Region, string> variants, SeoHints? hints = null)
    {
        var origin = OriginOf(request, config);
        var alternates = hints is { NoIndex: true } ? [] : Region.All.Where(variants.ContainsKey).Select(r => (r.HtmlLang, origin + variants[r])).ToList();
        if (alternates.Count > 0) alternates.Add(("x-default", $"{origin}/"));
        var robots = string.Join(", ", new[] { hints is { NoIndex: true } ? "noindex" : null, hints is { NoFollow: true } ? "nofollow" : null }.OfType<string>());
        return new Seo(origin + path, alternates, hints?.Description, robots.Length > 0 ? robots : null);
    }

    /// <summary>
    /// Public origin (<c>https://host</c>) for absolute URLs: <c>SITE_URL</c> if set, else the request's, which
    /// <c>UseForwardedHeaders</c> has taken from the ingress (<c>X-Forwarded-Proto/Host</c>). A client can forge those
    /// headers: set <c>SITE_URL</c> in production.
    /// </summary>
    static string OriginOf(HttpRequest request, IConfiguration config) =>
        config["SITE_URL"] is { Length: > 0 } site ? site.TrimEnd('/') : $"{request.Scheme}://{request.Host}";
}
