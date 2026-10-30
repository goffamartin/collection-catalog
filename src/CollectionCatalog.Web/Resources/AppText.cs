using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Localization;

namespace CollectionCatalog.Web.Resources;

/// <summary>
/// Marker for the UI text resources (<c>IStringLocalizer&lt;AppText&gt;</c>, in components as <c>T["…"]</c>). Keys are the
/// English texts used directly in code; translations live in <c>AppText.{culture}.resx</c> (e.g. <c>AppText.cs.resx</c>).
/// A missing translation shows the English key, so the UI never shows a blank.
/// </summary>
public sealed class AppText
{
    private static readonly ResourceManager _resources = new(typeof(AppText).FullName!, typeof(AppText).Assembly);

    /// <summary>
    /// Text in the current UI culture for code without dependency injection (enum labels) –
    /// the same resources as <c>IStringLocalizer&lt;AppText&gt;</c>.
    /// </summary>
    public static string Get(string key, params object[] arguments)
    {
        var text = _resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        return arguments.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, arguments);
    }

    /// <summary>
    /// Text whose translation depends on context – the key is <c>"Sold [listing]"</c> (Czech: offer „Prodaná“ vs. item
    /// „Prodáno“). Without a translation the text itself is shown, without the context.
    /// </summary>
    public static string In(string context, string text) =>
        _resources.GetString($"{text} [{context}]", CultureInfo.CurrentUICulture) ?? text;
}

/// <summary>
/// Which languages the texts of the application itself (menus, buttons, messages) are translated to. They are not
/// edited in the administration: English is the source text in code, any other language needs its
/// <c>Resources/AppText.{code}.resx</c>. A language added to the codebook picks the file up automatically;
/// without it the application texts show in English (website content still uses the language).
/// </summary>
public static class AppLanguages
{
    /// <summary>English – the texts written in code.</summary>
    public const string Source = "en";

    private static readonly ResourceManager _resources = new(typeof(AppText).FullName!, typeof(AppText).Assembly);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _cache = new();

    /// <summary>Has the application a resource file for the language (or is it the source language)?</summary>
    public static bool IsTranslated(string code) => _cache.GetOrAdd(code, c =>
    {
        if (c == Source)
        {
            return true;
        }
        try
        {
            // tryParents: false – only the language's own file counts, not the English fallback.
            return _resources.GetResourceSet(CultureInfo.GetCultureInfo(c), createIfNotExists: true, tryParents: false) is not null;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    });

    /// <summary>File holding the application texts of a language.</summary>
    public static string ResourceFile(string code) => $"Resources/AppText.{code}.resx";
}

public static class PluralExtensions
{
    /// <summary>
    /// Count with the right plural form, e.g. <c>T.Plural("{0} photo|{0} photos", n)</c>. The resource holds the forms
    /// separated by <c>|</c> in the order of the language's plural rules – English one|other, Czech one|few (2–4)|many.
    /// </summary>
    public static string Plural(this IStringLocalizer localizer, string forms, int count) =>
        PickForm(localizer[forms].Value, count);

    /// <summary>The same for code without dependency injection.</summary>
    public static string Plural(string forms, int count) => PickForm(AppText.Get(forms), count);

    private static string PickForm(string forms, int count)
    {
        var variants = forms.Split('|');
        var index = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            // Czech (and Slovak): 1 | 2–4 | 0, 5+
            "cs" or "sk" => count == 1 ? 0 : count is >= 2 and <= 4 ? 1 : 2,
            _ => count == 1 ? 0 : 1,
        };
        var form = variants[Math.Min(index, variants.Length - 1)];
        return string.Format(CultureInfo.CurrentCulture, form, count.ToString("N0", CultureInfo.CurrentCulture));
    }
}
