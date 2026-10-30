using CollectionCatalog.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CollectionCatalog.Web.Data.Configurations;

public class ItemImageConfiguration : IEntityTypeConfiguration<ItemImage>
{
    public void Configure(EntityTypeBuilder<ItemImage> builder)
    {
        builder.Property(e => e.FileKey).HasMaxLength(64);
        builder.Property(e => e.OriginalFileName).HasMaxLength(260);

        builder.HasIndex(e => new { e.ItemId, e.SortOrder });
        builder.HasIndex(e => e.FileKey);
    }
}
