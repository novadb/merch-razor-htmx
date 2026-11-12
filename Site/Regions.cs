using System.Globalization;

namespace Merch.Site;

public enum Market { EU, UK, US }

/// <summary>
/// A region is the first URL segment (<c>/de/…</c>, <c>/uk/…</c>, <c>/us/…</c>). It fixes the content language, the UI
/// texts, the market and the currency: <c>de</c> is German in euros, <c>uk</c> English in pounds, <c>us</c> English in dollars.
/// </summary>
/// <param name="Culture">NovaDB content language: <c>uk</c> and <c>us</c> share the English content.</param>
/// <param name="HtmlLang">Language tag for <c>&lt;html lang&gt;</c> and <c>hreflang</c>.</param>
/// <param name="Short">Label of the region in the header switch; <paramref name="Title"/> is its tooltip.</param>
/// <param name="PriceCulture">The culture prices are formatted in; its currency is the market's.</param>
public sealed record Region(string Code, string Culture, string HtmlLang, Market Market, string Short, string Title, string PriceCulture)
{
    public static readonly Region De = new("de", "de-DE", "de", Market.EU, "DE", "Deutschland · Euro", "de-DE");
    public static readonly Region Uk = new("uk", "en-US", "en-GB", Market.UK, "UK", "United Kingdom · Pound", "en-GB");
    public static readonly Region Us = new("us", "en-US", "en-US", Market.US, "US", "United States · Dollar", "en-US");
    public static readonly IReadOnlyList<Region> All = [De, Uk, Us];

    /// <summary>The region a URL segment names; anything else, e.g. <c>en</c>, is none.</summary>
    public static Region? Find(string? code) => All.FirstOrDefault(r => r.Code == code);

    /// <summary>The region of the other content language: where the German and English version of an object are looked up.</summary>
    public Region OtherLanguage => this == De ? Uk : De;

    /// <summary>The price as the culture writes it, e.g. <c>24,90 €</c>; amount and currency are joined by a no-break space, as in a browser's <c>Intl.NumberFormat</c>, so they never wrap apart.</summary>
    public string FormatPrice(decimal? value) => value?.ToString("C", CultureInfo.GetCultureInfo(PriceCulture)).Replace(' ', '\u00A0') ?? "";

    /// <summary>The same content in every region: <paramref name="own"/> is the path (without region) in this region's content language, <paramref name="other"/> in the other one.</summary>
    public IReadOnlyDictionary<Region, string> Alternates(string? own, string? other)
    {
        var result = new Dictionary<Region, string>();
        foreach (var r in All)
            if ((r.Culture == Culture ? own : other) is { } path) result[r] = $"/{r.Code}/{path}";
        return result;
    }
}
