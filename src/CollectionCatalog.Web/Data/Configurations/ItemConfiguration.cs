using CollectionCatalog.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CollectionCatalog.Web.Data.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.Property(e => e.PrivateNotes).HasMaxLength(8000);
        builder.Property(e => e.Provenance).HasMaxLength(2000);
        builder.Property(e => e.Seller).HasMaxLength(200);

        builder.HasOne(e => e.Type)
            .WithMany(t => t.Items)
            .HasForeignKey(e => e.TypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Country)
            .WithMany(c => c.Items)
            .HasForeignKey(e => e.CountryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Categories)
            .WithMany(c => c.Items)
            .UsingEntity(
                "ItemCategories",
                category => category.HasOne(typeof(Category)).WithMany().HasForeignKey("CategoryId").OnDelete(DeleteBehavior.Cascade),
                item => item.HasOne(typeof(Item)).WithMany().HasForeignKey("ItemId").OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("ItemId", "CategoryId"));

        builder.HasMany(e => e.Images)
            .WithOne(i => i.Item)
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Listings)
            .WithOne(l => l.Item)
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.Visibility);
        builder.HasIndex(e => e.YearReleased);

        builder.Ignore(e => e.IsSold);
    }
}
