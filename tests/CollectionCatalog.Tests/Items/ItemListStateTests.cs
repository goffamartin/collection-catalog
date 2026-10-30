using System.Globalization;
using CollectionCatalog.Web.Components.Admin.Items;
using CollectionCatalog.Web.Components.Admin.Shared;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Items;

namespace CollectionCatalog.Tests.Items;

[TestClass]
public sealed class ItemListStateTests
{
    [TestMethod]
    public void Query_RoundTripsAllFilters()
    {
        var state = new ItemListState
        {
            Search = "geode",
            TypeId = 1,
            CountryId = 2,
            CategoryId = 3,
            Visibility = ItemVisibility.Private,
            Sold = ItemListState.Yes,
            HasImages = ItemListState.No,
            Sort = SortColumn.EstimatedValue,
            SortDescending = true,
        };

        var query = state.ToQuery(page: 2);
        var restored = new ItemListState();
        restored.ReadFrom(key => query[key]?.ToString());

        Assert.AreEqual(state.FilterKey, restored.FilterKey);
        Assert.AreEqual(SortColumn.EstimatedValue, restored.Sort);
        Assert.IsTrue(restored.SortDescending);
        Assert.AreEqual("3", query[ItemListState.Keys.Page], "pages are one-based in the address");
    }

    [TestMethod]
    public void DefaultState_LeavesQueryEmpty()
    {
        Assert.IsTrue(new ItemListState().ToQuery().Values.All(v => v is null));
    }

    [TestMethod]
    public void ReadFrom_IgnoresGarbage()
    {
        var state = new ItemListState();

        state.ReadFrom(key => key == ItemListState.Keys.Search ? null : "x1");

        Assert.IsFalse(state.HasFilter);
        Assert.AreEqual(SortColumn.Name, state.Sort);
        Assert.IsFalse(state.SortDescending);
    }

    [TestMethod]
    public void EmptySelectOption_MeansNoFilter()
    {
        var state = new ItemListState { Sold = "", HasImages = "" };

        Assert.IsFalse(state.HasFilter);
        Assert.IsNull(state.ToFilter().Sold);
    }

    [TestMethod]
    public void SortBy_SameColumnTogglesDirection()
    {
        var state = new ItemListState();

        state.SortBy(SortColumn.Year);
        Assert.IsFalse(state.SortDescending);
        state.SortBy(SortColumn.Year);
        Assert.IsTrue(state.SortDescending);
        state.SortBy(SortColumn.Name);
        Assert.IsFalse(state.SortDescending);
    }

    [TestMethod]
    public void Apply_SortsByTheRulesOfTheLanguage_ThenByName()
    {
        var rows = new[] { Row("Čestný kámen", "B"), Row("Cena", "A"), Row("Hvězda", "A"), Row("Chalcedon", "A") };

        // English: diacritics are ignored, "Ch" is just C + h.
        Assert.AreEqual("Cena,Čestný kámen,Chalcedon,Hvězda", Sorted(new ItemListState(), rows));

        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("cs");
        try
        {
            // Czech: "Č" after "C", "Ch" after "H".
            Assert.AreEqual("Cena,Čestný kámen,Hvězda,Chalcedon", Sorted(new ItemListState(), rows));
            Assert.AreEqual("Cena,Hvězda,Chalcedon,Čestný kámen", Sorted(new ItemListState { Sort = SortColumn.Type }, rows));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    private static string Sorted(ItemListState state, IEnumerable<AdminItemRow> rows) =>
        string.Join(",", state.Apply(rows).Select(r => r.Name));

    private static AdminItemRow Row(string name, string type) => new() { Name = name, TypeName = type };
}
