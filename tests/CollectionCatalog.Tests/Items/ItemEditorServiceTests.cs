using System.ComponentModel.DataAnnotations;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Images;
using CollectionCatalog.Web.Services.Items;
using Microsoft.EntityFrameworkCore;
using static CollectionCatalog.Tests.Infrastructure.TestText;

namespace CollectionCatalog.Tests.Items;

[TestClass]
public sealed class ItemEditorServiceTests
{
    private TestEnvironment _env = null!;
    private ItemEditorService _service = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _env = await TestEnvironment.CreateAsync();
        _service = new ItemEditorService(new TestDbContextFactory(_env), _env.ImageStore, _env.Languages);
    }

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    public async Task Save_CreatesAndUpdatesItemWithCategories()
    {
        var (crystals, fossils) = await AddCategoriesAsync();
        var mineral = await AddTypeAsync("Mineral");

        var model = NewModel("  Amethyst geode ");
        model.TypeId = mineral.Id;
        model.CategoryIds = [crystals.Id];
        model.PurchasePrice = 15000;
        model.PrivateNotes = "   ";
        var id = await _service.SaveAsync(model);

        var created = (await _service.GetAsync(id))!;
        Assert.AreEqual("Amethyst geode", created.Name);
        Assert.AreEqual(mineral.Id, created.TypeId);
        Assert.AreEqual(15000m, created.PurchasePrice);
        Assert.IsNull(created.PrivateNotes, "Empty text is stored as null.");
        CollectionAssert.AreEquivalent(new[] { crystals.Id }, created.CategoryIds.ToArray());

        created.CategoryIds = [fossils.Id];
        created.Visibility = ItemVisibility.Public;
        await _service.SaveAsync(created);

        var updated = (await _service.GetAsync(id))!;
        Assert.AreEqual(ItemVisibility.Public, updated.Visibility);
        CollectionAssert.AreEquivalent(new[] { fossils.Id }, updated.CategoryIds.ToArray());
    }

    [TestMethod]
    public async Task CreateNew_PreselectsMostUsedType()
    {
        var mineral = await AddTypeAsync("Mineral");
        var fossil = await AddTypeAsync("Fossil");
        foreach (var (name, type) in new[] { ("Quartz", mineral), ("Pyrite", mineral), ("Trilobite", fossil) })
        {
            var item = NewModel(name);
            item.TypeId = type.Id;
            await _service.SaveAsync(item);
        }

        var model = await _service.CreateNewAsync();

        Assert.AreEqual(mineral.Id, model.TypeId);
        Assert.AreEqual("en", model.EditLanguage);
    }

    [TestMethod]
    public void Validation_ChecksYearsAndPrices()
    {
        var model = NewModel("");
        model.YearReleased = 1945;
        model.YearDiscontinued = 1918;
        model.PurchasePrice = -1;

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        results.AddRange(model.Validate(new ValidationContext(model)));

        var members = results.SelectMany(r => r.MemberNames).ToList();
        CollectionAssert.Contains(members, nameof(ItemEditModel.Name));
        CollectionAssert.Contains(members, nameof(ItemEditModel.PurchasePrice));
        CollectionAssert.Contains(members, nameof(ItemEditModel.YearDiscontinued));
    }

    [TestMethod]
    public async Task AddImage_AppendsWithGuessedTypes()
    {
        var id = await _service.SaveAsync(NewModel("Specimen"));

        await _service.AddImageAsync(id, new MemoryStream(TestImages.SplitJpeg(300, 200)), "front.jpg");
        await _service.AddImageAsync(id, new MemoryStream(TestImages.SplitJpeg(310, 200)), "back.jpg");
        await _service.AddImageAsync(id, new MemoryStream(TestImages.SplitJpeg(320, 200)), "detail.jpg");

        var images = await _service.GetImagesAsync(id);
        CollectionAssert.AreEqual(new[] { ImageType.Front, ImageType.Back, ImageType.Detail }, images.Select(i => i.ImageType).ToArray());
        CollectionAssert.AreEqual(new[] { "front.jpg", "back.jpg", "detail.jpg" }, images.Select(i => i.OriginalFileName).ToArray());
        Assert.IsTrue(File.Exists(_env.ImageStore.GetMasterPath(images[0].FileKey)));
    }

    [TestMethod]
    public async Task AddImage_InvalidFile_Throws()
    {
        var id = await _service.SaveAsync(NewModel("Specimen"));

        await Assert.ThrowsExactlyAsync<InvalidImageException>(
            () => _service.AddImageAsync(id, new MemoryStream("not an image"u8.ToArray()), "x.jpg"));
        Assert.IsEmpty(await _service.GetImagesAsync(id));
    }

    [TestMethod]
    public async Task UpdateImages_SavesOrderAndMetadata()
    {
        var id = await _service.SaveAsync(NewModel("Specimen"));
        await _service.AddImageAsync(id, new MemoryStream(TestImages.SplitJpeg(300, 200)), "a.jpg");
        await _service.AddImageAsync(id, new MemoryStream(TestImages.SplitJpeg(310, 200)), "b.jpg");

        var images = await _service.GetImagesAsync(id);
        images.Reverse();
        images[0].ImageType = ImageType.Certificate;
        images[0].IsPublic = false;
        images[0].Caption = En(" Certificate ");
        await _service.UpdateImagesAsync(id, images);

        var reloaded = await _service.GetImagesAsync(id);
        Assert.AreEqual("b.jpg", reloaded[0].OriginalFileName);
        Assert.AreEqual(ImageType.Certificate, reloaded[0].ImageType);
        Assert.IsFalse(reloaded[0].IsPublic);
        Assert.AreEqual("Certificate", reloaded[0].Caption["en"]);
    }

    [TestMethod]
    public async Task RotateImage_ReplacesFileAndKeepsMetadata()
    {
        var id = await _service.SaveAsync(NewModel("Specimen"));
        var original = await _service.AddImageAsync(id, new MemoryStream(TestImages.SplitJpeg(300, 200)), "a.jpg");
        var images = await _service.GetImagesAsync(id);
        images[0].Caption = En("Front");
        await _service.UpdateImagesAsync(id, images);

        var rotated = await _service.RotateImageAsync(original.Id, clockwise: true);

        Assert.AreEqual(original.Id, rotated.Id);
        Assert.AreNotEqual(original.FileKey, rotated.FileKey);
        Assert.AreEqual(200, rotated.Width);
        Assert.AreEqual(300, rotated.Height);
        Assert.AreEqual("Front", rotated.Caption["en"]);
        Assert.IsFalse(File.Exists(_env.ImageStore.GetMasterPath(original.FileKey)), "The old file is deleted.");
        Assert.IsTrue(File.Exists(_env.ImageStore.GetMasterPath(rotated.FileKey)));
    }

    [TestMethod]
    public async Task RotateImage_KeepsOriginalFileSharedWithOtherItem()
    {
        var photo = TestImages.SplitJpeg(300, 200);
        var first = await _service.SaveAsync(NewModel("Specimen 1"));
        var second = await _service.SaveAsync(NewModel("Specimen 2"));
        var image = await _service.AddImageAsync(first, new MemoryStream(photo), "a.jpg");
        await _service.AddImageAsync(second, new MemoryStream(photo), "a.jpg");

        await _service.RotateImageAsync(image.Id, clockwise: false);

        Assert.IsTrue(File.Exists(_env.ImageStore.GetMasterPath(image.FileKey)), "The original file is used by the other item too.");
    }

    [TestMethod]
    public async Task RemoveImage_KeepsFilesSharedWithOtherItem()
    {
        var photo = TestImages.SplitJpeg(300, 200);
        var first = await _service.SaveAsync(NewModel("Specimen 1"));
        var second = await _service.SaveAsync(NewModel("Specimen 2"));
        var shared = await _service.AddImageAsync(first, new MemoryStream(photo), "a.jpg");
        var copy = await _service.AddImageAsync(second, new MemoryStream(photo), "a.jpg");
        Assert.AreEqual(shared.FileKey, copy.FileKey);

        await _service.RemoveImageAsync(shared.Id);
        Assert.IsTrue(File.Exists(_env.ImageStore.GetMasterPath(shared.FileKey)), "The file is used by the other item too.");

        await _service.RemoveImageAsync(copy.Id);
        Assert.IsFalse(File.Exists(_env.ImageStore.GetMasterPath(shared.FileKey)));
        Assert.IsFalse(_env.StoredImageFiles().Any());
    }

    [TestMethod]
    public async Task Delete_RemovesItemImagesAndKeepsInquiries()
    {
        var id = await _service.SaveAsync(NewModel("Specimen"));
        await _service.AddImageAsync(id, new MemoryStream(TestImages.SplitJpeg(300, 200)), "a.jpg");
        await using (var db = _env.CreateDbContext())
        {
            db.Inquiries.Add(new Inquiry { ItemId = id, Name = "Jane", Email = "jane@example.com", Message = "Interested" });
            await db.SaveChangesAsync();
        }

        await _service.DeleteAsync(id);

        Assert.IsNull(await _service.GetAsync(id));
        Assert.IsFalse(_env.StoredImageFiles().Any());
        await using (var db = _env.CreateDbContext())
        {
            Assert.AreEqual(1, await db.Inquiries.CountAsync());
        }
    }

    /// <summary>Edit model the way the editor creates it – editing the default language (English).</summary>
    private static ItemEditModel NewModel(string name)
    {
        var model = new ItemEditModel();
        model.SetLanguages(new LanguageContext("en", "en"));
        model.Name = name;
        return model;
    }

    private async Task<ItemType> AddTypeAsync(string name)
    {
        await using var db = _env.CreateDbContext();
        var type = new ItemType { Name = En(name) };
        db.ItemTypes.Add(type);
        await db.SaveChangesAsync();
        return type;
    }

    private async Task<(Category Crystals, Category Fossils)> AddCategoriesAsync()
    {
        await using var db = _env.CreateDbContext();
        var crystals = new Category { Name = En("Crystals") };
        var fossils = new Category { Name = En("Fossils") };
        db.Categories.AddRange(crystals, fossils);
        await db.SaveChangesAsync();
        return (crystals, fossils);
    }
}
