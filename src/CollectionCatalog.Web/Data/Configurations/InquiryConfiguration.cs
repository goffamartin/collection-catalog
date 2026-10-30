using CollectionCatalog.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CollectionCatalog.Web.Data.Configurations;

public class InquiryConfiguration : IEntityTypeConfiguration<Inquiry>
{
    public void Configure(EntityTypeBuilder<Inquiry> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(200);
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.Message).HasMaxLength(4000);
        builder.Property(e => e.InternalNote).HasMaxLength(Inquiry.MaxNoteLength);

        // Inquiries are kept even after the item / offer is deleted.
        builder.HasOne(e => e.Item)
            .WithMany()
            .HasForeignKey(e => e.ItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Listing)
            .WithMany(l => l.Inquiries)
            .HasForeignKey(e => e.ListingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAt);
    }
}
