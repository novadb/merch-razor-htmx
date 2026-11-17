# Nova Merchandising — ASP.NET Core Razor Pages + htmx

**Live:** <https://razor-htmx.merch.novadb.com/>: this repository's `main`, running against NovaDB. `/` picks the
region by browser language (`/de/`, `/uk/`, `/us/`); the checkout is in Shopify's test mode.

The same shop in other stacks: React Router ([live](https://react-router.merch.novadb.com/),
[source](https://github.com/novadb/merch-react-router)) · ASP.NET Core Blazor
([live](https://blazor-dotnet.merch.novadb.com/), [source](https://github.com/novadb/merch-blazor-dotnet)) · Nuxt
([live](https://vue-nuxt.merch.novadb.com/), [source](https://github.com/novadb/merch-vue-nuxt)) · Astro
([live](https://astro.merch.novadb.com/), [source](https://github.com/novadb/merch-astro))

The demo site of NovaDB as CMS and PIM in one, built with ASP.NET Core 10 Razor Pages (pages and partials, `.cshtml`
only) and htmx: server-rendered, HTML over the wire, no Blazor runtime, no client framework. Every page, product, price,
picture and UI text is read live from NovaDB's Delivery API (branch `merchDemo`) with a plain `HttpClient`; there is
no NovaDB package in between.

## What this demo shows, and what it does not

It shows **hypermedia**: the server sends HTML and decides what the browser swaps, htmx puts it in place, and the state
of a filtered view is the address. It does not use Blazor, a client component framework or JSON to the browser: a page
handler picks the fragment (`_Range`, `_MoreCards`, `_Suggestions`) and answers it as a Razor partial.

Everything works without JavaScript: filters, sorting, "Show more" and search are GET forms, their fields are the
address (a filtered view can be shared, Back returns to the one before). htmx 4 (`wwwroot/htmx.min.js`, attributes
`hx-*` in the HTML) sends the same GET in the background; the page answers with the part it is asked for and htmx puts it
in place: the range is morphed (cards, focus and scroll position stay), "Show more" appends the next 12 cards while
scrolling, and the typeahead's list comes from the search page (`?handler=suggest`). The server sends HTML only, never
JSON. Two small scripts: `wwwroot/ui.js` for what htmx has no attribute for (the keys of the typeahead's combobox, focus
and scroll position after a swap) and `wwwroot/shop.js` for Shopify's own Buy Button and cart in the site's colours.

## Run

```bash
dotnet run           # http://localhost:5000
```

It needs the Delivery API login of your NovaDB instance: `NOVA_DELIVERY_URL`, `NOVA_DELIVERY_USER` and
`NOVA_DELIVERY_PASSWORD`, from the environment or from `dotnet user-secrets set NOVA_DELIVERY_URL https://…` (the same
for the other two). Without them the site does not start. Optional: `DEMO_BRANCH` (default `merchDemo`),
`SHOPIFY_DOMAIN` and `SHOPIFY_STOREFRONT_TOKEN` (public Buy Button token; without them there is no cart), `SITE_URL`
(public origin for the absolute canonical and hreflang URLs; default: the ingress's `X-Forwarded-*` headers, which a
client can forge, so set it in production), `PORT`, `MEDIA_CACHE_DIR`.

Container: `uv run scripts/refresh-container.py --full` builds the `Dockerfile` and runs it at http://localhost:4005
with the variables from the environment or `.env` (see `.env.example`).

## Test

The site passes the shared Playwright behaviour suite of the Nova Merchandising demos (not part of this repository, one
project per demo), which checks the deviations below as this demo's own variant: run it from its folder with
`BASE_URL=http://localhost:4005 npx playwright test --project razor-htmx`. The tests tagged `@shopify` (buy button, cart)
need the Shopify variables.

## Where things are

| Path | What |
|---|---|
| `NOVADB.md` | how the site reads NovaDB: the model, the Delivery API's concepts, what the site decided |
| `NovaDB/Schema.cs` | every NovaDB name (branch, types, attributes, market variants) as API identifiers, and the `props` records: the one place to change |
| `NovaDB/Delivery.cs` | Delivery API client on `HttpClient` (login and branch as options, checked at start): paging, 60 s `HybridCache` |
| `Site/Regions.cs`, `Site/Labels.cs` | regions (`de`, `uk`, `us`: content language, market, currency; the `{region:region}` route constraint) and the UI texts, read from NovaDB's labels |
| `Site/SiteTree.cs` | page tree, URLs from `merchPageUrlSegment` + parent page, breadcrumb, category page of a group |
| `Site/Catalog.cs` | the product and article types, full-text filter, facets (counted on the server), sorting; reading released products with their articles, claims and set rows |
| `Site/Images.cs`, `Site/Media.cs` | picture URLs by content address; the `/media` proxy with disk cache and scaled WebP variants (SkiaSharp) |
| `Site/Seo.cs` | canonical and hreflang URLs, description and robots hint |
| `Program.cs` | services, middleware, `/` → region by `Accept-Language`, `/media/…` |
| `Pages/RegionContext.cs` | the region of a request with its texts and page tree, filled before every page's handler; `Request.IsHtmx()` |
| `Pages/Show.cshtml` | `/{region}/…`: a page (content + live range) or a product |
| `Pages/Search.cshtml` | search page; its handlers `more` ("Show more") and `suggest` (the typeahead's list) |
| `Pages/Error.cshtml` | 404 and error page |
| `Pages/ContentContext.cs`, `Pages/Views.cs` | what a document is rendered with (the IDs it refers to, link rewriting); the records the partials take |
| `Pages/Shared/_Layout.cshtml` | document shell, header, region switch, footer |
| `Pages/Shared/_Content.cshtml` | Visual Document renderer and page teaser |
| `Pages/Shared/_ProductPage.cshtml` | a product's page |
| `Pages/Shared/_Range.cshtml`, `_ShowMore.cshtml`, `_MoreCards.cshtml` | the live range with its facets and its htmx attributes; the button for the next batch; the next batch as htmx appends it |
| `Pages/Shared/_ProductCard.cshtml`, `_Crumbs.cshtml` | product card; breadcrumb |
| `Pages/Shared/_SearchBox.cshtml`, `_Suggestions.cshtml` | search form with the typeahead; the typeahead's list |
| `wwwroot/` | `site.css`, fonts, icons, `robots.txt`: the same files in every demo; `htmx.min.js` (4.0.0, its licence beside it); `ui.js`; `shop.js`; `MapStaticAssets` serves CSS and JS under a fingerprinted name |

## Deviations

- No `site.js`: the behaviour it adds in the other demos is htmx and `wwwroot/ui.js` here, and `wwwroot/shop.js` is its Shopify part.
- The DOM carries what htmx needs: `hx-*` attributes, `data-htmx-powered` on the elements htmx wired up, an `id`
  per card in a range (`card-{object ID}`: the morph matches cards by it), and the typeahead's list (`ul.suggest`)
  and ARIA attributes are in the HTML from the start instead of being added by a script.
- A card that left the range (filtered out) and comes back is a new element; its picture comes from the browser cache.
  `site.js` keeps every card it has seen for that, which takes more script than this demo wants. The suite's test "a
  facet or sort change replaces the range in place" does not judge it.
- The pictures of the cards are not lazy (`loading="lazy"` is missing): a card that comes back after a filter is a new
  element, and a lazy picture counts as loaded only after the browser has looked at it, so it could show late, which the
  suite checks (it expects exactly this here). A range shows 12 cards at a time, so loading them at once costs little.
- Reset replaces the range instead of morphing it: a morph keeps what the visitor ticked.
- The forms are GET forms: a filtered view has an address of its own, and the typeahead's list is the HTML of
  `/{region}/search?handler=suggest` (the suite reads its options where it reads the JSON in other demos).
- Scaled pictures (`/media/{md5}/{width}{ext}.webp`) are made with SkiaSharp: their bytes differ slightly from other demos' (resampling at
  high-contrast edges), and an animated GIF or WebP is scaled to its first frame.
