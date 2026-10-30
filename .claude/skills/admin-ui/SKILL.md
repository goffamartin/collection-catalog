---
name: admin-ui
description: Writing administration pages and components of Collection Catalog (Blazor Interactive Server + Tailwind CSS 4 + Lucide icons) – layout, design system, forms, lists with filters in the URL, detail ↔ list navigation, photo upload, translatable fields and proven Blazor pitfalls. Use for any change in Components/Admin or Styles/app.css.
---

# Admin UI

## Page skeleton
```razor
@page "/admin/something"
@rendermode InteractiveServer
@inject SomethingService Something

<PageTitle>@T["Something"] – @T["administration"]</PageTitle>

<PageHeader Title="@T["Something"]">
    <Description>@T["Short description."]</Description>
    <Actions><button type="button" class="btn-primary" @onclick="..."><span class="icon-[lucide--plus] size-4"></span> @T["Add"]</button></Actions>
</PageHeader>

<div class="card"> … </div>
```
- Layout and `[Authorize]` come from `Components/Admin/Pages/_Imports.razor`. New shared components go to
  `Components/Admin/Shared`; item editor parts to `Components/Admin/Items`.
- Add navigation to `Shared/AdminNavLinks.razor` (sidebar and mobile menu) and the URL to `AdminUrls`.
- Data: a service in `Services/<Area>/` using `IDbContextFactory<AppDbContext>`, registered in `Program.cs`, tests in
  `tests/.../<Area>/`.
- Texts: English via `T["…"]`, Czech translation in `Resources/AppText.cs.resx`.

## Design system (`Styles/app.css`)
- Palette: neutral `stone`, accent `amber`. Background `bg-stone-50`, white cards.
- Classes: `card`, `card-header`, `card-title`, `card-body`; `form-label`, `form-input` (also for `select`/`textarea`),
  `form-hint`, `form-checkbox`; `btn-primary`, `btn-accent`, `btn-secondary`, `btn-ghost`, `btn-danger`,
  `btn-danger-solid` (confirming a destructive action), modifier `btn-sm`; `badge`.
- New shared classes go to `@layer components`. A class needed in `@apply` of another must be an `@utility`.
- Icons: `<span class="icon-[lucide--name] size-4"></span>` – the name **literally** (Tailwind scans the sources).
- Touch devices: `pointer-coarse:` / `pointer-fine:` variants (not the window width).
- Ready components: `UnsavedChangesGuard`, `PageHeader`, `SaveBar`, `FilterTabs`, `Pager`, `ItemBadge`,
  `AdminThumbnail`, `StatTile`, `Toggle`, `MoneyInput`, `SortHeader`, `CodebookPanel`, `LanguagePanel`, `ItemPhotos`,
  `ItemListingPanel`, `EditLanguageTabs`, `LanguageMark`, `BrandMark`, `CategoryColors.BadgeStyle(color)`,
  `Format.Money/Number`.
- Bool values in `aria-*` as text (`aria-pressed="@(x ? "true" : "false")"`) – Blazor drops a `false` attribute.
- Mobile: hide table columns with `hidden md:table-cell`; important content (photos) `order-first xl:order-none`.

## Forms (interactive)
- `EditForm` + `<LocalizedValidator />` + `ValidationMessage`; model with DataAnnotations and `IValidatableObject`
  (pattern `ItemEditModel`); `ErrorMessage` is the English template.
- Translatable fields: the model derives from `TranslatableModel` (string properties read/write the `EditLanguage`
  value of a `LocalizedText`), the page shows `<EditLanguageTabs @bind-Value="_model.EditLanguage" />`, labels get
  `<LanguageMark Code="…" />`, and the placeholder shows the default-language text (`Model.Fallback(...)`).
- Unsaved changes: `EditContext.OnFieldChanged` → `_dirty` + `StateHasChanged()` (fields live in section
  sub-components); custom controls outside `Input*` report changes with `EditContext.NotifyFieldChanged(...)`.
  Leaving the page is guarded **only by `<UnsavedChangesGuard HasChanges="() => _dirty" />`** – `NavigationLock` alone
  misses links in the static layout. The header Save button is enabled only with unsaved changes.
  Parts saved immediately (photos, offer) must say so in the card header and show "Saving…/Saved".
  Next to "Save changes" belongs "Discard changes" (reload the saved state – pattern `ItemEditor.LoadAsync`).
- Errors of saving: `SafeAction.RunAsync(...)` + `T[SafeAction.Message]` – an exception must not tear down the circuit.
- `InputSelect` with nullable int/enum works; **native `<select @bind>` with `bool?` doesn't** → strings.
- Inline confirmation ("Delete?" Yes/No) instead of dialogs.
- A separately saved panel **inside** another `EditForm` (pattern `ItemListingPanel`): no nested `EditForm` and no
  `Input*`/`MoneyInput` (they would attach to the parent's EditContext) – native `<input @bind>` + own button.

## Lists and navigation
- List state (filters, sorting, page) lives in the URL (pattern `ItemListState` + `Items.razor`); search filters
  immediately and writes the URL after a debounce.
- A detail returns **by a step back in history**: the list opens it via `ListNavigation.OpenFromListAsync(url)`, the
  detail calls `CameFromListAsync()` in `OnAfterRenderAsync(firstRender)` and `BackAsync(...)`.
- **Actions with own navigation as `<button>`, never `<a href>`** – Blazor intercepts the link first.
- Full-text: `SearchText.Normalize/Matches` (no diacritics, word starts); structured filters in SQL, text in memory.

## Photos and files
- Upload only through `SafeInputFile` (not `InputFile`), into a `MemoryStream` with an explicit size limit, processed
  by `ItemEditorService.AddImageAsync`.
- Admin images: `admin/media/{fileKey}/{400|800|1600}` (authorized endpoint, WebP).

## Verification
Always walk through the change in a browser following `verify-in-browser` (desktop and mobile, clean console).
