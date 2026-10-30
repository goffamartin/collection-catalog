using CollectionCatalog.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CollectionCatalog.Web.Data.Configurations;

public class SiteSettingsConfiguration : IEntityTypeConfiguration<SiteSettings>
{
    public void Configure(EntityTypeBuilder<SiteSettings> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.ContactEmail).HasMaxLength(200);
        builder.Property(e => e.ContactPhone).HasMaxLength(50);

        // The single row with default texts – the website works right after deployment.
        builder.HasData(new SiteSettings
        {
            Id = SiteSettings.SingletonId,
            SiteName = LocalizedText.Of("en", "Collection").With("cs", "Sbírka"),
            Headline = LocalizedText.Of("en", "A private collection").With("cs", "Soukromá sbírka"),
            Intro = LocalizedText.Of("en", "Selected pieces of a private collection – with photos, stories and pieces for sale.")
                .With("cs", "Vybrané kusy soukromé sbírky – s fotkami, příběhy a kusy na prodej."),
            CreatedAt = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
