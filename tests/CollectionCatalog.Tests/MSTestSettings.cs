using System.Globalization;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace CollectionCatalog.Tests;

[TestClass]
public static class TestCulture
{
    /// <summary>
    /// Services read the language from the UI culture; tests run in English (the default language), independent of the machine.
    /// </summary>
    [AssemblyInitialize]
    public static void Initialize(TestContext _)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");
    }
}
