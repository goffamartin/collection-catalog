---
name: public-ui
description: Writing pages of the public website of Collection Catalog (static SSR) – layout, museum-like design, URLs and filters in the query string, languages and hreflang, SEO/OpenGraph, gallery, public photos and protecting private data. Use for any change in Components/Public, Components/Layout, Services/Public or wwwroot/js/site.js.
---

# Public website

## Page skeleton
```razor
@page "/something"
@inject PublicItemQuery Query

@if (_data is null)
{
    return;   // SSR renders the page once before OnInitializedAsync finishes
}

<Seo Title="@T["Something"]" Path="something" Description="…" ImageKey="@photo?.FileKey" />

<div class="site-container pt-10 sm:pt-14">
    <p class="eyebrow">@T["Eyebrow"]</p>
    <h1 class="display-title mt-3 text-4xl sm:text-5xl">@T["Heading"]</h1>
    <div class="gold-rule mt-6"></div>
    …
</div>
```
- **No `@rendermode`** – public pages are static SSR (fast, SEO). Pages in `Components/Public/Pages`, shared
  components in `Components/Public/Shared`, layout `Components/Layout/MainLayout.razor`.
- Add navigation to `MainLayout` (desktop nav, mobile `<details>` menu, footer) and to the sitemap in
  `Infrastructure/SeoEndpoints.cs`.

## Data and privacy
- Data only through `PublicItemQuery` → DTOs in `Services/Public/PublicDtos.cs`. Never entities, never `AppDbContext`
  in a page.
- A new DTO field: check it is not private (prices, seller, provenance, notes). `PublicDtoTests` guards names,
  `PublicSiteTests` the HTML output.
- Only `Visibility == Public` items (collection) or items with an Active/Reserved offer (offers section) and `IsPublic`
  photos. Photos via `PublicUrls.Image(key, 400|800|1600)` / `PublicUrls.ImageJpeg(key)` (OpenGraph).
- Item URL always `PublicUrls.Item(id, name)` (`sbirka/{id}/{slug}`, slug from the name in the current language); the
  detail redirects to the current slug.
- Website texts from `SiteSettingsService.GetAsync()` (`SiteInfo`, already in the current language), multi-line text
  via `<TextBlock Text=… />`.

## Languages
- Relative links (`href="sbirka"`) stay in the current language thanks to `<base href>` – never hard-code `/` paths
  for pages.
- `<Seo>` writes canonical + `hreflang` alternates; when the path differs per language (translated slug), pass
  `PathIn="language => …"`.
- The language switcher (`Components/Layout/LanguageSwitcher.razor`) links with a full page load.

## Design (`Styles/app.css`)
- Background `bg-paper` (#f7f5f0), text `stone`, accent `amber-700/800`, headings `font-display` (Fraunces,
  self-hosted in `wwwroot/fonts`).
- Classes: `site-container`, `eyebrow`, `display-title`, `gold-rule`, `photo-well`, `text-link`.
- Photos in cards and the detail always through `PhotoFrame` (uniform ratio with a blurred fill).
- Components: `PhotoFrame`, `ItemCard`, `OfferCard`, `ItemShowcase`, `InquiryForm`, `BrowseChips`, `FacetSelect`,
  `PageLink`, `DetailRow`, `TextBlock`, `Seo`, `JsonLd`, `Breadcrumbs`; plurals via `T.Plural(...)`.

## Forms
- Only `InquiryForm`. Static SSR `EditForm` with `FormName`, `Enhance`, POST → `NavigateTo(…?odeslano=1#dotaz)` (PRG).
- `[SupplyParameterFromForm(FormName = "inquiry", Name = "Form")]` – `Name` must match the expressions in markup.
- Anti-spam: hidden field `Website` (filled = pretend success, store nothing) + `InquiryRateLimiter`. Consent is
  required (link to the privacy page).

## SEO and semantics
- Every page: `<Seo Title Path Description ImageKey NoIndex>`; only one `HeadContent` per page.
- Structured data: `<JsonLd Data="StructuredData.…" />` – `WebSite` on home, `<Breadcrumbs>` (renders the `<ol>` and
  `BreadcrumbList`), offers with a price `Product` + `Offer`.
- Sitemap includes every language and public photos; robots.txt and `X-Robots-Tag` keep `/admin`, `/prihlaseni`,
  `/ucet` (under every language prefix) out of search engines.
- One `h1` per page, sections `h2`, cards `h3`; images with `alt`, decorative ones `alt=""`.

## Filters and URLs
- Filters = a plain `<form method="get" data-autosubmit>`; parameters as `string?`, parsed with `QueryValue`
  (a hand-edited URL must not crash the page). Filtered listings are `NoIndex`.

## JavaScript
- Only `wwwroot/js/site.js`, everything via **delegation on `document`** (enhanced navigation swaps the DOM without
  running scripts). The page must make sense without JS.

## Verification
- `verify-in-browser` with demo data (`seed-demo`), desktop 1440 px and mobile 390 px, no horizontal overflow, clean
  console, both languages.
