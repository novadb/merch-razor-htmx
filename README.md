# Nova Merchandising — ASP.NET Core Razor Pages + htmx

The demo site of NovaDB as CMS and PIM in one, built with ASP.NET Core 10 Razor Pages (pages and partials, `.cshtml`
only) and htmx instead of a script of its own: server-rendered, no Blazor runtime, no client framework. Every page, product, price,
picture and UI text is read live from NovaDB's Delivery API (branch `merchDemo`) with a plain `HttpClient`; there is
no NovaDB package in between.

Everything works without JavaScript: filters, sorting and search are POST forms answered with the page. htmx 4
(`wwwroot/htmx.min.js`, attributes `hx-*` in the HTML) sends the same POSTs in the background and puts the answer in
place: the range is morphed (cards, focus and scroll position stay), more cards come while scrolling, and the typeahead
gets its list as HTML from `POST /api/suggest`. The only script of the site is `wwwroot/shop.js`: Shopify's own Buy
Button and cart in the site's colours. A few lines in `hx-on:*` attributes handle what htmx does not: the keys of the
typeahead's combobox, `aria-busy` while the range loads, and showing the range from its start when it shrank.

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

Container: `uv run scripts/refresh-container.py --full` builds the `Dockerfile` and runs it at http://localhost:4012
with the variables from the environment or `.env` (see `.env.example`).

## Test

The site passes the shared Playwright behaviour suite of the Nova Merchandising demos (Firefox) but for two tests (see
Deviations): run it from its checkout with `BASE_URL=http://localhost:4012 npx playwright test`. The tests tagged
`@shopify` (buy button, cart) need the Shopify variables.

## Where things are

| Path | What |
|---|---|
| `NOVADB.md` | how the site reads NovaDB: the model, the Delivery API's concepts, what the site decided |
| `NovaDB/Schema.cs` | every NovaDB name (branch, types, attributes, market variants) as API identifiers, and the `props` records: the one place to change |
| `NovaDB/Delivery.cs` | Delivery API client on `HttpClient` (Basic auth set in `Program.cs`): paging, 60 s cache |
| `Site/Regions.cs`, `Site/Labels.cs` | regions (`de`, `uk`, `us`: content language, market, currency) and the UI texts, read from NovaDB's labels |
| `Site/SiteTree.cs` | page tree, URLs from `merchPageUrlSegment` + parent page, breadcrumb, category page of a group |
| `Site/Catalog.cs` | products, full-text filter, facets (counted on the server), sorting |
| `Site/Images.cs`, `Site/Media.cs` | picture URLs by content address; the `/media` proxy with disk cache and scaled WebP variants (SkiaSharp) |
| `Site/Seo.cs` | canonical and hreflang URLs |
| `Program.cs` | services, middleware, `/` → region by `Accept-Language`, `/media/…` |
| `Pages/Show.cshtml` | `/{region}/…`: a page (content + live range) or a product |
| `Pages/Search.cshtml`, `Pages/Error.cshtml` | search page; 404 and error page |
| `Pages/Suggest.cshtml` | `POST /api/suggest`: the typeahead's list as HTML, without layout |
| `Pages/RegionPageModel.cs` | base of every page: the region, its texts, the page tree and the posted form |
| `Pages/ContentContext.cs` | what a document is rendered with: the IDs it refers to (objects, links, images) and the link rewriting |
| `Pages/Shared/_Layout.cshtml` | document shell, header, region switch, footer |
| `Pages/Shared/_Content.cshtml` | Visual Document renderer and page teaser |
| `Pages/Shared/_Range.cshtml` | the live range with its facets, and its htmx attributes |
| `Pages/Shared/_ProductCard.cshtml`, `_Crumbs.cshtml` | product card; breadcrumb |
| `Pages/Shared/_SearchBox.cshtml` | search form with the typeahead (its keys in `hx-on:*`) |
| `wwwroot/` | `site.css`, fonts, icons, `robots.txt`: the same files in every demo; `htmx.min.js` (4.0.0, its licence beside it); `shop.js`; `MapStaticAssets` serves CSS and JS under a fingerprinted name |

## Deviations

- No `site.js`: the behaviour it adds in the other demos is htmx here, and `shop.js` is its Shopify part, unchanged.
- The DOM carries what htmx needs: `hx-*` attributes, `data-htmx-powered` on the elements htmx wired up, an `id`
  per card in the range (`card-{object ID}`: the morph matches cards by it), and the typeahead's list (`ul.suggest`)
  and ARIA attributes are in the HTML from the start instead of being added by a script.
- A card that left the range (filtered out) and comes back is a new element; its picture comes from the browser cache.
  `site.js` keeps every card it has seen for that, which takes more script than this demo wants. The suite's test "a
  facet or sort change replaces the range in place" does not judge it.
- The pictures of the cards are not lazy (`loading="lazy"` is missing): a card that comes back after a filter is a new
  element, and a lazy picture counts as loaded only after the browser has looked at it, so it could show late, which the
  suite checks. A range shows 12 cards at a time, so loading them at once costs little.
- Reset replaces the range instead of morphing it: a morph keeps what the visitor ticked.
- `POST /api/suggest` answers HTML (the typeahead's list, with the extra field `list` as its ID), never JSON: the
  browser gets nothing but HTML from this site. The suite's one test that reads the JSON answer, tagged `@suggest-json`,
  is left out of this demo's pull request pipeline (`--grep-invert`); the `/media` routes are checked as in every demo.
- Scaled pictures (`/media/{md5}/{width}{ext}.webp`) are made with SkiaSharp: their bytes differ slightly from other demos' (resampling at
  high-contrast edges), and an animated GIF or WebP is scaled to its first frame.
