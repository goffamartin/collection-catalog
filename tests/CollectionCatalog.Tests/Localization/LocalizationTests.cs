using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Resources;

namespace CollectionCatalog.Tests.Localization;

/// <summary>Translatable texts and plural forms of UI texts.</summary>
[TestClass]
public sealed class LocalizationTests
{
    private static readonly LanguageContext _english = new("en", "cs");
    private static readonly LanguageContext _czech = new("cs", "cs");

    [TestMethod]
    public void LocalizedText_FallsBackToDefaultLanguage_ThenToAnyLanguage()
    {
        var text = LocalizedText.Of("cs", "Křemen").With("en", "Quartz");

        Assert.AreEqual("Quartz", text.Get(_english));
        Assert.AreEqual("Křemen", text.Get(_czech));
        Assert.AreEqual("Křemen", LocalizedText.Of("cs", "Křemen").Get(_english), "missing translation shows the default language");
        Assert.AreEqual("Quarz", LocalizedText.Of("de", "Quarz").Get(_english), "with no default either, any language beats nothing");
        Assert.IsNull(LocalizedText.Empty.Get(_english));
    }

    [TestMethod]
    public void LocalizedText_With_TrimsAndRemovesEmptyValues()
    {
        var text = LocalizedText.Of("cs", "  Křemen  ").With("en", "Quartz");

        Assert.AreEqual("Křemen", text["cs"]);
        Assert.IsNull(text.With("en", "   ")["en"]);
        Assert.IsTrue(LocalizedText.Of("cs", null).IsEmpty);
    }

    [TestMethod]
    public void LocalizedText_JsonRoundTrip_KeepsCzechLettersReadable()
    {
        var text = LocalizedText.Of("cs", "Ametyst „Žíhaný“").With("en", "Amethyst \"Banded\"");

        var json = text.ToJson();

        StringAssert.Contains(json, "Ametyst „Žíhaný“");
        Assert.AreEqual(text, LocalizedText.FromJson(json));
        Assert.AreEqual(LocalizedText.Empty, LocalizedText.FromJson("{}"));
        Assert.AreEqual(LocalizedText.Empty, LocalizedText.FromJson(null));
    }

    [TestMethod]
    public void LocalizedText_Equality_IgnoresOrderOfLanguages()
    {
        var a = LocalizedText.Of("cs", "Křemen").With("en", "Quartz");
        var b = LocalizedText.Of("en", "Quartz").With("cs", "Křemen");

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.AreNotEqual(a, b.With("en", "Rock crystal"));
    }

    [TestMethod]
    [DataRow("cs", 1, "1 fotka")]
    [DataRow("cs", 3, "3 fotky")]
    [DataRow("cs", 5, "5 fotek")]
    [DataRow("cs", 0, "0 fotek")]
    [DataRow("en", 1, "1 photo")]
    [DataRow("en", 2, "2 photos")]
    public void Plural_PicksFormByLanguageRules(string culture, int count, string expected)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Assert.AreEqual(expected, TestEnvironment.Text.Plural("{0} photo|{0} photos", count));
        }
        finally
        {
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void CzechResources_HaveNoKeysDifferingOnlyInCase()
    {
        // .resources lookups collide on keys like "Collection" / "collection" – one of them then never translates.
        var directory = new DirectoryInfo(Path.GetDirectoryName(SourcePath())!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CollectionCatalog.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.IsNotNull(directory, "repository root not found");
        var resx = XDocument.Load(Path.Combine(directory.FullName, "src", "CollectionCatalog.Web", "Resources", "AppText.cs.resx"));

        var clashes = resx.Root!.Elements("data")
            .Select(d => (string)d.Attribute("name")!)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" / ", g))
            .ToList();

        Assert.IsEmpty(clashes, string.Join(", ", clashes));
    }

    [TestMethod]
    [DataRow("cs", true)]
    [DataRow("en", true)]
    [DataRow("de", false)]
    public void AppLanguages_DetectTranslationFromResourceFile(string code, bool translated)
    {
        // English is the source text in code, Czech has AppText.cs.resx, German has no file (yet).
        Assert.AreEqual(translated, AppLanguages.IsTranslated(code));
    }

    /// <summary>Path of this source file – tests may run from a build output outside the repository.</summary>
    private static string SourcePath([CallerFilePath] string path = "") => path;

    [TestMethod]
    public void EnumLabels_UseContextForCzechGrammar()
    {
        Assert.AreEqual("Sold", ListingStatus.Sold.Label(), "English shows the text without the context");
        Assert.AreEqual("Front", ImageType.Front.Label());

        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("cs");
        try
        {
            Assert.AreEqual("Prodaná", ListingStatus.Sold.Label());
            Assert.AreEqual("Nová", InquiryStatus.New.Label());
            Assert.AreEqual("Veřejný", ItemVisibility.Public.Label());
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
