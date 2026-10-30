using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Images;
using CollectionCatalog.Web.Services.Languages;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace CollectionCatalog.Web.Services.Demo;

/// <summary>
/// CLI command <c>seed-demo</c> – fills an empty database with a demo collection of minerals, fossils and curiosities
/// (English and Czech texts, photos drawn in code, a few offers):
/// <code>
/// dotnet run --project src/CollectionCatalog.Web -- seed-demo
/// </code>
/// Refuses to run when the database already holds items, so it cannot mix into a real collection.
/// </summary>
public static class DemoSeedCommand
{
    public const string Name = "seed-demo";

    private sealed record Specimen(
        string En, string Cs, string Type, string Country, string[] Categories, SpecimenShape Shape, string Color,
        string DescriptionEn, string DescriptionCs, int? Year = null, decimal? Price = null, decimal? Value = null,
        bool Public = true, bool Featured = false, ListingStatus? Offer = null, decimal? AskingPrice = null);

    private static readonly (string Key, string En, string Cs)[] _types =
    [
        ("mineral", "Mineral", "Minerál"), ("fossil", "Fossil", "Fosilie"), ("meteorite", "Meteorite", "Meteorit"), ("curiosity", "Curiosity", "Kuriozita"),
    ];

    private static readonly (string Key, string En, string Cs)[] _countries =
    [
        ("br", "Brazil", "Brazílie"), ("ma", "Morocco", "Maroko"), ("cz", "Czech Republic", "Česko"), ("mg", "Madagascar", "Madagaskar"),
        ("us", "USA", "USA"), ("na", "Namibia", "Namibie"), ("pe", "Peru", "Peru"), ("mx", "Mexico", "Mexiko"),
    ];

    private static readonly (string Key, string En, string Cs, string Color)[] _categories =
    [
        ("crystals", "Crystals", "Krystaly", "#7C3AED"), ("gems", "Gemstones", "Drahé kameny", "#DB2777"),
        ("fossils", "Fossils", "Zkameněliny", "#A16207"), ("space", "From space", "Z vesmíru", "#1D4ED8"),
        ("rare", "Rarities", "Rarity", "#059669"),
    ];

    private static readonly Specimen[] _specimens =
    [
        new("Amethyst geode", "Ametystová geoda", "mineral", "br", ["crystals", "gems"], SpecimenShape.Geode, "#7B4BB7",
            "A hollow volcanic nodule lined with deep violet amethyst points. The banded agate rim shows how the cavity filled layer by layer.",
            "Dutá vulkanická hlíza vystlaná sytě fialovými krystaly ametystu. Páskovaný achátový okraj ukazuje, jak se dutina plnila vrstvu po vrstvě.",
            Year: 2019, Price: 4200, Value: 6500, Featured: true),
        new("Rose quartz cluster", "Drúza růženínu", "mineral", "mg", ["crystals"], SpecimenShape.Crystals, "#E9A3B8",
            "Rare crystallised rose quartz – the pink variety usually forms only massive pieces without crystal faces.",
            "Vzácný krystalický růženín – růžová odrůda křemene jinak tvoří jen masivní kusy bez krystalových ploch.",
            Year: 2021, Price: 2800, Value: 3500, Offer: ListingStatus.Active, AskingPrice: 3900),
        new("Ammonite fossil", "Amonit", "fossil", "ma", ["fossils"], SpecimenShape.Ammonite, "#B07A45",
            "A spiral shell of a marine cephalopod that lived some 400 million years ago. Cut and polished to show the chambers.",
            "Spirální schránka mořského hlavonožce, který žil asi před 400 miliony let. Rozříznutá a vyleštěná, aby byly vidět komůrky.",
            Price: 1500, Value: 2200, Featured: true),
        new("Gibeon meteorite slice", "Řez meteoritem Gibeon", "meteorite", "na", ["space", "rare"], SpecimenShape.Meteorite, "#4B4F57",
            "An iron meteorite with the etched Widmanstätten pattern – crystals of iron and nickel that grew over millions of years in space.",
            "Železný meteorit s naleptanými Widmanstättenovými obrazci – krystaly železa a niklu rostly ve vesmíru miliony let.",
            Price: 9500, Value: 12000, Featured: true),
        new("Labradorite sphere", "Labradoritová koule", "mineral", "mg", ["gems"], SpecimenShape.Sphere, "#3E6C8A",
            "Polished sphere showing labradorescence – flashes of blue and gold that change with the angle of light.",
            "Leštěná koule s labradorescencí – záblesky modré a zlaté, které se mění s úhlem světla.",
            Price: 1900, Value: 2400, Offer: ListingStatus.Reserved, AskingPrice: 2600),
        new("Citrine points", "Krystaly citrínu", "mineral", "br", ["crystals", "gems"], SpecimenShape.Crystals, "#E3A93B",
            "Golden quartz crystals from Rio Grande do Sul. The colour comes from traces of iron.",
            "Zlatavé krystaly křemene z Rio Grande do Sul. Barvu jim dávají stopy železa.",
            Year: 2018, Price: 1200, Value: 1600),
        new("Moldavite", "Vltavín", "curiosity", "cz", ["space", "rare", "gems"], SpecimenShape.Sphere, "#4E7A3A",
            "Green natural glass formed by a meteorite impact some 15 million years ago, found in southern Bohemia.",
            "Zelené přírodní sklo vzniklé při dopadu meteoritu asi před 15 miliony let, nalezené v jižních Čechách.",
            Year: 2016, Price: 3600, Value: 5200, Featured: true),
        new("Smoky quartz cluster", "Drúza záhnědy", "mineral", "us", ["crystals"], SpecimenShape.Crystals, "#6B5A4C",
            "Translucent brown crystals coloured by natural radiation in the surrounding granite.",
            "Průsvitné hnědé krystaly zbarvené přirozeným zářením okolní žuly.",
            Price: 900, Value: 1100),
        new("Agate nodule", "Achátová hlíza", "mineral", "mx", ["gems"], SpecimenShape.Geode, "#C26A3D",
            "Concentric bands of chalcedony in warm orange and cream, cut in half and polished.",
            "Soustředné pásky chalcedonu v teplých oranžových a krémových tónech, rozříznutá a vyleštěná.",
            Price: 650, Value: 800, Offer: ListingStatus.Active, AskingPrice: 950),
        new("Aquamarine crystal", "Krystal akvamarínu", "mineral", "pe", ["crystals", "gems", "rare"], SpecimenShape.Crystals, "#7FC4D6",
            "Sea-blue beryl with sharp hexagonal faces. Clear enough to be cut into a gemstone.",
            "Mořsky modrý beryl s ostrými šestibokými plochami. Dost čistý na vybroušení drahokamu.",
            Year: 2022, Price: 7800, Value: 9800),
        new("Pyrite sun", "Pyritové slunce", "curiosity", "us", ["rare"], SpecimenShape.Sphere, "#B89A4C",
            "A flat radiating disc of pyrite that grew between layers of shale – a rare formation from Illinois coal mines.",
            "Plochý paprsčitý disk pyritu, který vyrostl mezi vrstvami břidlice – vzácný útvar z uhelných dolů v Illinois.",
            Price: 1100, Value: 1500),
        new("Fossil ammonite pair", "Pár amonitů", "fossil", "mg", ["fossils"], SpecimenShape.Ammonite, "#C48B5A",
            "Both halves of one ammonite showing iridescent nacre preserved from the Cretaceous sea.",
            "Obě poloviny jednoho amonitu s duhovou perletí zachovanou z křídového moře.",
            Price: 2100, Value: 2600, Public: false),
        new("Celestine geode", "Celestinová geoda", "mineral", "mg", ["crystals"], SpecimenShape.Geode, "#9DB8D9",
            "Pale sky-blue celestine crystals inside a rough grey nodule.",
            "Světle nebesky modré krystaly celestinu uvnitř hrubé šedé hlízy.",
            Price: 1800, Value: 2300),
        new("Stony meteorite", "Kamenný meteorit", "meteorite", "ma", ["space"], SpecimenShape.Meteorite, "#5C4A3E",
            "An ordinary chondrite from the Sahara with a dark fusion crust from its fall through the atmosphere.",
            "Obyčejný chondrit ze Sahary s tmavou tavnou kůrou z průletu atmosférou.",
            Price: 2400, Value: 2800, Public: false),
    ];

    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var imageStore = scope.ServiceProvider.GetRequiredService<ImageStore>();
        var languages = scope.ServiceProvider.GetRequiredService<LanguageService>();

        if (await db.Items.AnyAsync())
        {
            await output.WriteLineAsync("The database already contains items – the demo collection is seeded only into an empty database.");
            return 1;
        }

        var types = _types.ToDictionary(t => t.Key, t => new ItemType { Name = Text(t.En, t.Cs) });
        var countries = _countries.ToDictionary(c => c.Key, c => new Country { Name = Text(c.En, c.Cs) });
        var categories = _categories.ToDictionary(c => c.Key, c => new Category { Name = Text(c.En, c.Cs), Color = c.Color });
        db.ItemTypes.AddRange(types.Values);
        db.Countries.AddRange(countries.Values);
        db.Categories.AddRange(categories.Values);

        var seed = 0;
        foreach (var specimen in _specimens)
        {
            var item = new Item
            {
                Name = Text(specimen.En, specimen.Cs),
                PublicDescription = Text(specimen.DescriptionEn, specimen.DescriptionCs),
                Type = types[specimen.Type],
                Country = countries[specimen.Country],
                YearReleased = specimen.Year,
                Condition = ConditionState.Excellent,
                HasBox = seed % 3 == 0,
                HasCertificate = specimen.Value > 5000,
                HasDocumentation = seed % 2 == 0,
                Visibility = specimen.Public ? ItemVisibility.Public : ItemVisibility.Private,
                IsFeatured = specimen.Featured,
                PurchasePrice = specimen.Price,
                EstimatedValue = specimen.Value,
                PurchaseDate = new DateOnly(2015 + seed % 9, 1 + seed % 12, 1 + seed * 2 % 27),
                PrivateNotes = "Demo data – not a real collection.",
            };
            foreach (var key in specimen.Categories)
            {
                item.Categories.Add(categories[key]);
            }

            var color = SKColor.Parse(specimen.Color);
            for (var photo = 0; photo < 2; photo++)
            {
                var processed = await imageStore.ProcessAsync(new MemoryStream(DemoImages.Draw(specimen.Shape, color, seed, detail: photo == 1)));
                await imageStore.WriteAsync(processed);
                item.Images.Add(new ItemImage
                {
                    FileKey = processed.FileKey,
                    Width = processed.Width,
                    Height = processed.Height,
                    ImageType = photo == 0 ? ImageType.Front : ImageType.Detail,
                    SortOrder = photo,
                    IsPublic = true,
                });
            }

            if (specimen.Offer is { } status)
            {
                item.Listings.Add(new Listing
                {
                    Status = status,
                    AskingPrice = specimen.AskingPrice,
                    AcceptsOffers = true,
                    PublishedAt = DateTime.UtcNow,
                    PublicNote = Text("Shipping within the EU or personal handover.", "Zaslání v rámci EU nebo osobní předání."),
                });
            }

            db.Items.Add(item);
            seed++;
        }

        var settings = await db.SiteSettings.SingleAsync(s => s.Id == SiteSettings.SingletonId);
        settings.SiteName = Text("Cabinet of Curiosities", "Kabinet kuriozit");
        settings.Headline = Text("Minerals, fossils & things from space", "Minerály, zkameněliny a věci z vesmíru");
        settings.Intro = Text(
            "A small private collection of crystals, fossils and meteorites – with the story of every piece.",
            "Malá soukromá sbírka krystalů, zkamenělin a meteoritů – s příběhem každého kusu.");
        settings.About = Text(
            "This is a demo collection generated by the seed-demo command.\n\nAll photos are drawn in code and all prices are made up.",
            "Toto je ukázková sbírka vytvořená příkazem seed-demo.\n\nVšechny fotky jsou nakreslené v kódu a ceny jsou smyšlené.");
        settings.ContactEmail = "collector@example.com";

        await db.SaveChangesAsync();
        await output.WriteLineAsync($"Demo collection seeded: {_specimens.Length} items, {_specimens.Length * 2} photos.");
        return 0;

        LocalizedText Text(string en, string cs) =>
            LocalizedText.Of(languages.Find("en")?.Code ?? languages.Default.Code, en).With("cs", cs);
    }
}
