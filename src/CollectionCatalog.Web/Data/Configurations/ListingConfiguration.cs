using CollectionCatalog.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CollectionCatalog.Web.Data.Configurations;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {

        builder.HasIndex(e => e.Status);

        // At most one open offer per item (enums are stored as text).
        var openStatuses = string.Join(", ", Listing.OpenStatuses.Select(s => $"'{s}'"));
        builder.HasIndex(e => e.ItemId)
            .IsUnique()
            .HasFilter($"\"Status\" IN ({openStatuses})")
            .HasDatabaseName("IX_Listings_ItemId_Open");

        builder.Ignore(e => e.IsOpen);
    }
}
