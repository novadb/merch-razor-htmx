using System.Collections.Concurrent;
using Merch.NovaDB;
using Microsoft.AspNetCore.Html;

namespace Merch.Site;

/// <summary>
/// Everything the site prints that is not content, from NovaDB, in one language:
/// UI texts are objects of the type <c>merchLabel</c> (one per key, the text per language; <c>{n}</c> and <c>{q}</c> are
/// placeholders; a count has one label per plural form, <c>results.one</c>, <c>results.other</c>). The names of the
/// product attributes are read from their definitions (NovaDB's <c>typeAttribute</c>): the name an editor sees in the
/// CMS is the name on the site. Colours, materials and finishes are PIM objects a product references, by object ID.
/// </summary>
public sealed class Labels(Delivery delivery, ILogger<Labels> logger)
{
    /// <summary>UI text key → the product or article attribute whose name it is.</summary>
    internal static readonly Dictionary<string, string> AttributeText = new()
    {
        ["group"] = Schema.Product.Group, ["sku"] = Schema.Article.Number, ["colour"] = Schema.Article.Color,
        ["material"] = Schema.Product.Materials, ["price"] = Schema.Product.Price, ["availability"] = Schema.Article.Stock,
        ["volume"] = Schema.Product.Volume, ["finish"] = Schema.Product.Finishes, ["pieces"] = Schema.Product.Pieces,
        ["screenSize"] = Schema.Product.ScreenSize, ["delivery"] = Schema.Article.DeliveryDays,
    };

    /// <summary>Labels, attribute names and PIM values of a region's content language (five requests, cached like every Delivery read).</summary>
    public async Task<Texts> LoadAsync(Region region)
    {
        var q = new ListQuery(region.Culture);
        var labels = delivery.ListAsync<LabelProps>(Schema.Types.Label, q);
        var attributes = delivery.ListAsync<NamedProps>(Schema.Types.Attribute, q with { Props = 1, Filter = $"meta.apiIdentifier = {string.Join(", ", AttributeText.Values)}" });
        var values = new[] { Schema.Types.Color, Schema.Types.Material, Schema.Types.Finish }.Select(type => delivery.ListAsync<NamedProps>(type, q)).ToList();

        var texts = new Dictionary<string, string>();
        foreach (var o in (await labels).Objs)
            if (o.Props is { Key: { } key, Text: { } text }) texts[key] = text;
        foreach (var o in (await attributes).Objs)
            if (o.Props.Name is { } name) texts[o.Meta.ApiIdentifier!] = name;
        var pim = (await Task.WhenAll(values)).SelectMany(r => r.Objs).ToDictionary(o => o.Meta.Id, o => o.Props);
        return new Texts(texts, pim, region, logger);
    }
}

/// <summary>
/// The texts of a region. <c>text["key"]</c> is a plain text (printed encoded), or the name of a product attribute
/// (<see cref="Labels.AttributeText"/>); <see cref="Html"/> is for the few texts that carry inline HTML (links, emphasis).
/// A text the language lacks shows as <c>⟦key⟧</c>, never as another language's text, and is logged once per process.
/// </summary>
public sealed class Texts(Dictionary<string, string> texts, Dictionary<int, NamedProps> values, Region region, ILogger logger)
{
    static readonly ConcurrentDictionary<string, bool> Reported = new();

    public string this[string key] => Labels.AttributeText.TryGetValue(key, out var attribute)
        ? texts.GetValueOrDefault(attribute) ?? Missing(attribute, "attribute name")
        : texts.GetValueOrDefault(key) ?? Missing(key, "merchLabel");

    public IHtmlContent Html(string key) => new HtmlString(this[key]);

    /// <summary>German and English know two plural forms: one for exactly 1, other for every other count.</summary>
    string Count(string key, int n) => this[$"{key}.{(n == 1 ? "one" : "other")}"].Replace("{n}", n.ToString());
    public string Results(int n) => Count("results", n);
    public string PieceCount(int n) => Count("pieceCount", n);
    public string SearchTitle(string q) => q.Length > 0 ? this["searchTitle"].Replace("{q}", q) : this["searchTitle.empty"];

    /// <summary>The name of a PIM value (colour, material, finish object, by ID) in this language.</summary>
    public string Value(int id) => values.GetValueOrDefault(id)?.Name ?? Missing($"value {id}", "name of a colour, material or finish");
    /// <summary>The swatch of a colour (by ID), a CSS colour; null if the colour has none.</summary>
    public string? Swatch(int id) => values.GetValueOrDefault(id)?.Hex;

    /// <summary>The strings <c>shop.js</c> prints (the buy button, the cart's visible texts).</summary>
    public Dictionary<string, string> ForClient() => new()
    {
        ["addToCart"] = this["addToCart"], ["cart"] = this["cart"], ["subtotal"] = this["subtotal"], ["checkout"] = this["checkout"],
        ["cartEmpty"] = this["cartEmpty"], ["cartNotice"] = this["cartNotice"],
    };

    string Missing(string key, string where)
    {
        if (Reported.TryAdd($"{region.Code}:{key}", true)) logger.LogError("'{Key}' has no text in {Culture} ({Where})", key, region.Culture, where);
        return $"⟦{key}⟧";
    }
}
