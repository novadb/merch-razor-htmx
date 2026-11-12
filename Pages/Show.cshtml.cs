using Merch.NovaDB;
using Merch.Site;
using Microsoft.AspNetCore.Mvc;

namespace Merch.Pages;

/// <summary><c>/{region}/{path}</c>: a CMS page (with its live product range) or a product from the PIM.</summary>
public sealed class ShowModel(Labels labels, SiteTree tree, Catalog catalog, Images images, IConfiguration config) : RegionPageModel(labels, tree)
{
    public string Title { get; private set; } = "";
    public Seo Seo { get; private set; } = null!;
    public List<Target> Crumbs { get; private set; } = [];
    public ContentContext Ctx { get; private set; } = null!;

    // A CMS page
    public VNode? Document { get; private set; }
    public ProductRange? Range { get; private set; }
    public Dictionary<int, string> GroupNames { get; private set; } = [];

    // A product
    public Product? Product { get; private set; }
    public List<Product> Similar { get; private set; } = [];
    public string? GroupName { get; private set; }

    public Task<IActionResult> OnGetAsync(string region, string? path) => LoadAsync(region, path);

    /// <summary>The facet and sort forms post to the page itself: the answer is the page, rendered with the posted fields.</summary>
    public Task<IActionResult> OnPostAsync(string region, string? path) => LoadAsync(region, path);

    async Task<IActionResult> LoadAsync(string code, string? rawPath)
    {
        if (Region.Find(code) is not { } region) return NotFound();
        // One URL per page: `/de/shop/` → `/de/shop`, `/de` → `/de/` (the canonical forms); a foreign query string (tracking) is kept.
        var path = (rawPath ?? "").Trim('/');
        var wanted = path.Length == 0 ? $"/{region.Code}/" : Request.Path.Value!.TrimEnd('/');
        if (Request.Path.Value != wanted) return RedirectPermanent(wanted + Request.QueryString);

        await LoadRegionAsync(region);
        var otherSite = Tree.LoadAsync(region.OtherLanguage);
        var groups = await Tree.LoadGroupsAsync(region);
        var found = Site!.ByPath.TryGetValue(path, out var page)
            ? await LoadPageAsync(page, await otherSite, groups)
            : await LoadProductAsync(path, groups);
        if (!found) return NotFound();
        Seo = Seo.Of(Request, config, Product?.Url ?? page!.Url, Alternates);
        return Page();
    }

    async Task<bool> LoadPageAsync(SitePage page, SiteMap otherSite, Dictionary<int, string> groups)
    {
        var embeddedIds = ContentContext.ReferencedIds(page.Content).Where(id => !Site!.Pages.ContainsKey(id)).ToList();
        var wantedImages = ContentContext.ImageIds(page.Content); // plus the teaser picture of every page
        foreach (var p in Site!.Pages.Values) if (p.ImageId is { } id) wantedImages.Add(id);
        var embedded = catalog.ByIdsAsync(embeddedIds, Region);
        var range = page.GroupRefs.Length > 0 ? catalog.LoadAsync(Region, $"{Schema.Product.Group} = {string.Join(", ", page.GroupRefs)}") : Task.FromResult<List<Product>>([]);
        var pictures = await images.ByIdsAsync(wantedImages);

        var targets = Site.Pages.Values.ToDictionary(p => p.Id, p => new Target(p.Url, p.Title, p.ImageId is { } id ? pictures.GetValueOrDefault(id) : null));
        foreach (var p in await embedded) targets[p.Id] = new Target(p.Url, p.Name, p.Image);
        Title = page.Title;
        Alternates = Region.Alternates(page.Path, otherSite.Pages.GetValueOrDefault(page.Id)?.Path);
        Crumbs = [.. Site.Breadcrumb(page).Select(ToCrumb)];
        Document = page.Content;
        Ctx = new ContentContext(Region, Text!, (await embedded).ToDictionary(p => p.Id), targets,
            ContentContext.ImageIds(page.Content).Where(pictures.ContainsKey).ToDictionary(id => id, id => pictures[id]));
        if (await range is { Count: > 0 } products) Range = Catalog.ApplySelection(products, Catalog.ParseSelection(Form), "name");
        GroupNames = groups;
        return true;
    }

    async Task<bool> LoadProductAsync(string path, Dictionary<int, string> groups)
    {
        if (path.Length == 0 || await catalog.ByPathAsync(Region, path) is not { } product) return false;
        var similar = catalog.ByIdsAsync(product.Similar, Region);
        var other = (await catalog.ByIdsAsync([product.Id], Region.OtherLanguage)).FirstOrDefault();
        var category = product.GroupId is { } group ? Site!.CategoryPage(group) : null;
        Title = product.Name;
        Alternates = Region.Alternates(product.Path, other?.Path);
        Crumbs = [.. Site!.Breadcrumb(category).Select(ToCrumb), new Target(product.Url, product.Name, null)];
        Ctx = new ContentContext(Region, Text!);
        Product = product;
        Similar = await similar;
        GroupName = product.GroupId is { } id ? groups.GetValueOrDefault(id) : null;
        return true;
    }

    /// <summary>A page in the breadcrumb, named as in the navigation.</summary>
    static Target ToCrumb(SitePage p) => new(p.Url, p.NavTitle, null);
}
