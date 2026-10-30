using CollectionCatalog.Web.Services.Text;

namespace CollectionCatalog.Tests.Public;

[TestClass]
public sealed class SearchTextTests
{
    [TestMethod]
    [DataRow("Ametystová geoda z Brazílie", "ametystova geoda z brazilie")]
    [DataRow("ŠPERK", "sperk")]
    [DataRow("", "")]
    [DataRow(null, "")]
    public void Normalize_IgnoresCaseAndDiacritics(string? text, string expected)
    {
        Assert.AreEqual(expected, SearchText.Normalize(text));
    }

    [TestMethod]
    [DataRow("geo", true)]
    [DataRow("geoda amet", true)]
    [DataRow("braz", true)]
    [DataRow("eoda", false)]
    [DataRow("geoda eoda", false)]
    [DataRow("1939", true)]
    public void Matches_EveryWordMustStartSomeWord(string query, bool expected)
    {
        var texts = new[] { "Ametystová geoda", "Brazílie (1939–1945)", null };

        Assert.AreEqual(expected, SearchText.Matches(SearchText.Normalize(query), texts));
    }
}
