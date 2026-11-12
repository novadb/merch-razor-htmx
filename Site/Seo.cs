namespace Merch.Site;

/// <summary>Canonical URL and language/region variants (<c>rel="canonical"</c>, <c>rel="alternate" hreflang</c>) for search engines.</summary>
public sealed record Seo(string Canonical, IReadOnlyList<(string Hreflang, string Href)> Alternates)
{
    /// <summary>
    /// <paramref name="path"/> is the page's clean URL (no filters, sorting, tracking parameters). <paramref name="variants"/>
    /// holds the same content in every region that has it, the page itself included: every variant lists all of them,
    /// <c>x-default</c> is <c>/</c>, which picks a region.
    /// </summary>
    public static Seo Of(HttpRequest request, IConfiguration config, string path, IReadOnlyDictionary<Region, string> variants)
    {
        var origin = OriginOf(request, config);
        var alternates = Region.All.Where(variants.ContainsKey).Select(r => (r.HtmlLang, origin + variants[r])).ToList();
        if (alternates.Count > 0) alternates.Add(("x-default", $"{origin}/"));
        return new Seo(origin + path, alternates);
    }

    /// <summary>
    /// Public origin (<c>https://host</c>) for absolute URLs: <c>SITE_URL</c> if set, else the request's, which
    /// <c>UseForwardedHeaders</c> has taken from the ingress (<c>X-Forwarded-Proto/Host</c>). A client can forge those
    /// headers: set <c>SITE_URL</c> in production.
    /// </summary>
    static string OriginOf(HttpRequest request, IConfiguration config) =>
        config["SITE_URL"] is { Length: > 0 } site ? site.TrimEnd('/') : $"{request.Scheme}://{request.Host}";
}
