using Merch.NovaDB;

namespace Merch.Site;

/// <summary>A page of the website, linked into the tree.</summary>
public sealed class SitePage
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    /// <summary>Short name for the navigation and the breadcrumb (<c>merchPageNavTitle</c>); the title where a page has none.</summary>
    public required string NavTitle { get; init; }
    /// <summary>The path below the region, e.g. <c>shop/tassen</c>; empty for the home page.</summary>
    public required string Path { get; init; }
    /// <summary>URL of the page including the region, e.g. <c>/de/shop/tassen</c>.</summary>
    public required string Url { get; init; }
    public int? ImageId { get; init; }
    public int? ParentId { get; init; }
    /// <summary>Product groups whose live range the page shows (category pages).</summary>
    public required int[] GroupRefs { get; init; }
    public VNode? Content { get; init; }
    public List<SitePage> Children { get; } = [];
}

public sealed record SiteMap(SitePage Home, IReadOnlyDictionary<int, SitePage> Pages, IReadOnlyDictionary<string, SitePage> ByPath)
{
    /// <summary>The page that presents a product group on its own (its category page), else the first page listing it.</summary>
    public SitePage? CategoryPage(int groupId)
    {
        var pages = Pages.Values.Where(p => p.GroupRefs.Contains(groupId)).ToList();
        return pages.FirstOrDefault(p => p.GroupRefs.Length == 1) ?? pages.FirstOrDefault();
    }

    /// <summary>The trail from the home page down to the page, the page included.</summary>
    public List<SitePage> Breadcrumb(SitePage? page)
    {
        var trail = new List<SitePage>();
        for (var p = page; p is not null; p = p.ParentId is { } parent ? Pages.GetValueOrDefault(parent) : null) trail.Insert(0, p);
        return trail;
    }
}

/// <summary>The page tree of the website: paths, navigation and the lookup of a URL.</summary>
public sealed class SiteTree(Delivery delivery)
{
    /// <summary>Loads every page of the site in one language and links them into a tree.</summary>
    public async Task<SiteMap> LoadAsync(Region region)
    {
        var (objs, _) = await delivery.ListAsync<PageProps>(Schema.Types.Page, new(region.Culture, Sort: $"{Schema.Page.SortKey} asc"));
        // A page without a title in this language is untranslated and left out; only the home page (no parent) has no URL segment.
        var shown = objs.Where(o => o.Props.Title is not null && (o.Props.UrlSegment is { Length: > 0 } || o.Props.Parent is null))
            .ToDictionary(o => o.Meta.Id, o => o.Props);
        var homeId = shown.FirstOrDefault(p => p.Value.Parent is null).Key;
        if (homeId == 0) throw new InvalidDataException("No home page: no page without a parent page");

        string PathOf(int id) => id == homeId || shown[id].Parent is not { } parent ? ""
            : string.Join("/", new[] { shown.ContainsKey(parent) ? PathOf(parent) : "", shown[id].UrlSegment! }.Where(s => s.Length > 0));

        var pages = new Dictionary<int, SitePage>();
        foreach (var (id, p) in shown)
        {
            var path = PathOf(id);
            pages[id] = new SitePage
            {
                Id = id, Title = p.Title!, NavTitle = p.NavTitle ?? p.Title!, Path = path, Url = $"/{region.Code}/{path}",
                ImageId = p.Image, ParentId = p.Parent, GroupRefs = p.ProductGroups ?? [], Content = p.Content,
            };
        }
        foreach (var page in pages.Values) // in sort-key order, so children are sorted too
            if (page.ParentId is { } parent) pages.GetValueOrDefault(parent)?.Children.Add(page);
        return new SiteMap(pages[homeId], pages, pages.Values.ToDictionary(p => p.Path));
    }

    /// <summary>The first <paramref name="take"/> pages that match what a visitor typed, best first.</summary>
    public async Task<List<SitePage>> SearchAsync(Region region, string q, int take)
    {
        if (Catalog.SearchFilter(q, Schema.PageSearchFields) is not { } filter) return [];
        var hits = delivery.ListAsync<PageProps>(Schema.Types.Page, new(region.Culture, Filter: filter, Sort: "$score", Take: take));
        var site = await LoadAsync(region);
        return [.. (await hits).Objs.Select(o => site.Pages.GetValueOrDefault(o.Meta.Id)).OfType<SitePage>()];
    }

    /// <summary>Product groups (id → name) in one language.</summary>
    public async Task<Dictionary<int, string>> LoadGroupsAsync(Region region)
    {
        var (objs, _) = await delivery.ListAsync<NamedProps>(Schema.Types.Group, new(region.Culture));
        return objs.Where(o => o.Props.Name is not null).ToDictionary(o => o.Meta.Id, o => o.Props.Name!);
    }
}
