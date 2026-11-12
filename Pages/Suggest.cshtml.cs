using Merch.NovaDB;
using Merch.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Merch.Pages;

/// <summary>
/// <c>POST /api/suggest</c> with the form fields <c>lang</c>, <c>q</c> and <c>list</c> (the list's ID): the typeahead's
/// list as HTML, up to 5 products and 3 pages; empty below two characters. htmx swaps it in place of the empty list.
/// </summary>
public sealed class SuggestModel(Labels labels, SiteTree tree, Catalog catalog) : PageModel
{
    public string ListId { get; private set; } = "";
    /// <summary>What was typed (two characters or more); null for the empty list.</summary>
    public string? Query { get; private set; }
    public Region Region { get; private set; } = null!;
    public Texts Text { get; private set; } = null!;
    public List<Product> Products { get; private set; } = [];
    public List<SitePage> Pages { get; private set; } = [];

    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync()
    {
        if (Region.Find(Request.Form["lang"]) is not { } region) return NotFound();
        Region = region;
        ListId = Request.Form["list"].ToString();
        var q = Request.Form["q"].ToString().Trim();
        var filter = q.Length < 2 ? null : Catalog.SearchFilter(q, Schema.ProductSearchFields);
        Text = await labels.LoadAsync(region);
        if (filter is null) return Page();
        var products = catalog.LoadAsync(region, filter, "$score");
        var pages = tree.SearchAsync(region, q, 3);
        Query = q;
        Products = (await products).Take(5).ToList();
        Pages = await pages;
        return Page();
    }
}
