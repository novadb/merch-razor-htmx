using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using Merch.NovaDB;
using Merch.Site;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

// Numbers in the HTML (volumes, inches, counts) print the same on every machine; prices take the region's culture.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
// The platform names the port in PORT (as for every demo); ASP.NET Core's own setting is HTTP_PORTS.
if (builder.Configuration["PORT"] is { Length: > 0 } port) builder.WebHost.UseSetting(WebHostDefaults.HttpPortsKey, port);

// The Delivery login comes from the environment (or `dotnet user-secrets`) and is never logged. Without it the site does not start.
string Required(string name) => builder.Configuration[name] is { Length: > 0 } value ? value : throw new InvalidOperationException($"{name} is not set (see README, \"Run\")");
var deliveryUrl = new Uri(Required("NOVA_DELIVERY_URL"));
var login = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Required("NOVA_DELIVERY_USER")}:{Required("NOVA_DELIVERY_PASSWORD")}"));
builder.Services.AddHttpClient(Delivery.HttpClientName, client =>
{
    client.BaseAddress = deliveryUrl;
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", login);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Delivery>();
builder.Services.AddSingleton<Labels>();
builder.Services.AddSingleton<SiteTree>();
builder.Services.AddSingleton<Catalog>();
builder.Services.AddSingleton<Images>();
builder.Services.AddSingleton<Media>();

// The forms only read (filters, sorting, search) and must work from a cached page and from htmx's request: no antiforgery token.
builder.Services.AddRazorPages(options => options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));
// Umlauts and "·" stay readable in the HTML instead of becoming character references.
builder.Services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddResponseCompression();
// TLS ends at the ingress: believe its X-Forwarded-Proto and -Host, so that the request carries the public https URL.
// Remove this where the site is reached directly, without a proxy: a client could then forge those headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseExceptionHandler("/error");
app.UseStatusCodePagesWithReExecute("/error/{0}");
app.MapStaticAssets();

// `/` → the visitor's region: `/de/` for German browsers, `/uk/` for British English and its neighbours, else `/us/`.
var ukTags = new Regex(@"^en-(gb|ie|au|nz|za|in|sg)\b");
app.MapGet("/", (HttpRequest request) =>
{
    var preferred = request.Headers.AcceptLanguage.ToString().Split(',')[0].Trim().ToLowerInvariant();
    return Results.Redirect(preferred.StartsWith("de") ? "/de/" : ukTags.IsMatch(preferred) ? "/uk/" : "/us/");
});

// GET /media/{md5}/original{ext}: the file `files/{md5}{ext}`.
// GET /media/{md5}/{width}{ext}.webp: the same file scaled to one of the widths as WebP. SVG stays vector: it has no variant.
// The name keeps the extension because NovaDB serves a file only under it. Neither can change under its name.
app.MapGet("/media/{md5}/{file}", (string md5, string file, HttpContext context, Media media) =>
{
    if (Media.OriginalFile().Match(file) is { Success: true } original)
        return Media.FileName().IsMatch(md5 + original.Groups["ext"].Value) ? media.ServeOriginalAsync(context, md5, original.Groups["ext"].Value) : Task.FromResult(Results.NotFound());

    var variant = Media.VariantFile().Match(file);
    var width = Media.Widths.FirstOrDefault(w => w.ToString() == variant.Groups["width"].Value); // not int.Parse: it would also read "0400" as 400
    var ext = variant.Groups["ext"].Value;
    return variant.Success && width > 0 && ext != ".svg" && Media.FileName().IsMatch(md5 + ext) ? media.ServeScaledAsync(context, md5, ext, width) : Task.FromResult(Results.NotFound());
});

app.MapRazorPages().WithStaticAssets();
app.Run();
