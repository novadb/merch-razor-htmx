using Merch.NovaDB;
using Merch.Site;
using Microsoft.AspNetCore.Mvc;

namespace Merch.Pages;

/// <summary><c>/{region}/search</c>: full-text search over products (with facets) and pages; the query is the posted field <c>q</c>.</summary>
public sealed class SearchModel(Labels labels, SiteTree tree, Catalog catalog) : RegionPageModel(labels, tree)
{
    public string Query { get; private set; } = "";
    public ProductRange Range { get; private set; } = null!;
    public List<SitePage> Pages { get; private set; } = [];
    public Dictionary<int, string> GroupNames { get; private set; } = [];

    public Task<IActionResult> OnGetAsync(string region) => LoadAsync(region);

    /// <summary>A posted search or range form renders this page again with its fields.</summary>
    public Task<IActionResult> OnPostAsync(string region) => LoadAsync(region);

    async Task<IActionResult> LoadAsync(string code)
    {
        if (Region.Find(code) is not { } region) return NotFound();
        await LoadRegionAsync(region);
        Query = Form["q"].ToString().Trim();
        var filter = Catalog.SearchFilter(Query, Schema.ProductSearchFields);
        var products = catalog.LoadAsync(region, filter, filter is null ? null : "$score");
        var pages = Tree.SearchAsync(region, Query, 5);
        GroupNames = await Tree.LoadGroupsAsync(region);
        Range = Catalog.ApplySelection(await products, Catalog.ParseSelection(Form), filter is null ? "name" : "relevance");
        Pages = await pages;
        Alternates = Region.All.ToDictionary(r => r, r => $"/{r.Code}/search");
        return Page();
    }
}
