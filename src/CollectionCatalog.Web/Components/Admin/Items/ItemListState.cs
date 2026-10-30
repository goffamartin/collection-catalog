using System.Globalization;
using CollectionCatalog.Web.Components.Admin.Shared;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Infrastructure;
using CollectionCatalog.Web.Services.Items;
using CollectionCatalog.Web.Services.Text;

namespace CollectionCatalog.Web.Components.Admin.Items;

/// <summary>
/// Filters and sorting of the item list and their URL representation – returning from the detail (even with Back) restores them.
/// </summary>
public sealed class ItemListState
{
    /// <summary>Names of the URL parameters.</summary>
    public static class Keys
    {
        public const string Search = "hledat";
        public const string Type = "typ";
        public const string Country = "zeme";
        public const string Category = "kategorie";
        public const string Visibility = "viditelnost";
        public const string Sold = "prodej";
        public const string Images = "fotky";
        public const string Sort = "razeni";
        public const string Direction = "smer";
        public const string Page = "strana";
    }

    // Three-state filters as text: Blazor cannot reliably match bool? with <option> values of a <select>.
    public const string Yes = "ano";
    public const string No = "ne";

    private const string Descending = "sestupne";


    private static readonly (string Key, SortColumn Column)[] _sortKeys =
    [
        ("nazev", SortColumn.Name), ("typ", SortColumn.Type), ("zeme", SortColumn.Country),
        ("obdobi", SortColumn.Year), ("nakup", SortColumn.PurchasePrice), ("odhad", SortColumn.EstimatedValue),
    ];

    private static readonly (string Key, ItemVisibility Value)[] _visibilityKeys =
    [
        ("verejny", ItemVisibility.Public), ("soukromy", ItemVisibility.Private),
    ];

    public string? Search { get; set; }
    public int? TypeId { get; set; }
    public int? CountryId { get; set; }
    public int? CategoryId { get; set; }
    public ItemVisibility? Visibility { get; set; }

    /// <summary><see cref="Yes"/> = sold only, <see cref="No"/> = in the collection only, <c>null</c> = all.</summary>
    public string? Sold
    {
        get;
        set => field = YesNo(value);
    }

    /// <summary><see cref="Yes"/> = with photos only, <see cref="No"/> = without photos only, <c>null</c> = all.</summary>
    public string? HasImages
    {
        get;
        set => field = YesNo(value);
    }

    public SortColumn Sort { get; set; } = SortColumn.Name;
    public bool SortDescending { get; set; }

    public bool HasFilter => !string.IsNullOrWhiteSpace(Search) || TypeId is not null || CountryId is not null
        || CategoryId is not null || Visibility is not null || Sold is not null || HasImages is not null;

    /// <summary>Fingerprint of the filters (without sorting) – tells when the rows must be loaded again.</summary>
    public string FilterKey => string.Join("|", Search, TypeId, CountryId, CategoryId, Visibility, Sold, HasImages);

    public AdminItemFilter ToFilter() => new()
    {
        Search = Search,
        TypeId = TypeId,
        CountryId = CountryId,
        CategoryId = CategoryId,
        Visibility = Visibility,
        Sold = ToBool(Sold),
        HasImages = ToBool(HasImages),
    };

    public void ClearFilter()
    {
        Search = null;
        TypeId = CountryId = CategoryId = null;
        Visibility = null;
        Sold = HasImages = null;
    }

    /// <summary>Click on a column header: the same column reverses the direction, another one sorts ascending.</summary>
    public void SortBy(SortColumn column)
    {
        SortDescending = Sort == column && !SortDescending;
        Sort = column;
    }

    /// <summary>Reads the state from the URL parameters; unknown or hand-broken values are ignored.</summary>
    public void ReadFrom(Func<string, string?> query, bool includeSearch = true)
    {
        if (includeSearch)
        {
            Search = query(Keys.Search);
        }
        TypeId = QueryValue.Int(query(Keys.Type));
        CountryId = QueryValue.Int(query(Keys.Country));
        CategoryId = QueryValue.Int(query(Keys.Category));
        var visibility = query(Keys.Visibility);
        Visibility = _visibilityKeys.Where(k => k.Key == visibility).Select(k => (ItemVisibility?)k.Value).FirstOrDefault();
        Sold = query(Keys.Sold);
        HasImages = query(Keys.Images);
        Sort = _sortKeys.FirstOrDefault(k => k.Key == query(Keys.Sort)).Column;
        SortDescending = query(Keys.Direction) == Descending;
    }

    /// <summary>URL parameters of the current state (defaults are omitted); <paramref name="page"/> is zero-based.</summary>
    public Dictionary<string, object?> ToQuery(int page = 0) => new()
    {
        [Keys.Search] = string.IsNullOrWhiteSpace(Search) ? null : Search,
        [Keys.Type] = TypeId,
        [Keys.Country] = CountryId,
        [Keys.Category] = CategoryId,
        [Keys.Visibility] = Visibility is { } visibility ? _visibilityKeys.First(k => k.Value == visibility).Key : null,
        [Keys.Sold] = Sold,
        [Keys.Images] = HasImages,
        [Keys.Sort] = Sort == SortColumn.Name ? null : _sortKeys.First(k => k.Column == Sort).Key,
        [Keys.Direction] = SortDescending ? Descending : null,
        [Keys.Page] = page > 0 ? (page + 1).ToString(CultureInfo.InvariantCulture) : null,
    };

    /// <summary>Sorts the rows by the chosen column; equal values always by name.</summary>
    public IEnumerable<AdminItemRow> Apply(IEnumerable<AdminItemRow> rows)
    {
        var ordered = Sort switch
        {
            SortColumn.Type => OrderBy(rows, r => r.TypeName ?? "", TextComparer.Current),
            SortColumn.Country => OrderBy(rows, r => r.CountryName ?? "", TextComparer.Current),
            SortColumn.Year => OrderBy(rows, r => r.YearReleased ?? int.MaxValue),
            SortColumn.Condition => OrderBy(rows, r => r.Condition),
            SortColumn.PurchasePrice => OrderBy(rows, r => r.PurchasePrice ?? 0),
            SortColumn.EstimatedValue => OrderBy(rows, r => r.EstimatedValue ?? 0),
            _ => OrderBy(rows, r => r.Name, TextComparer.Current),
        };
        return ordered.ThenBy(r => r.Name, TextComparer.Current);
    }

    private IOrderedEnumerable<AdminItemRow> OrderBy<TKey>(IEnumerable<AdminItemRow> rows, Func<AdminItemRow, TKey> key, IComparer<TKey>? comparer = null) =>
        SortDescending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer);

    private static bool? ToBool(string? value) => value switch { Yes => true, No => false, _ => null };

    /// <summary>The empty option of a &lt;select&gt; and garbage from the URL both mean no filter.</summary>
    private static string? YesNo(string? value) => value is Yes or No ? value : null;
}
