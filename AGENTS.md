# AGENTS.md

For coding agents; `CLAUDE.md` only imports it. A line stays only if an agent would decide wrongly without it.

## Project

"Nova Merchandising": a demo website that shows NovaDB as CMS and PIM in one. Every page, product, price, picture and UI text is read live from NovaDB's Delivery API. Stack: ASP.NET Core 10 Razor Pages (only `.cshtml`: pages and partials), server-rendered, C#; htmx 4 in the browser; no NovaDB package (a plain `HttpClient`).

The idea of this demo is HTML over the wire the way a Razor Pages and htmx developer writes it: the server sends HTML, never JSON, and htmx swaps it; the state of a filtered view is in the URL (GET forms). Script of our own is `wwwroot/ui.js` (what htmx has no attribute for: the typeahead's keys, focus and scroll after a swap) and `wwwroot/shop.js` (Shopify), plus one-line `hx-on:*` handlers; no other script file. Not shown on purpose: Blazor, a client framework, JSON.

Agencies and developers read this code to learn how the stack works with NovaDB, then change it for their own project. Measure every change by it: easy to read (a short path from URL to HTML, no layer of our own, fewer lines); the NovaDB code in the open, one place per concern; everything else (shop, pictures, SEO, deployment) in the smallest form that is right in concept; modern, idiomatic ASP.NET Core and C#; a complete site on the live API.

The same site exists in other stacks and must look and behave identically: HTML structure, CSS classes, label keys, page URLs and the shared files in `wwwroot/` are a shared contract, checked by the shared Playwright suite. Change none of them here alone. How a request travels (GET, query string, fragment endpoints) is this stack's own.

Non-goals: client-side rendering, JSON to the browser, writes to NovaDB, a database or an external cache, a cart of our own.

## Commands and gates

- Run: `dotnet run` (http://localhost:5000). It needs the Delivery login (`NOVA_DELIVERY_URL`, `NOVA_DELIVERY_USER`, `NOVA_DELIVERY_PASSWORD`) in its environment or in `dotnet user-secrets`, and does not start without it.
- `dotnet build` (warnings are errors), `docker build .`
- Local container (`localhost:4005`, podman): `uv run scripts/refresh-container.py` after every completed change; `--full` after a change to `Merch.csproj` or `Dockerfile`.
- Tests: the shared Playwright suite (outside this repository, one project per demo), from its folder: `BASE_URL=http://localhost:4005 npx playwright test --project razor-htmx`. The tests tagged `@shopify` need the Shopify variables.
- Done means: build and `docker build` pass and the suite is green against the real Delivery API (what the README names under "Deviations" the suite checks as this demo's own variant). "Should work" is not a result; report each gate with its output.
- The suite asserts structure and data rules, not CMS copy: a missing seed path is CMS data, not a defect.
- Scripts live in `scripts/`, which `Merch.csproj` excludes from its build. Python through `uv run` with a PEP 723 block. A script that needs this project's classes (`#:project ../Merch.csproj`), or that C# plainly does better, is a C# file-based app: `dotnet run --file scripts/name.cs -- args`, never `dotnet run name.cs` (that runs the project). No quotes in `#:` directives; `#:package` needs a version.

## NovaDB

Before touching `NovaDB/`, `Site/` or `Pages/Shared/_Content.cshtml` read `NOVADB.md`: the model and the Delivery API's concepts (identifiers, `lang`, `props`, variants, references, Visual Document, files, search). The wire protocol (routes, filter and sort grammar, paging) is in the product documentation it links. `README.md` lists which file does what.

- Read-only, branch `merchDemo`.
- Only products and articles in the workflow state "released" (`Schema.WorkflowReleased`) are on the site, never an unfinished one. A product is the card, URL and page; SKU, colour, stock and delivery time come from its articles.
- Every NovaDB name (branch, types, attributes, market variants) is an API identifier and stands only in `NovaDB/Schema.cs`, as a `const` the `props` records use in `[JsonPropertyName]`. An unavoidable numeric ID goes there too, as a named constant.
- A missing `props` key means unset or untranslated: it is `null`, omit it. Never fall back to another language or to `displayName`.
- Never print or log `NOVA_DELIVERY_URL/USER/PASSWORD`; the `HttpClient` logs stay at `Warning` (`appsettings.json`) because they name the host. The login stays on the server: pictures go through the `/media` proxy.
- `NovaDB/Delivery.cs` caches responses for 60 s (`HybridCache`, in memory): after a NovaDB data change wait a minute or restart before judging output.
- A UI text is a `merchLabel` object in NovaDB: a new text is a new label there, read with `text["key"]`. The name of a product attribute is not a label: it is the name of the attribute's definition (`Labels.AttributeText`). A missing text prints `⟦key⟧` and fails the suite.

## Rules

- Fix the cause, not the symptom: no swallowed exception, fallback value or retry that hides a defect. Missing configuration or data fails loudly.
- Never weaken, skip or work around a test of the suite to get green.
- Change only what the task needs; list unrelated findings at the end of your answer (`file:line`).
- Ask before adding a package or touching `Dockerfile` or deploy configuration.
- Comments say why the code is this way (a constraint of NovaDB, the framework, browser or contract) or what a name stands for, true for a reader who never saw an earlier version. A declaration other files use gets a doc comment when name and type leave something open (a unit, what a missing value means); a file header or one comment for a group may cover several. Never what the code plainly does or how it changed: that goes in the pull request. No TODO, no commented-out code; test names likewise.

## Where the code does not say why

- Filters, sorting, "Show more" and search are GET forms: the fields are the address's query string, read by `Catalog.ParseSelection`. Without JavaScript the form submits and the answer is the page. With htmx the same GET asks for a part of it: the page handlers answer `HX-Request` with `_Range` (filters), `_MoreCards` (`?handler=more`) or `_Suggestions` (`?handler=suggest`) via `Request.IsHtmx()`, which is false for htmx's history restore (Back wants the whole page). Every page sends `Vary: HX-Request` (`RegionFilter`).
- `RegionContext` (scoped: region, texts, page tree) is filled by `RegionFilter` from the route's `{region:region}` before a handler runs; the layout and partials `@inject` it as `Site`.
- htmx 4: attributes inherit only with `:inherited`, events are `htmx:before:request` style (`hx-on::before:request`), morph swaps (`outerMorph`) are built in. Its documentation is four.htmx.org (htmx.org is version 2). A morph keeps what the visitor ticked: a swap that must reset the form uses `outerHTML`. A button that is also a submit button needs `click` among its `hx-trigger`s, else the form's own request answers the click.
- The HTML structure is a contract that every implementation of this site shares, plus what htmx needs (README, "Deviations"). Razor prints the whitespace of the template: where content flows inline (a price and its badge, a facet option), keep the elements on one line.
- `wwwroot/site.css`, `NOVADB.md` and the other shared files in `wwwroot/` (`favicon.svg`, `novadb.svg`, `github.svg`, `robots.txt`, the fonts) are shared files, kept identical from outside this repository: never adapt them. `site.css` is also the CMS editor's stylesheet. The shared client script `site.js` is not used here: `ui.js` and `shop.js` are this demo's own.
- Page content is made from a closed set of components (classes `merch*`): compose, or follow the structure with CSS, before asking for a new one.
- The shop is Shopify's own Buy Button and cart, started in `shop.js` from a product's `data-shopify-product`; its texts are `data-*` attributes of `<body>`. Without `SHOPIFY_DOMAIN` and `SHOPIFY_STOREFRONT_TOKEN` there are no buy buttons and no cart, silently: keep both in `.env`.

## Git

- Branch from a fresh `origin/main` as `feature/<id>-<slug>` or `bugfix/<id>-<slug>` (`<id>` is the number of the work item, the slug lower-case words). `main` changes only by pull request, completed as **squash merge** with the source branch deleted; direct and force pushes to `main` are rejected.
- **Title and description of the PR are the one public commit message.** Write them with the skill `pr-text` (title up to 60 characters, description optional, up to three lines), check them with `scripts/check-pr-text.py`, get the user's approval, and set the merge commit message explicitly to that text.
- The PR needs a review, a linked work item, resolved comments and a green build.
- Everything on `main` is public and mirrored read-only to GitHub (never push there): no addresses, accounts, tracker numbers or secrets in files or commit messages. No tags, no version numbers.
- Pushing, merging and tagging need explicit approval.

## Deployment

The site is one container (`Dockerfile`, port 3000 via `PORT`) with no state of its own: the media cache (`MEDIA_CACHE_DIR`) refills by itself. Where and how it is hosted is the operator's choice. It only needs its environment (see NovaDB) from the platform's secrets, with `SITE_URL` set to the public origin. Never push images, deploy or change DNS without explicit approval.
