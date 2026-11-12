using System.Text.Json.Serialization;

namespace Merch.NovaDB;

/// <summary>
/// Every NovaDB name the site depends on, in one place. IDs of branches and object types differ from installation to
/// installation, API identifiers do not, so the code addresses everything by identifier. The identifiers carry the
/// prefix <c>merch</c> because they must be unique across branches.
/// The Delivery API serves an identifier with a lower-case first letter. A numeric ID belongs here too (as a named
/// constant with a comment) if one is ever unavoidable; today there is none.
/// </summary>
public static class Schema
{
    /// <summary>API identifier of the branch (default of <c>DEMO_BRANCH</c>; a numeric branch ID works as well).</summary>
    public const string Branch = "merchDemo";

    /// <summary>Object types, as used in <c>types/{type}/objects</c>.</summary>
    public static class Types
    {
        public const string Page = "merchPage";
        public const string Product = "merchProduct";
        public const string Group = "merchProductGroup";
        public const string Image = "image";
        public const string Label = "merchLabel";
        public const string Color = "merchColor";
        public const string Material = "merchMaterial";
        public const string Finish = "merchFinish";
        /// <summary>NovaDB's own type of attribute definitions: the site prints a product attribute under its name from here.</summary>
        public const string Attribute = "typeAttribute";
    }

    /// <summary>Attributes of a label: one UI text, its key language-neutral, its text per language.</summary>
    public static class Label
    {
        public const string Key = "merchLabelKey";
        public const string Text = "merchLabelText";
    }

    /// <summary>Attributes of a page. <c>Parent</c> is the Sitemap relation to the page above.</summary>
    public static class Page
    {
        public const string Title = "merchPageTitle";
        public const string NavTitle = "merchPageNavTitle";
        public const string UrlSegment = "merchPageUrlSegment";
        public const string SortKey = "merchPageSortKey";
        public const string Image = "merchPageImage";
        public const string Content = "merchPageContent";
        public const string ProductGroups = "merchPageProductGroups";
        public const string Parent = "merchPageParent";
    }

    /// <summary>Attributes of a product. Colour, materials and finishes reference <c>merchColor</c>, <c>merchMaterial</c>, <c>merchFinish</c> objects.</summary>
    public static class Product
    {
        public const string Name = "merchProductName";
        public const string Description = "merchProductDescription";
        public const string Sku = "merchProductSku";
        public const string Group = "merchProductGroupRef";
        public const string Price = "merchProductPrice";
        public const string InStock = "merchProductInStock";
        public const string Images = "merchProductImages";
        public const string Similar = "merchProductSimilar";
        public const string UrlSegment = "merchProductUrlSegment";
        public const string ShopifyId = "merchProductShopifyId";
        public const string Color = "merchProductColorRef";
        public const string Materials = "merchProductMaterialRefs";
        public const string CupVolume = "merchProductCupVolume";
        public const string Finish = "merchProductFinishRefs";
        public const string Pieces = "merchProductPieces";
        public const string ScreenSize = "merchProductScreenSize";
    }

    /// <summary>
    /// Attributes of an image (a file type: the object IS the file). MD5 and extension give the content address
    /// <c>files/{md5}{extension}</c>. The type must release both to the Delivery API.
    /// </summary>
    public static class Image
    {
        public const string Md5 = "externalBinaryMD5";
        public const string Extension = "externalBinaryFileExtension";
    }

    /// <summary>
    /// Name of a product group, a colour, a material, a finish and of an attribute definition: NovaDB's system attribute
    /// <c>AttributeName</c> (small ID, the same everywhere), per language.
    /// </summary>
    public const string Name = "attributeName";

    /// <summary>The swatch of a colour: a CSS hex value.</summary>
    public const string ColorHex = "merchColorHex";

    /// <summary>Market → the variant of the price axis, the value of the Delivery API's <c>variants</c> parameter.</summary>
    public static string MarketVariant(Site.Market market) => market switch
    {
        Site.Market.EU => "merchMarketEU",
        Site.Market.UK => "merchMarketUK",
        Site.Market.US => "merchMarketUS",
        _ => throw new ArgumentOutOfRangeException(nameof(market)),
    };

    /// <summary>Fields the full-text search looks in.</summary>
    public static readonly string[] ProductSearchFields = [Product.Name, Product.Sku, Product.Description];
    public static readonly string[] PageSearchFields = [Page.Title, Page.Content];
}

// The props of each type the site reads, as the Delivery API returns them. A missing key is null: unset or untranslated.

public sealed record PageProps(
    [property: JsonPropertyName(Schema.Page.Title)] string? Title,
    [property: JsonPropertyName(Schema.Page.NavTitle)] string? NavTitle,
    [property: JsonPropertyName(Schema.Page.UrlSegment)] string? UrlSegment,
    [property: JsonPropertyName(Schema.Page.Image)] int? Image,
    [property: JsonPropertyName(Schema.Page.Content)] VNode? Content,
    [property: JsonPropertyName(Schema.Page.ProductGroups)] int[]? ProductGroups,
    [property: JsonPropertyName(Schema.Page.Parent)] int? Parent);

public sealed record ProductProps(
    [property: JsonPropertyName(Schema.Product.Name)] string? Name,
    [property: JsonPropertyName(Schema.Product.Description)] RichText? Description,
    [property: JsonPropertyName(Schema.Product.Sku)] string? Sku,
    [property: JsonPropertyName(Schema.Product.Group)] int? Group,
    [property: JsonPropertyName(Schema.Product.Price)] decimal? Price,
    [property: JsonPropertyName(Schema.Product.InStock)] bool? InStock,
    [property: JsonPropertyName(Schema.Product.Images)] int[]? Images,
    [property: JsonPropertyName(Schema.Product.Similar)] int[]? Similar,
    [property: JsonPropertyName(Schema.Product.UrlSegment)] string? UrlSegment,
    [property: JsonPropertyName(Schema.Product.ShopifyId)] string? ShopifyId,
    [property: JsonPropertyName(Schema.Product.Color)] int? Color,
    [property: JsonPropertyName(Schema.Product.Materials)] int[]? Materials,
    [property: JsonPropertyName(Schema.Product.CupVolume)] decimal? CupVolume,
    [property: JsonPropertyName(Schema.Product.Finish)] int[]? Finishes,
    [property: JsonPropertyName(Schema.Product.Pieces)] int? Pieces,
    [property: JsonPropertyName(Schema.Product.ScreenSize)] decimal? ScreenSize);

public sealed record LabelProps(
    [property: JsonPropertyName(Schema.Label.Key)] string? Key,
    [property: JsonPropertyName(Schema.Label.Text)] string? Text);

/// <summary>A product group, colour, material, finish or attribute definition: its name, and for a colour its swatch.</summary>
public sealed record NamedProps(
    [property: JsonPropertyName(Schema.Name)] string? Name,
    [property: JsonPropertyName(Schema.ColorHex)] string? Hex);

public sealed record ImageProps(
    [property: JsonPropertyName(Schema.Image.Md5)] string? Md5,
    [property: JsonPropertyName(Schema.Image.Extension)] string? Extension);

/// <summary>Rich text: XHTML in the field <c>XML</c>.</summary>
public sealed record RichText([property: JsonPropertyName("XML")] string? Xml);

/// <summary>
/// One node of a Visual Document (the page content) as the Delivery API returns it. The site renders by
/// <c>Type</c> (<c>group</c>, <c>text</c>, <c>image</c>, <c>object</c>), <c>Element</c> and <c>Class</c>.
/// </summary>
public sealed record VNode(string Type, string? Element, string? Class, string? Content, int? ObjectId, List<VNode>? Children);
