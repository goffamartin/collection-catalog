using CollectionCatalog.Web.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemImage> ItemImages => Set<ItemImage>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<ItemType> ItemTypes => Set<ItemType>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<Language> Languages => Set<Language>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity reads the schema version (v3 = passkeys table) from IdentityOptions in the application service
        // provider of the context – see AuthenticationSetup; a context created outside DI must get it the same way (tests).
        base.OnModelCreating(modelBuilder);

        // Entity configurations live in separate IEntityTypeConfiguration<T> classes.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as text – adding/reordering values does not break existing data
        // (the desktop version stored ints and removing a value shifted the meaning of stored data).
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);

        // Prices – SQLite has no decimal, EF stores it as TEXT; the precision is kept consistent.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // Translatable texts are JSON in one column (see LocalizedText); their length is limited in services.
        configurationBuilder.Properties<LocalizedText>().HaveConversion<LocalizedTextConverter, LocalizedTextComparer>();
    }
}
