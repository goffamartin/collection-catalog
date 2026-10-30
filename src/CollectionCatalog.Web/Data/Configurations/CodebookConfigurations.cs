using CollectionCatalog.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CollectionCatalog.Web.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(e => e.Color).HasMaxLength(20);
    }
}

public class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.Property(e => e.Code).HasMaxLength(Language.MaxCodeLength);
        builder.Property(e => e.Name).HasMaxLength(Language.MaxNameLength);

        builder.HasIndex(e => e.Code).IsUnique();

        // English by default, Czech ready for translations (the application texts exist in both).
        builder.HasData(
            new Language { Id = 1, Code = "en", Name = "English", IsDefault = true, SortOrder = 0 },
            new Language { Id = 2, Code = "cs", Name = "Čeština", SortOrder = 1 });
    }
}
