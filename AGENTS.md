# AGENTS.md

Single source of truth for coding agents in this repository; `CLAUDE.md` only imports it. A line stays only if an agent would decide wrongly without it. Why a piece of code is the way it is belongs in a comment at that code, not here.

## Project

"Nova Merchandising": a demo website that shows NovaDB as CMS and PIM in one. Every page, product, price, picture and UI text is read live from NovaDB's Delivery API. Stack: ASP.NET Core 10 Razor Pages (only `.cshtml`: pages and partials), server-rendered, C#; htmx 4 in the browser instead of a script of our own; no Blazor runtime, no client framework, no NovaDB package (a plain `HttpClient`).

The idea of this demo is as little JavaScript as possible: behaviour is an `hx-*` attribute and HTML from the server. Script of our own is only `wwwroot/shop.js` (Shopify) and short `hx-on:*` handlers where htmx has no attribute; never add a script file for what an attribute or the server can do.

Agencies and developers read this code to learn how the stack works with NovaDB, then change it for their own project. Measure every change against that: easy to read (a short path from URL to HTML, no layer of our own, fewer lines); the NovaDB code in the open, one place per concern; everything else (shop, pictures, SEO, deployment) in the smallest form that is right in concept; modern, idiomatic ASP.NET Core and C#; a complete site on the live API.

The same site exists in other stacks and must look and behave identically: HTML structure, CSS classes, label keys, routes and the files in `wwwroot/` are a shared contract, checked by the shared Playwright suite. Change none of them here alone.

Non-goals: client-side rendering, writes to NovaDB, a database or shared cache, query strings, a cart of our own.

## Commands and gates

- Run: `dotnet run` (http://localhost:5000). It needs the Delivery login (`NOVA_DELIVERY_URL`, `NOVA_DELIVERY_USER`, `NOVA_DELIVERY_PASSWORD`) in its environment or in `dotnet user-secrets`, and does not start without it.
- `dotnet build` (warnings are errors), `docker build .`
- Local container (`localhost:4012`, podman): `uv run scripts/refresh-container.py` after every completed change; `--full` after a change to `Merch.csproj` or `Dockerfile`.
- Tests: the shared Playwright suite, from its checkout: `BASE_URL=http://localhost:4012 npx playwright test` (Firefox). The tests tagged `@shopify` need the Shopify variables.
- Done means: build and `docker build` pass and the suite is green against the real Delivery API, but for the tests the README names under "Deviations". "Should work" is not a result; report each gate with its output.
- The suite asserts structure and data rules, not CMS copy: a missing seed path is CMS data, not a defect of the app.
- Scripts live in `scripts/`, which `Merch.csproj` excludes from its build. Python through `uv run` with a PEP 723 block. A script that needs this project's classes (`#:project ../Merch.csproj`), or that C# plainly does better, is a C# file-based app: `dotnet run --file scripts/name.cs -- args`, never `dotnet run name.cs` (that runs the project). No quotes in `#:` directives; `#:package` needs a version.

## NovaDB

Before touching `NovaDB/`, `Site/` or `Pages/Shared/_Content.cshtml` read `NOVADB.md`: the model and the Delivery API's concepts (identifiers, `lang`, `props`, variants, references, Visual Document, files, search). The wire protocol (routes, filter and sort grammar, paging) is in the product documentation it links. `README.md` lists which file does what.

- Read-only, branch `merchDemo`.
- Every NovaDB name (branch, types, attributes, market variants) is an API identifier and stands only in `NovaDB/Schema.cs`, as a `const` the `props` records use in `[JsonPropertyName]`. An unavoidable numeric ID goes there too, as a named constant.
- A missing `props` key means unset or untranslated: it is `null`, omit it. Never fall back to another language or to `displayName`.
- Never print or log `NOVA_DELIVERY_URL/USER/PASSWORD`; the `HttpClient` logs stay at `Warning` (`appsettings.json`) because they name the host. The login stays on the server: pictures go through the `/media` proxy.
- `NovaDB/Delivery.cs` caches responses for 60 s: after a NovaDB data change wait a minute or restart before judging output.
- A UI text is a `merchLabel` object in NovaDB: a new text is a new label there, read with `text["key"]`. The name of a product attribute is not a label: it is the name of the attribute's definition (`Labels.AttributeText`). A missing text prints `⟦key⟧` and fails the suite.

## Rules

- Fix the cause, not the symptom: no swallowed exception, fallback value or retry that hides a defect. Missing configuration or data fails loudly.
- Never weaken, skip or work around a test of the suite to get green.
- Change only what the task needs; list unrelated findings at the end of your answer (`file:line`).
- Ask before adding a package or touching `Dockerfile` or deploy configuration.

## Where the code does not say why

- No `@addTagHelper *`: the form tag helper would add an antiforgery field and handler URLs. Only the link and script tag helpers are imported (`_Layout.cshtml`, for the fingerprinted asset names). Antiforgery is off for every page: the forms only read.
- The site writes no query string and reads none (a foreign one is ignored): filters, sorting and search are POST forms answered with the page itself (`OnPostAsync` = `OnGetAsync`), so a filtered view has no URL of its own. That is intended. htmx sends the same POST and picks `#range` from the whole page (`hx-select`): the pages have no htmx branch, keep it that way.
- htmx 4: attributes inherit only with `:inherited`, events are `htmx:before:request` style (`hx-on::before:request`), morph swaps (`outerMorph`) are built in. Its documentation is four.htmx.org, not htmx.org (htmx 2). A morph keeps what the visitor ticked: a swap that must reset the form uses `outerHTML`.
- The HTML must match the other demos' DOM, plus what htmx needs (README, "Deviations"). Razor prints the whitespace of the template: where content flows inline (a price and its badge, a facet option), keep the elements on one line.
- `wwwroot/site.css`, `NOVADB.md` and the other shared files in `wwwroot/` (`favicon.svg`, `novadb.svg`, `github.svg`, `robots.txt`, the fonts) are the same files in every demo, kept identical from outside this repository: never adapt them. `site.css` is also the CMS editor's stylesheet. The other demos' `site.js` is not used here on purpose.
- Page content is made from a closed set of components (classes `merch*`): compose, or follow the structure with CSS, before asking for a new one.
- The shop is Shopify's own Buy Button and cart, started in `shop.js` from a product's `data-shopify-product`; `shop.js` is the Shopify part of the other demos' `site.js`, unchanged: change it only together with them. Without `SHOPIFY_DOMAIN` and `SHOPIFY_STOREFRONT_TOKEN` there are no buy buttons and no cart, silently: keep both in `.env`.

## Git

- Branch from a fresh `origin/main` as `feature/<id>-<slug>` or `bugfix/<id>-<slug>` (`<id>` is the number of the work item, the slug lower-case words). `main` changes only by pull request, completed as **squash merge** with the source branch deleted; direct and force pushes to `main` are rejected.
- **Title and description of the PR are the one public commit message.** Write them with the skill `pr-text` (title up to 60 characters, description optional, up to three lines), check them with `scripts/check-pr-text.py`, get the user's approval, and set the merge commit message explicitly to that text.
- The PR needs a review, a linked work item, resolved comments and a green build.
- Everything on `main` is public and mirrored read-only to GitHub (never push there): no addresses, accounts, tracker numbers or secrets in files or commit messages. No tags, no version numbers.
- Pushing, merging and tagging need explicit approval.

## Deployment

The site is one container (`Dockerfile`, port 3000 via `PORT`) with no state of its own: the media cache (`MEDIA_CACHE_DIR`) refills by itself. Where and how it is hosted is the operator's choice. It only needs its environment (see NovaDB) from the platform's secrets, with `SITE_URL` set to the public origin. Never push images, deploy or change DNS without explicit approval.
