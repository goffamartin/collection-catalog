using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Images;
using CollectionCatalog.Web.Services.Languages;
using CollectionCatalog.Web.Services.Text;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Web.Services.Items;

/// <summary>
/// Editing an item in the administration: data, categories and photos.
/// </summary>
/// <remarks>
/// Every operation takes its own DbContext (it is called from interactive components, where a DbContext must not live
/// for the whole circuit). Photo changes are saved immediately; item data only with the Save button.
/// </remarks>
public sealed class ItemEditorService(IDbContextFactory<AppDbContext> dbContextFactory, ImageStore imageStore, LanguageService languages)
{
    public async Task<EditorLookups> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var types = await db.ItemTypes.AsNoTracking().ToListAsync(cancellationToken);
        var countries = await db.Countries.AsNoTracking().ToListAsync(cancellationToken);
        var categories = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);

        var language = languages.Current;
        var comparer = TextComparer.For(language.Code);
        List<LookupOption> Options(IEnumerable<LookupOption> options) => options.OrderBy(o => o.Name, comparer).ToList();
        return new EditorLookups(
            Options(types.Select(t => new LookupOption(t.Id, t.Name.Get(language) ?? ""))),
            Options(countries.Select(c => new LookupOption(c.Id, c.Name.Get(language) ?? ""))),
            Options(categories.Select(c => new LookupOption(c.Id, c.Name.Get(language) ?? "", c.Color))));
    }

    /// <summary>Model of a new item – with the most used type preselected (the likely type of the next one).</summary>
    public async Task<ItemEditModel> CreateNewAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var typeId = await db.Items
            .Where(i => i.TypeId != null)
            .GroupBy(i => i.TypeId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(cancellationToken);
        var model = new ItemEditModel { TypeId = typeId };
        model.SetLanguages(languages.Current);
        return model;
    }

    public async Task<ItemEditModel?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Items.AsNoTracking()
            .Include(i => i.Categories)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return null;
        }
        var model = ItemEditModel.FromEntity(item);
        model.SetLanguages(languages.Current);
        return model;
    }

    /// <summary>
    /// Saves a new or an existing item and returns its Id.
    /// </summary>
    public async Task<int> SaveAsync(ItemEditModel model, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        Item item;
        if (model.Id is { } id)
        {
            item = await db.Items.Include(i => i.Categories).SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
                ?? throw new InvalidOperationException($"Item #{id} does not exist.");
        }
        else
        {
            item = new Item { Name = model.NameText };
            db.Items.Add(item);
        }

        model.ApplyTo(item);

        var categories = await db.Categories.Where(c => model.CategoryIds.Contains(c.Id)).ToListAsync(cancellationToken);
        item.Categories.Clear();
        foreach (var category in categories)
        {
            item.Categories.Add(category);
        }

        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    /// <summary>
    /// Deletes an item including its photos (files only when no other item shares them). Inquiries stay.
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Items.Include(i => i.Images).SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return;
        }

        var fileKeys = item.Images.Select(i => i.FileKey).Distinct().ToList();
        db.Items.Remove(item);
        await db.SaveChangesAsync(cancellationToken);

        await DeleteUnreferencedFilesAsync(db, fileKeys, cancellationToken);
    }

    /// <summary>Bulk publishing / hiding of items from the list.</summary>
    /// <returns>Number of items whose visibility changed.</returns>
    public async Task<int> SetVisibilityAsync(IReadOnlyCollection<int> ids, ItemVisibility visibility, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Through entities (not ExecuteUpdate), so AuditInterceptor sets ModifiedAt – the sitemap relies on it.
        var items = await db.Items.Where(i => ids.Contains(i.Id) && i.Visibility != visibility).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.Visibility = visibility;
        }
        await db.SaveChangesAsync(cancellationToken);
        return items.Count;
    }

    public async Task<List<ItemImageModel>> GetImagesAsync(int itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var images = await db.ItemImages.AsNoTracking()
            .Where(i => i.ItemId == itemId)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);
        return images.Select(ItemImageModel.FromEntity).ToList();
    }

    /// <summary>
    /// Processes a photo and adds it to the end of the gallery. The type is guessed by order (first obverse, second reverse).
    /// </summary>
    /// <exception cref="InvalidImageException">The file is not a supported image.</exception>
    public async Task<ItemImageModel> AddImageAsync(int itemId, Stream content, string? fileName, CancellationToken cancellationToken = default)
    {
        var processed = await imageStore.ProcessAsync(content, cancellationToken);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Items.AnyAsync(i => i.Id == itemId, cancellationToken))
        {
            throw new InvalidOperationException($"Item #{itemId} does not exist.");
        }

        var count = await db.ItemImages.CountAsync(i => i.ItemId == itemId, cancellationToken);
        var maxOrder = count == 0 ? -1 : await db.ItemImages.Where(i => i.ItemId == itemId).MaxAsync(i => i.SortOrder, cancellationToken);

        var written = await imageStore.WriteAsync(processed, cancellationToken);
        var image = new ItemImage
        {
            ItemId = itemId,
            FileKey = processed.FileKey,
            Width = processed.Width,
            Height = processed.Height,
            ImageType = count switch { 0 => ImageType.Front, 1 => ImageType.Back, _ => ImageType.Detail },
            SortOrder = maxOrder + 1,
            IsPublic = true,
            OriginalFileName = fileName is null ? null : Path.GetFileName(fileName),
        };
        db.ItemImages.Add(image);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch when (written)
        {
            imageStore.Delete(processed.FileKey);
            throw;
        }

        return ItemImageModel.FromEntity(image);
    }

    /// <summary>
    /// Saves the order (by the order in the list), type, caption and visibility of the item's photos.
    /// </summary>
    public async Task UpdateImagesAsync(int itemId, IReadOnlyList<ItemImageModel> images, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.ItemImages.Where(i => i.ItemId == itemId).ToDictionaryAsync(i => i.Id, cancellationToken);

        for (var order = 0; order < images.Count; order++)
        {
            if (entities.TryGetValue(images[order].Id, out var entity))
            {
                entity.SortOrder = order;
                entity.ImageType = images[order].ImageType;
                entity.Caption = images[order].Caption.Select(caption => TextLimit.Clean(caption, ItemImage.MaxCaptionLength));
                entity.IsPublic = images[order].IsPublic;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Rotates a photo by 90°. A new file is created (different content hash); the old one is deleted unless another item uses it.
    /// </summary>
    public async Task<ItemImageModel> RotateImageAsync(int imageId, bool clockwise, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var image = await db.ItemImages.SingleOrDefaultAsync(i => i.Id == imageId, cancellationToken)
            ?? throw new InvalidOperationException($"Fotka #{imageId} neexistuje.");

        var oldFileKey = image.FileKey;
        var rotated = await imageStore.RotateAsync(oldFileKey, clockwise, cancellationToken);
        var written = await imageStore.WriteAsync(rotated, cancellationToken);

        image.FileKey = rotated.FileKey;
        image.Width = rotated.Width;
        image.Height = rotated.Height;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch when (written)
        {
            imageStore.Delete(rotated.FileKey);
            throw;
        }

        await DeleteUnreferencedFilesAsync(db, [oldFileKey], cancellationToken);
        return ItemImageModel.FromEntity(image);
    }

    public async Task RemoveImageAsync(int imageId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var image = await db.ItemImages.SingleOrDefaultAsync(i => i.Id == imageId, cancellationToken);
        if (image is null)
        {
            return;
        }

        db.ItemImages.Remove(image);
        await db.SaveChangesAsync(cancellationToken);

        await DeleteUnreferencedFilesAsync(db, [image.FileKey], cancellationToken);
    }

    private async Task DeleteUnreferencedFilesAsync(AppDbContext db, IReadOnlyCollection<string> fileKeys, CancellationToken cancellationToken)
    {
        var stillUsed = await db.ItemImages
            .Where(i => fileKeys.Contains(i.FileKey))
            .Select(i => i.FileKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var fileKey in fileKeys.Except(stillUsed))
        {
            imageStore.Delete(fileKey);
        }
    }
}
