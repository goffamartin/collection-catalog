using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Infrastructure;
using CollectionCatalog.Web.Services.Inquiries;
using CollectionCatalog.Web.Services.Text;

namespace CollectionCatalog.Tests.Inputs;

/// <summary>Helpers guarding input from outside (return address, e-mail, URL parameters, free text).</summary>
[TestClass]
public sealed class InputHelpersTests
{
    [TestMethod]
    [DataRow("admin", true)]
    [DataRow("/admin/predmety?typ=1#x", true)]
    [DataRow("sbirka/12-amethyst-geode", true)]
    [DataRow("//evil.example/admin", false)]
    [DataRow("/\\evil.example", false)]
    [DataRow("\\\\evil.example", false)]
    [DataRow("https://evil.example", false)]
    [DataRow("javascript:alert(1)", false)]
    [DataRow("/admin\r\nLocation: x", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void LocalUrl_AcceptsOnlyRelativeAddressesOfThisSite(string? url, bool expected)
    {
        Assert.AreEqual(expected, LocalUrl.IsLocal(url));
    }

    [TestMethod]
    [DataRow("jan.novak@example.cz", true)]
    [DataRow("a+b@sub.example.com", true)]
    [DataRow("jan@localhost", false)]
    [DataRow("jan@example.cz?cc=x@y.cz", false)]
    [DataRow("jan@example.cz&body=x", false)]
    [DataRow("\"Jan\" <jan@example.cz>", false)]
    [DataRow("jan novak@example.cz", false)]
    [DataRow("jan@example.cz,eva@example.cz", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void StrictEmail_RejectsAddressesThatCouldTamperWithMailto(string? email, bool expected)
    {
        Assert.AreEqual(expected, StrictEmailAttribute.IsValid(email));
    }

    [TestMethod]
    [DataRow("42", 42)]
    [DataRow(" 7 ", null)]
    [DataRow("abc", null)]
    [DataRow("1e3", null)]
    [DataRow("99999999999", null)]
    [DataRow("", null)]
    [DataRow(null, null)]
    public void QueryValue_ParsesOnlyPlainIntegers(string? value, int? expected)
    {
        Assert.AreEqual(expected, QueryValue.Int(value));
    }

    [TestMethod]
    public void AppTime_UsesConfiguredZoneIncludingDaylightSaving()
    {
        AppTime.Configure("Europe/Prague");
        try
        {
            Assert.AreEqual(new DateTime(2026, 7, 1, 0, 30, 0), AppTime.ToLocal(new DateTime(2026, 6, 30, 22, 30, 0, DateTimeKind.Utc)));
            Assert.AreEqual(new DateTime(2026, 1, 1, 0, 30, 0), AppTime.ToLocal(new DateTime(2025, 12, 31, 23, 30, 0, DateTimeKind.Utc)));

            // Late evening UTC is already the next day in Prague.
            var time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 24, 22, 30, 0, TimeSpan.Zero));
            Assert.AreEqual(new DateOnly(2026, 10, 25), AppTime.Today(time));
        }
        finally
        {
            AppTime.Configure("UTC");
        }
    }

    [TestMethod]
    [DataRow("  popisek  ", 10, "popisek")]
    [DataRow("   ", 10, null)]
    [DataRow(null, 10, null)]
    [DataRow("abcdefghij", 10, "abcdefghij")]
    [DataRow("abcde fghijkl", 6, "abcde")]
    public void TextLimit_TrimsAndClamps(string? text, int maxLength, string? expected)
    {
        Assert.AreEqual(expected, TextLimit.Clean(text, maxLength));
    }
}
