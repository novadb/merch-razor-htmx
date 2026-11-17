using Merch.Site;

namespace Merch.Pages;

// What the partials render, one record per partial that needs more than one thing.

/// <summary>A live range: the filtered, sorted products with their facets, the names of the product groups, and the search query a search page's range belongs to (it travels in a hidden field).</summary>
public sealed record RangeView(ProductRange Range, IReadOnlyDictionary<int, string> GroupNames, string? Query = null);

/// <summary>A product page: the product, the name of its group, the rows of a set, and the rows of related products.</summary>
public sealed record ProductView(Product Product, string? GroupName, List<SetRow> Contents, List<Product> Similar, List<Product> Accessories, List<Product> SpareParts);

/// <summary>The search form with its typeahead. <c>ListId</c> names the typeahead's list: the header's and the search page's must differ.</summary>
public sealed record SearchBoxView(string ListId, bool Large = false, string? Query = null);

/// <summary>The typeahead's list; <c>Query</c> is null for the empty list (nothing typed yet, or fewer than two characters).</summary>
public sealed record SuggestView(string ListId, string? Query, List<Product> Products, List<SitePage> Pages);
