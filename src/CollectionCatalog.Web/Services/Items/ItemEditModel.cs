using System.ComponentModel.DataAnnotations;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Languages;

namespace CollectionCatalog.Web.Services.Items;

/// <summary>
/// Form model of the item editor (administration). Name and public description are translatable,
/// see <see cref="TranslatableModel"/>.
/// </summary>
public sealed class ItemEditModel : TranslatableModel, IValidatableObject
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 8000;

    private const int MinYear = 1000;
    private const int MaxYear = 2100;
    private const double MaxPrice = 100_000_000;

    /// <summary><c>null</c> = a new item.</summary>
    public int? Id { get; set; }

    public LocalizedText NameText { get; set; } = LocalizedText.Empty;
    public LocalizedText PublicDescriptionText { get; set; } = LocalizedText.Empty;

    [StringLength(MaxNameLength, ErrorMessage = "The name can have at most {1} characters.")]
    public string? Name { get => Read(NameText); set => NameText = Write(NameText, value); }

    public int? TypeId { get; set; }

    [Range(MinYear, MaxYear, ErrorMessage = "Enter a year {1}–{2}.")]
    public int? YearReleased { get; set; }

    [Range(MinYear, MaxYear, ErrorMessage = "Enter a year {1}–{2}.")]
    public int? YearDiscontinued { get; set; }

    public int? CountryId { get; set; }
    public HashSet<int> CategoryIds { get; set; } = [];

    public ConditionState Condition { get; set; } = ConditionState.Good;
    public Originality Originality { get; set; } = Originality.Original;
    public bool HasBox { get; set; }
    public bool HasCertificate { get; set; }
    public bool HasDocumentation { get; set; }

    public ItemVisibility Visibility { get; set; } = ItemVisibility.Private;
    public bool IsFeatured { get; set; }

    [StringLength(MaxDescriptionLength, ErrorMessage = "The description can have at most {1} characters.")]
    public string? PublicDescription { get => Read(PublicDescriptionText); set => PublicDescriptionText = Write(PublicDescriptionText, value); }

    [StringLength(8000, ErrorMessage = "The notes can have at most {1} characters.")]
    public string? PrivateNotes { get; set; }

    [StringLength(2000, ErrorMessage = "The provenance can have at most {1} characters.")]
    public string? Provenance { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    [Range(0, MaxPrice, ErrorMessage = "Enter a non-negative amount.")]
    public decimal? PurchasePrice { get; set; }

    [StringLength(200, ErrorMessage = "The seller can have at most {1} characters.")]
    public string? Seller { get; set; }

    [Range(0, MaxPrice, ErrorMessage = "Enter a non-negative amount.")]
    public decimal? EstimatedValue { get; set; }

    public DateOnly? SaleDate { get; set; }

    [Range(0, MaxPrice, ErrorMessage = "Enter a non-negative amount.")]
    public decimal? SalePrice { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Required only in the default language – other languages fall back to it.
        if (NameText[DefaultLanguage] is null)
        {
            yield return new ValidationResult("Enter the name in the default language.", [nameof(Name)]);
        }

        if (YearReleased is { } from && YearDiscontinued is { } to && to < from)
        {
            yield return new ValidationResult("The year of discontinuation cannot be earlier than the year of introduction.", [nameof(YearDiscontinued)]);
        }

        if (SaleDate is { } sold && PurchaseDate is { } bought && sold < bought)
        {
            yield return new ValidationResult("The sale date cannot be earlier than the purchase date.", [nameof(SaleDate)]);
        }
    }

    public static ItemEditModel FromEntity(Item item) => new()
    {
        Id = item.Id,
        NameText = item.Name,
        TypeId = item.TypeId,
        YearReleased = item.YearReleased,
        YearDiscontinued = item.YearDiscontinued,
        CountryId = item.CountryId,
        CategoryIds = item.Categories.Select(c => c.Id).ToHashSet(),
        Condition = item.Condition,
        Originality = item.Originality,
        HasBox = item.HasBox,
        HasCertificate = item.HasCertificate,
        HasDocumentation = item.HasDocumentation,
        Visibility = item.Visibility,
        IsFeatured = item.IsFeatured,
        PublicDescriptionText = item.PublicDescription,
        PrivateNotes = item.PrivateNotes,
        Provenance = item.Provenance,
        PurchaseDate = item.PurchaseDate,
        PurchasePrice = item.PurchasePrice,
        Seller = item.Seller,
        EstimatedValue = item.EstimatedValue,
        SaleDate = item.SaleDate,
        SalePrice = item.SalePrice,
    };

    public void ApplyTo(Item item)
    {
        item.Name = NameText;
        item.TypeId = TypeId;
        item.YearReleased = YearReleased;
        item.YearDiscontinued = YearDiscontinued;
        item.CountryId = CountryId;
        item.Condition = Condition;
        item.Originality = Originality;
        item.HasBox = HasBox;
        item.HasCertificate = HasCertificate;
        item.HasDocumentation = HasDocumentation;
        item.Visibility = Visibility;
        item.IsFeatured = IsFeatured;
        item.PublicDescription = PublicDescriptionText;
        item.PrivateNotes = Clean(PrivateNotes);
        item.Provenance = Clean(Provenance);
        item.PurchaseDate = PurchaseDate;
        item.PurchasePrice = PurchasePrice;
        item.Seller = Clean(Seller);
        item.EstimatedValue = EstimatedValue;
        item.SaleDate = SaleDate;
        item.SalePrice = SalePrice;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>
/// Photo in the item editor. The caption is translatable – the photo panel edits the language chosen in the editor.
/// </summary>
public sealed class ItemImageModel
{
    public int Id { get; init; }
    public required string FileKey { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public ImageType ImageType { get; set; }
    public LocalizedText Caption { get; set; } = LocalizedText.Empty;
    public bool IsPublic { get; set; }
    public string? OriginalFileName { get; init; }

    public static ItemImageModel FromEntity(ItemImage image) => new()
    {
        Id = image.Id,
        FileKey = image.FileKey,
        Width = image.Width,
        Height = image.Height,
        ImageType = image.ImageType,
        Caption = image.Caption,
        IsPublic = image.IsPublic,
        OriginalFileName = image.OriginalFileName,
    };
}

/// <summary>
/// Codebooks for the selects of the editor and filters, named in the current language.
/// </summary>
public sealed record EditorLookups(IReadOnlyList<LookupOption> Types, IReadOnlyList<LookupOption> Countries, IReadOnlyList<LookupOption> Categories);

public sealed record LookupOption(int Id, string Name, string? Color = null);
