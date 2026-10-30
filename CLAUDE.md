# CLAUDE.md

Collection Catalog – a private **inventory** (administration) + public **catalog** + **offers** with inquiries for a
collection. Overview, features and configuration: `README.md`.

- Commits: English, short and descriptive.
- Before larger changes, read the relevant skill in `.claude/skills/`.

## Stack and structure
- ASP.NET Core 10, Blazor (static SSR + Interactive Server), EF Core + **SQLite**, Tailwind CSS 4, SkiaSharp, MSTest.
- One project `src/CollectionCatalog.Web` with feature folders – no extra layers without a reason:
  `Domain/` entities and enums · `Data/` DbContext, configurations, migrations · `Services/` (Items, Images, Codebooks,
  Languages, Users, Text, Public, Site, Listings, Inquiries, Demo) · `Infrastructure/` storage, auth, CLI, endpoints
  (media, SEO), language middleware · `Components/Public|Admin|Account|Pages|Layout|Shared` · `Resources/` UI texts ·
  `Styles/app.css`.
- Tests `tests/CollectionCatalog.Tests` – services over a temporary SQLite database (`TestEnvironment`), end-to-end
  through `WebApplicationFactory` (`TestWebApp`).

## Architecture rules
- Public pages = static SSR (no `@rendermode`); administration = `@rendermode InteractiveServer` (pages in
  `Components/Admin/Pages`).
- `@layout AdminLayout` and `[Authorize]` live in `Components/Admin/Pages/_Imports.razor` – **not** one level up
  (AdminLayout would become its own layout → infinite recursion, the request hangs).
- Interactive components and their services: `IDbContextFactory<AppDbContext>` (a context per operation), never a
  scoped DbContext for the whole circuit.
- The public part reads only through `PublicItemQuery` → DTOs in `Services/Public/PublicDtos.cs` (guarded by
  `PublicDtoTests`). Private fields (purchase/estimated prices, seller, private notes, provenance) must never leak.
  Public photos only through `/media/{key}/{size}` (checks the photo and the item are public).
- An item is public when `Visibility == Public` (collection) **or** it has an Active/Reserved offer (offers section
  only) – `PublicItemQuery.PublicOfferStatuses`. Offers only through `ListingService` (at most one open offer per item;
  "Sold" records the sale on the item), inquiries through `InquiryService` (public `SubmitAsync` checks availability).
- Public forms: static SSR `EditForm` + `[SupplyParameterFromForm(Name = …)]` matching the expressions in markup,
  Post/Redirect/Get (`?odeslano=1`), honeypot field `Website` + `InquiryRateLimiter` (5 / h / IP). Pattern:
  `Components/Public/Shared/InquiryForm.razor`.
- Website texts (`SiteSettings`, one row) through the singleton `SiteSettingsService` with a cache – change them only
  through its `SaveAsync`. Public address `Site:BaseUrl`.
- Schema only through migrations, never `EnsureCreated`. Enums stored as text (convention in `AppDbContext`); decimal
  precision 18,2; times in UTC `DateTime`, purchase/sale dates `DateOnly`; audit times set by `AuditInterceptor`.
  Pending migrations make the app back up the database first (`VACUUM INTO` → `DataPath/backups/`).
- Item types, categories, countries = codebooks via `CodebookService`; deleting an entry keeps items (FK SET NULL,
  category join cascade).
- Item edits only through `ItemEditorService` (data + categories with the Save button; photos are saved immediately).
- Photos only through `ImageStore` (SkiaSharp): EXIF orientation, metadata stripped, master JPEG + WebP 400/800/1600,
  file name = content hash. A file can be shared by several rows → delete only unreferenced files. Don't use
  ImageSharp (licensing).
- Persistent data only under `StorageOptions.DataPath` (locally `src/CollectionCatalog.Web/App_Data`, `/data` in the
  container).
- Visitor IP (ForwardedHeaders in Program.cs) – used by `InquiryRateLimiter` and `LoginAttemptLimiter`
  (5 failures / 30 min). Forwarded headers are trusted only from loopback; with `Proxy:CloudflareTunnel=true` the IP
  comes from `CF-Connecting-IP` (never enable it without the tunnel). Web tests enable it and give every client its own
  `CF-Connecting-IP`.
- Sign-in: Identity, user name + password, no registration; accounts only via the CLI `user-set-password`.
- CLI commands (`Infrastructure/CliCommands.cs`): `user-set-password`, `seed-demo` – run instead of the web server,
  migrations are applied first; the command must be the **first argument**.
- **Languages:** codebook `Languages` (`LanguageService`, cached). The default language is at the root, others under
  a prefix (`/cs/…`) – `LanguagePathMiddleware` moves it into `PathBase` and sets the culture; `<base href>` in
  `App.razor` keeps links and the circuit in the language. Switching languages = full page load
  (`data-enhance-nav="false"`).
- **Content** (item name/description, photo captions, offer note, codebook names, website texts) = `LocalizedText`
  (JSON `{"en": …}` in one column, empty `{}`); display via `.Get(languages.Current)` with fallback to the default
  language (client-evaluated in EF projections). Forms derive from `TranslatableModel` (string properties for
  `EditLanguage`, required only in the default language) + `<EditLanguageTabs>` / `<LanguageMark>`.
- **Application texts:** English in code via `T["…"]` (injected in `_Imports.razor`), `IStringLocalizer<AppText>` in
  services, `AppText.Get` in static code. Translations in `Resources/AppText.{code}.resx` (English = the keys).
  Plurals `T.Plural("{0} photo|{0} photos", n)` (Czech has 3 forms). The same English word needing different
  translations → context `AppText.In("listing", "Sold")` = key `Sold [listing]`. **Keys must not differ only in
  case** (they collide in .resources – a test guards it). Line breaks in a key attribute must be written as `&#10;`.
  Validation: `<LocalizedValidator />` instead of `DataAnnotationsValidator` (ErrorMessage = English template = key).

## UI conventions (details in skill `admin-ui`)
- Plain HTML + Tailwind 4, no component library. Shared classes `card`, `form-input`, `btn-*`, `badge` in
  `Styles/app.css`.
- Lucide icons: `<span class="icon-[lucide--search] size-4"></span>` – the class literally in code.
- Code, comments and text keys in English. Amounts through `Format.Money`, sorting by language `TextComparer.Current`.

## Pitfalls
- **Blazor intercepts clicks on `<a href>`** before `@onclick:preventDefault` → use `<button>` for actions with own
  navigation.
- A page's `NavigationLock` **doesn't catch links in the static layout** (admin menu uses enhanced navigation) →
  unsaved changes are guarded by `UnsavedChangesGuard` (+ `wwwroot/js/unsaved-changes.js`).
- `InputFile` only as `SafeInputFile` – the original tears down the circuit on fast navigation.
- `<select @bind>` with `bool?` doesn't work – three-state filters as strings.
- Blazor doesn't keep `HistoryEntryState` across pages → returning to a list uses `ListNavigation` +
  `wwwroot/js/list-navigation.js` (a marker in `history.state`).
- Tailwind 4: `@apply` can't use own classes from `@layer components` – shared bases as `@utility`.
- Docker: restore runs on the bare `.csproj` → `RequiresAspNetWebAssets=true` in the csproj, otherwise
  `blazor.web.js` is missing.
- An async SSR page renders once before `OnInitializedAsync` finishes → markup starts with
  `@if (_data is null) { return; }`.
- Identity schema version comes from DI → tests supply `IdentityOptions` via `UseApplicationServiceProvider`
  (see `TestEnvironment`).
- Blazor renders `aria-*="false"` from a bool as a missing attribute → write the value as text.

## Commands
```bash
npm ci                                                          # once – Tailwind (CSS is generated by dotnet build)
dotnet build && dotnet test
dotnet run --project src/CollectionCatalog.Web                  # /admin
dotnet run --project src/CollectionCatalog.Web -- seed-demo     # demo collection into an empty database
dotnet run --project src/CollectionCatalog.Web -- user-set-password --username admin
docker compose up --build                                       # http://localhost:8080/health
```

## Skills (`.claude/skills/`)
- `verify-in-browser` – build next to a running instance, instance over a copy of the data, sign-in and walkthroughs.
- `ef-migration` – new migrations, hand-written data migrations, verification on a copy of the data.
- `admin-ui` – administration pages and components (layout, Tailwind, forms, navigation, pitfalls).
- `public-ui` – the public website (static SSR): layout, design, URLs, SEO, gallery, photos.
