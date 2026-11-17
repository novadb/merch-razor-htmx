using Merch.NovaDB;
using Merch.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Merch.Pages;

/// <summary><c>/{region}/{path}</c>: a CMS page (with its live product range) or a product from the PIM.</summary>
public sealed class ShowModel(RegionContext site, SiteTree tree, Catalog catalog, Images images, IConfiguration config) : PageModel
{
    public string Title { get; private set; } = "";
    public Seo Seo { get; private set; } = null!;
    public List<Target> Crumbs { get; private set; } = [];
    /// <summary>The CMS page the URL names; null where it names a product.</summary>
    public ContentContext? Document { get; private set; }
    public ProductView? ProductPage { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? path)
    {
        // One URL per page: `/de/shop/` → `/de/shop`, `/de` → `/de/` (the canonical forms); a foreign query string (tracking) is kept.
        var clean = (path ?? "").Trim('/');
        var wanted = clean.Length == 0 ? $"/{site.Region.Code}/" : Request.Path.Value!.TrimEnd('/');
        if (Request.Path.Value != wanted) return RedirectPermanent(wanted + Request.QueryString);

        var groups = await tree.LoadGroupsAsync(site.Region);
        if (site.Map.ByPath.TryGetValue(clean, out var page))
        {
            // The facet form asks for the range alone (htmx) and swaps it in place; a browser gets the whole page.
            if (Request.IsHtmx() && await RangeAsync(page, groups) is { } range) return Partial("_Range", range);
            await LoadPageAsync(page, groups);
        }
        else if (!await LoadProductAsync(clean, groups)) return NotFound();
        return Page();
    }

    /// <summary>"Show more" (htmx): the next batch of cards of the range, to append to the grid.</summary>
    public async Task<IActionResult> OnGetMoreAsync(string? path)
    {
        if (!site.Map.ByPath.TryGetValue((path ?? "").Trim('/'), out var page)) return NotFound();
        return await RangeAsync(page, await tree.LoadGroupsAsync(site.Region)) is { } range ? Partial("_MoreCards", range) : NotFound();
    }

    /// <summary>The live range of a category page with the selection of the address applied; null for a page that has none.</summary>
    async Task<RangeView?> RangeAsync(SitePage page, Dictionary<int, string> groups)
    {
        if (page.GroupRefs.Length == 0) return null;
        var products = await catalog.LoadAsync(site.Region, $"{Schema.Product.Group} = {string.Join(", ", page.GroupRefs)}");
        return products.Count > 0 ? new RangeView(Catalog.ApplySelection(products, Catalog.ParseSelection(Request.Query), "name"), groups) : null;
    }

    async Task LoadPageAsync(SitePage page, Dictionary<int, string> groups)
    {
        var map = site.Map;
        var embeddedIds = ContentContext.ReferencedIds(page.Content).Where(id => !map.Pages.ContainsKey(id)).ToList();
        var documentImages = ContentContext.ImageIds(page.Content);
        var wantedImages = documentImages.Union(map.Pages.Values.Select(p => p.ImageId).OfType<int>()).ToList(); // plus the teaser picture of every page
        var embeddedRead = catalog.ByIdsAsync(embeddedIds, site.Region);
        var rangeRead = RangeAsync(page, groups);
        var otherMapRead = tree.LoadAsync(site.Region.OtherLanguage); // the page's path in the other language, for hreflang
        var pictures = await images.ByIdsAsync(wantedImages);
        var embedded = await embeddedRead;

        var targets = map.Pages.Values.ToDictionary(p => p.Id, p => new Target(p.Url, p.Title, p.ImageId is { } id ? pictures.GetValueOrDefault(id) : null));
        foreach (var p in embedded) targets[p.Id] = new Target(p.Url, p.Name, p.Image);
        Title = page.Title;
        site.Alternates = site.Region.Alternates(page.Path, (await otherMapRead).Pages.GetValueOrDefault(page.Id)?.Path);
        Seo = Seo.Of(Request, config, page.Url, site.Alternates);
        Crumbs = [.. map.Breadcrumb(page).Select(ToCrumb)];
        Document = new ContentContext(page.Content, embedded.ToDictionary(p => p.Id), targets,
            documentImages.Where(pictures.ContainsKey).ToDictionary(id => id, id => pictures[id]), await rangeRead);
    }

    async Task<bool> LoadProductAsync(string path, Dictionary<int, string> groups)
    {
        if (path.Length == 0 || await catalog.ByPathAsync(site.Region, path) is not { } product) return false;
        var similar = catalog.ByIdsAsync(product.Similar, site.Region);
        var accessories = catalog.ByIdsAsync(product.Accessories, site.Region);
        var spareParts = catalog.ByIdsAsync(product.SpareParts, site.Region);
        var contents = catalog.SetContentsAsync(product, site.Region);
        var other = (await catalog.ByIdsAsync([product.Id], site.Region.OtherLanguage)).FirstOrDefault(); // its path in the other language, for hreflang
        var category = product.GroupId is { } group ? site.Map.CategoryPage(group) : null;
        Title = product.Seo.Title ?? product.Name;
        site.Alternates = site.Region.Alternates(product.Path, other?.Path);
        Seo = Seo.Of(Request, config, product.Url, site.Alternates, product.Seo);
        Crumbs = [.. site.Map.Breadcrumb(category).Select(ToCrumb), new Target(product.Url, product.Name, null)];
        ProductPage = new ProductView(product, product.GroupId is { } id ? groups.GetValueOrDefault(id) : null,
            await contents, await similar, await accessories, await spareParts);
        return true;
    }

    /// <summary>A page in the breadcrumb, named as in the navigation.</summary>
    static Target ToCrumb(SitePage p) => new(p.Url, p.NavTitle, null);
}
