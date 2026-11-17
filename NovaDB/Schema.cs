using System.Text.Json.Serialization;

namespace Merch.NovaDB;

/// <summary>
/// Every NovaDB name the site depends on, in one place. IDs of branches and object types differ from installation to
/// installation, API identifiers do not, so the code addresses everything by identifier. The site's own identifiers
/// carry the prefix <c>merch</c> because they must be unique across branches; NovaDB's own (<c>typeImage</c>,
/// <c>typeAttribute</c>, <c>attributeName</c>, the file attributes) do not.
/// The Delivery API serves an identifier with a lower-case first letter. A numeric ID belongs here too, as a named
/// constant with a comment, where one is unavoidable (<see cref="WorkflowReleased"/>).
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
        /// <summary>A product's sellable variant (SKU, colour, stock, delivery time).</summary>
        public const string Article = "merchArticle";
        public const string Group = "merchProductGroup";
        public const string Image = "typeImage";
        public const string Label = "merchLabel";
        public const string Color = "merchColor";
        public const string Material = "merchMaterial";
        public const string Finish = "merchFinish";
        public const string Claim = "merchClaim";
        /// <summary>The rows of a set product, an anonymous type: only reachable through <see cref="Schema.Product.SetItems"/>.</summary>
        public const string SetItem = "merchSetItem";
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

    /// <summary>
    /// Attributes of a product. A product is what the site shows (page, card, price, Shopify ID); its articles carry SKU,
    /// colour and stock. Materials and finishes reference <c>merchMaterial</c>, <c>merchFinish</c> objects.
    /// </summary>
    public static class Product
    {
        public const string Name = "merchProductName";
        public const string Description = "merchProductDescription";
        public const string Number = "merchProductNumber";
        /// <summary>Reference to the product group (the parent in the group tree).</summary>
        public const string Group = "merchProductParent";
        /// <summary>One price per market (<see cref="MarketVariant"/>).</summary>
        public const string Price = "merchProductSalesPrice";
        public const string Images = "merchProductImages";
        public const string Related = "merchProductRelated";
        public const string Accessories = "merchProductAccessories";
        public const string SpareParts = "merchProductSpareParts";
        public const string UrlSegment = "merchProductUrlSegment";
        public const string ShopifyId = "merchProductShopifyId";
        /// <summary>References to <c>merchClaim</c> objects: statements the product may make.</summary>
        public const string Claims = "merchProductClaims";
        /// <summary>References to <c>merchSetItem</c> objects, set only on a set product.</summary>
        public const string SetItems = "merchProductSetItems";
        public const string Materials = "merchProductMaterials";
        public const string Finishes = "merchProductFinishes";
        /// <summary>Volume in cm³ (a cup's, printed as ml), pieces per set, screen size in inches.</summary>
        public const string Volume = "merchProductVolume";
        public const string Pieces = "merchProductPiecesPerSet";
        public const string ScreenSize = "merchProductScreenSize";
        public const string SeoTitle = "merchSeoTitle";
        public const string SeoDescription = "merchSeoDescription";
        public const string SeoNoIndex = "merchSeoNoIndex";
        public const string SeoNoFollow = "merchSeoNoFollow";
        /// <summary>Reference to a workflow state (see <see cref="WorkflowReleased"/>).</summary>
        public const string Workflow = "merchProductWorkflow";
    }

    /// <summary>Attributes of an article, the sellable variant of a product.</summary>
    public static class Article
    {
        /// <summary>The SKU.</summary>
        public const string Number = "merchArticleNumber";
        public const string Name = "merchArticleName";
        public const string Product = "merchArticleParent";
        /// <summary>Reference to a <c>merchColor</c> object.</summary>
        public const string Color = "merchArticleColor";
        /// <summary>Pieces in stock; 0 is sold out.</summary>
        public const string Stock = "merchArticleStock";
        public const string DeliveryDays = "merchArticleDeliveryDays";
        public const string Workflow = "merchArticleWorkflow";
    }

    public static class Claim
    {
        public const string Text = "merchClaimText";
    }

    public static class SetItem
    {
        /// <summary>Reference to the article the row is made of.</summary>
        public const string Article = "merchSetItemArticle";
        public const string Quantity = "merchSetItemQuantity";
    }

    /// <summary>
    /// The workflow state "released": only products and articles in it are on the site, the rest is still being edited or
    /// checked. A reference attribute is filtered by the target's object ID, and the state objects have no API identifier.
    /// The ID is the same in every branch of this installation.
    /// </summary>
    public const int WorkflowReleased = 2108321;

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
    public static readonly string[] ProductSearchFields = [Product.Name, Product.Number, Product.Description];
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
    [property: JsonPropertyName(Schema.Product.Group)] int? Group,
    [property: JsonPropertyName(Schema.Product.Price)] decimal? Price,
    [property: JsonPropertyName(Schema.Product.Images)] int[]? Images,
    [property: JsonPropertyName(Schema.Product.Related)] int[]? Related,
    [property: JsonPropertyName(Schema.Product.Accessories)] int[]? Accessories,
    [property: JsonPropertyName(Schema.Product.SpareParts)] int[]? SpareParts,
    [property: JsonPropertyName(Schema.Product.UrlSegment)] string? UrlSegment,
    [property: JsonPropertyName(Schema.Product.ShopifyId)] string? ShopifyId,
    [property: JsonPropertyName(Schema.Product.Claims)] int[]? Claims,
    [property: JsonPropertyName(Schema.Product.SetItems)] int[]? SetItems,
    [property: JsonPropertyName(Schema.Product.Materials)] int[]? Materials,
    [property: JsonPropertyName(Schema.Product.Finishes)] int[]? Finishes,
    [property: JsonPropertyName(Schema.Product.Volume)] decimal? Volume,
    [property: JsonPropertyName(Schema.Product.Pieces)] int? Pieces,
    [property: JsonPropertyName(Schema.Product.ScreenSize)] decimal? ScreenSize,
    [property: JsonPropertyName(Schema.Product.SeoTitle)] string? SeoTitle,
    [property: JsonPropertyName(Schema.Product.SeoDescription)] string? SeoDescription,
    [property: JsonPropertyName(Schema.Product.SeoNoIndex)] bool? SeoNoIndex,
    [property: JsonPropertyName(Schema.Product.SeoNoFollow)] bool? SeoNoFollow);

public sealed record ArticleProps(
    [property: JsonPropertyName(Schema.Article.Number)] string? Number,
    [property: JsonPropertyName(Schema.Article.Name)] string? Name,
    [property: JsonPropertyName(Schema.Article.Product)] int? Product,
    [property: JsonPropertyName(Schema.Article.Color)] int? Color,
    [property: JsonPropertyName(Schema.Article.Stock)] int? Stock,
    [property: JsonPropertyName(Schema.Article.DeliveryDays)] int? DeliveryDays);

public sealed record ClaimProps([property: JsonPropertyName(Schema.Claim.Text)] string? Text);

public sealed record SetItemProps(
    [property: JsonPropertyName(Schema.SetItem.Article)] int? Article,
    [property: JsonPropertyName(Schema.SetItem.Quantity)] int? Quantity);

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
