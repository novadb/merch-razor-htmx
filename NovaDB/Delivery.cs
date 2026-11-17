using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Merch.NovaDB;

/// <summary>An object as the Delivery API returns it: <c>meta</c> and the attributes of its type in <c>props</c>.</summary>
public sealed record NovaObject<TProps>(NovaMeta Meta, TProps Props);
public sealed record NovaMeta(int Id, string? ApiIdentifier);

/// <summary>
/// A read of <c>types/{type}/objects</c>. <c>Props</c>: 0 the type's API attributes, 1 every named attribute (needed for
/// NovaDB's own types). <c>Resolve</c>: reference attributes whose targets arrive in <c>Refs</c>. <c>Take</c>: only the
/// first objects, one request; without it every object is read, page by page. <c>Variants</c>: the market member
/// (<c>Schema.MarketVariant</c>) whose price the read returns; without it a product has no price. <c>Filter</c> and
/// <c>Sort</c> are Delivery expressions (<c>$score</c> sorts a full-text filter by relevance).
/// </summary>
public sealed record ListQuery(string Lang, string? Filter = null, string? Sort = null, int? Take = null, string? Variants = null, int Props = 0, string? Resolve = null);

/// <summary><c>Refs</c> are the objects <c>Resolve</c> names, of every page together.</summary>
public sealed record ListResult<TProps>(List<NovaObject<TProps>> Objs, List<NovaObject<JsonElement>> Refs);

/// <summary>
/// The Delivery login and the branch to read. The site does not start without a login (it is checked at start). It
/// comes from the environment or <c>dotnet user-secrets</c> and is never logged.
/// </summary>
public sealed class DeliveryOptions
{
    [Required(ErrorMessage = "NOVA_DELIVERY_URL is not set (see README, \"Run\")")] public string? Url { get; set; }
    [Required(ErrorMessage = "NOVA_DELIVERY_USER is not set (see README, \"Run\")")] public string? User { get; set; }
    [Required(ErrorMessage = "NOVA_DELIVERY_PASSWORD is not set (see README, \"Run\")")] public string? Password { get; set; }
    /// <summary>DEMO_BRANCH, not NOVA_BRANCH: shells often export NOVA_BRANCH for other work.</summary>
    public string Branch { get; set; } = Schema.Branch;
}

/// <summary>
/// Thin client for NovaDB's Delivery API on the <see cref="HttpClient"/> named "NovaDB" (base address and Basic auth
/// are set in <c>Program.cs</c>), with a short cache.
/// </summary>
public sealed class Delivery(IHttpClientFactory http, HybridCache cache, IOptions<DeliveryOptions> options)
{
    public const string HttpClientName = "NovaDB";
    static readonly HybridCacheEntryOptions Ttl = new() { Expiration = TimeSpan.FromSeconds(60), LocalCacheExpiration = TimeSpan.FromSeconds(60) };
    /// <summary>The most objects the API returns per request (<c>take</c>).</summary>
    const int PageSize = 1000;

    string Branch => options.Value.Branch;

    /// <summary>
    /// The objects of a type, read page by page (<c>more</c> says another follows). The pages are read one after the
    /// other, so the sort must not tie across a page border: the default order is by ID.
    /// </summary>
    public async Task<ListResult<TProps>> ListAsync<TProps>(string type, ListQuery q)
    {
        var take = q.Take ?? PageSize;
        var result = new ListResult<TProps>([], []);
        for (var skip = 0; ; skip += take)
        {
            var url = QueryHelpers.AddQueryString($"branches/{Uri.EscapeDataString(Branch)}/types/{type}/objects", new Dictionary<string, string?>
            {
                ["lang"] = q.Lang, ["props"] = q.Props.ToString(), ["skip"] = skip.ToString(), ["take"] = take.ToString(),
                ["filter"] = q.Filter, ["sort"] = q.Sort, ["variants"] = q.Variants, ["resolve"] = q.Resolve,
            }.Where(p => !string.IsNullOrEmpty(p.Value)));
            var page = await GetJsonAsync<ListPage<TProps>>(url);
            result.Objs.AddRange(page.Objs);
            result.Refs.AddRange(page.Refs ?? []);
            if (!page.More || q.Take is not null) return result;
        }
    }

    /// <summary>A file by its content address, <c>files/{md5}{extension}</c>, streamed: for the media proxy.</summary>
    public Task<HttpResponseMessage> GetFileAsync(string name) =>
        http.CreateClient(HttpClientName).GetAsync($"files/{name}", HttpCompletionOption.ResponseHeadersRead);

    sealed record ListPage<TProps>(List<NovaObject<TProps>> Objs, List<NovaObject<JsonElement>>? Refs, bool More);

    /// <summary>Concurrent requests for the same URL share one read; a failed read is not cached.</summary>
    ValueTask<T> GetJsonAsync<T>(string url) => cache.GetOrCreateAsync(url, token => ReadJsonAsync<T>(url, token), Ttl);

    async ValueTask<T> ReadJsonAsync<T>(string url, CancellationToken token)
    {
        using var response = await http.CreateClient(HttpClientName).GetAsync(url, token);
        // `url` is relative to NOVA_DELIVERY_URL: the host stays out of the log.
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Delivery {(int)response.StatusCode} for {url}: {await response.Content.ReadAsStringAsync(token)}");
        return await response.Content.ReadFromJsonAsync<T>(token) ?? throw new InvalidDataException($"Delivery sent no JSON for {url}");
    }
}
