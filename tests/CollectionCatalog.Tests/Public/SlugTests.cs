using CollectionCatalog.Web.Services.Text;

namespace CollectionCatalog.Tests.Public;

[TestClass]
public sealed class SlugTests
{
    [TestMethod]
    [DataRow("Ametystová geoda z Brazílie", "ametystova-geoda-z-brazilie")]
    [DataRow("Křemenná drúza 2001–2003", "kremenna-druza-2001-2003")]
    [DataRow("  „Zlatý řez“ (ČR) ", "zlaty-rez-cr")]
    [DataRow("!!!", "predmet")]
    [DataRow("", "predmet")]
    public void From_CreatesUrlFriendlyText(string name, string expected)
    {
        Assert.AreEqual(expected, Slug.From(name));
    }

    [TestMethod]
    public void From_LimitsLength()
    {
        var slug = Slug.From(string.Join(' ', Enumerable.Repeat("chalcedony", 20)));

        Assert.IsLessThanOrEqualTo(80, slug.Length);
        Assert.IsFalse(slug.EndsWith('-'));
    }
}
