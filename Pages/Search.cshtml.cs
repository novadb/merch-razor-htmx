using Merch.NovaDB;
using Merch.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Merch.Pages;

/// <summary><c>/{region}/search</c>: full-text search over products (with facets) and pages; the query is the field <c>q</c>. Also the typeahead's list.</summary>
public sealed class SearchModel(RegionContext site, SiteTree tree, Catalog catalog) : PageModel
{
    public string Query { get; private set; } = "";
    public RangeView Range { get; private set; } = null!;
    public List<SitePage> Pages { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? q)
    {
        Query = (q ?? "").Trim();
        // The facet form asks for the range alone (htmx) and swaps it in place; a browser gets the whole page.
        if (Request.IsHtmx()) return Partial("_Range", await RangeAsync());
        var pages = tree.SearchAsync(site.Region, Query, 5);
        Range = await RangeAsync();
        Pages = await pages;
        site.Alternates = Region.All.ToDictionary(r => r, r => $"/{r.Code}/search");
        return Page();
    }

    /// <summary>"Show more" (htmx): the next batch of cards of the range, to append to the grid.</summary>
    public async Task<IActionResult> OnGetMoreAsync(string? q)
    {
        Query = (q ?? "").Trim();
        return Partial("_MoreCards", await RangeAsync());
    }

    /// <summary>The typeahead (htmx, while typing): its list of up to 5 products and 3 pages, empty below two characters. <paramref name="list"/> is the list's ID.</summary>
    public async Task<IActionResult> OnGetSuggestAsync(string? q, string? list)
    {
        var query = (q ?? "").Trim();
        var filter = query.Length < 2 ? null : Catalog.SearchFilter(query, Schema.ProductSearchFields);
        if (filter is null) return Partial("_Suggestions", new SuggestView(list ?? "", null, [], []));
        var products = catalog.LoadAsync(site.Region, filter, "$score", take: 5);
        var pages = tree.SearchAsync(site.Region, query, 3);
        return Partial("_Suggestions", new SuggestView(list ?? "", query, await products, await pages));
    }

    async Task<RangeView> RangeAsync()
    {
        var filter = Catalog.SearchFilter(Query, Schema.ProductSearchFields);
        var products = await catalog.LoadAsync(site.Region, filter, filter is null ? null : "$score");
        var selection = Catalog.ParseSelection(Request.Query);
        return new RangeView(Catalog.ApplySelection(products, selection, filter is null ? "name" : "relevance"), await tree.LoadGroupsAsync(site.Region), Query);
    }
}
