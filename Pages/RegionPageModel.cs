using Merch.Site;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Merch.Pages;

/// <summary>
/// What every page and the layout need: the region (language, texts, market and currency follow from it), its texts,
/// the page tree for the navigation, and the region switch's targets.
/// </summary>
public abstract class RegionPageModel(Labels labels, SiteTree tree) : PageModel
{
    public Region Region { get; private set; } = Region.Us;
    /// <summary>Null only on the error page when NovaDB cannot be reached: it then stands alone in English.</summary>
    public Texts? Text { get; private set; }
    public SiteMap? Site { get; private set; }
    /// <summary>Region → URL of the same content; a region missing here links to its home page.</summary>
    public IReadOnlyDictionary<Region, string> Alternates { get; protected set; } = new Dictionary<Region, string>();
    protected SiteTree Tree => tree;
    /// <summary>The fields of a POST form (filters, sorting, search); empty when the page was simply opened. The site reads no query string.</summary>
    protected IFormCollection Form { get; private set; } = FormCollection.Empty;

    protected async Task LoadRegionAsync(Region region)
    {
        Region = region;
        var (text, site) = (labels.LoadAsync(region), tree.LoadAsync(region));
        (Text, Site) = (await text, await site);
        if (Request.HasFormContentType) Form = await Request.ReadFormAsync();
    }
}
