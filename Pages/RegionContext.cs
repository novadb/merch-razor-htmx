using Merch.Site;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace Merch.Pages;

/// <summary>
/// The region of the request and what every page and the layout need from it: its texts and the page tree for the
/// navigation. <see cref="RegionFilter"/> fills it before a page's handler runs; the error page fills it itself.
/// </summary>
public sealed class RegionContext(Labels labels, SiteTree tree)
{
    Texts? text;
    SiteMap? map;

    public Region Region { get; private set; } = Region.Us;
    /// <summary>False only on the error page when NovaDB cannot be reached: it then stands alone in English.</summary>
    public bool Loaded => text is not null && map is not null;
    public Texts Text => text ?? throw new InvalidOperationException("The region is not loaded");
    public SiteMap Map => map ?? throw new InvalidOperationException("The region is not loaded");
    /// <summary>Region → URL of the same content, for the region switch; a region missing here links to its home page. Set by the page that knows its counterparts.</summary>
    public IReadOnlyDictionary<Region, string> Alternates { get; set; } = new Dictionary<Region, string>();

    public async Task LoadAsync(Region region)
    {
        Region = region;
        var (loadedText, loadedMap) = (labels.LoadAsync(region), tree.LoadAsync(region));
        (text, map) = (await loadedText, await loadedMap);
    }
}

/// <summary>Loads the region of every page whose route has one, before its handler runs.</summary>
public sealed class RegionFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (Region.Find(context.RouteData.Values["region"] as string) is { } region)
            await http.RequestServices.GetRequiredService<RegionContext>().LoadAsync(region);
        // A page answers htmx with only the part it asks for, at the same URL: a cache must keep the two answers apart.
        http.Response.Headers.Append(HeaderNames.Vary, "HX-Request");
        await next();
    }
}

public static class HtmxRequest
{
    /// <summary>
    /// Whether htmx asks for a part of the page. Not when the visitor went back or forward: htmx then fetches the address
    /// again to show the page as it was, and wants the whole page.
    /// </summary>
    public static bool IsHtmx(this HttpRequest request) =>
        request.Headers.ContainsKey("HX-Request") && !request.Headers.ContainsKey("HX-History-Restore-Request");
}
