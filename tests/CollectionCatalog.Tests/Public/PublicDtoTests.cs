using System.Reflection;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Public;

namespace CollectionCatalog.Tests.Public;

/// <summary>
/// Guard against leaking private data: public DTOs must not have any property named like a sensitive item field.
/// </summary>
[TestClass]
public sealed class PublicDtoTests
{
    private static readonly string[] _sensitiveFields =
    [
        nameof(Item.PurchasePrice),
        nameof(Item.PurchaseDate),
        nameof(Item.Seller),
        nameof(Item.EstimatedValue),
        nameof(Item.SalePrice),
        nameof(Item.SaleDate),
        nameof(Item.Provenance),
        nameof(Item.PrivateNotes),
        nameof(Item.Visibility),
        nameof(ItemImage.OriginalFileName),
        nameof(ItemImage.IsPublic),
    ];

    [TestMethod]
    public void PublicDtos_DoNotExposeSensitiveFields()
    {
        var dtoTypes = typeof(PublicItemQuery).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(PublicItemQuery).Namespace && t.IsPublic && t != typeof(PublicItemQuery))
            .ToList();

        Assert.IsNotEmpty(dtoTypes);
        foreach (var type in dtoTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                CollectionAssert.DoesNotContain(_sensitiveFields, property.Name, $"{type.Name}.{property.Name}");
                Assert.IsFalse(property.PropertyType.IsAssignableTo(typeof(Item)) || property.PropertyType.IsAssignableTo(typeof(ItemImage)),
                    $"{type.Name}.{property.Name} returns an entity – public code uses DTOs only.");
            }
        }
    }
}
