using CollectionCatalog.Web.Domain;

namespace CollectionCatalog.Web.Services.Languages;

/// <summary>
/// Base of edit models with translatable fields. The form edits one language at a time (<see cref="EditLanguage"/>):
/// string properties bound to inputs read and write the value of that language in a <see cref="LocalizedText"/>.
/// </summary>
/// <remarks>Switching the language only changes which values the inputs show – nothing is lost or marked as changed.</remarks>
public abstract class TranslatableModel
{
    /// <summary>Language being edited.</summary>
    public string EditLanguage { get; set; } = "";

    /// <summary>The default language – required texts must be filled in it, other languages fall back to it.</summary>
    public string DefaultLanguage { get; set; } = "";

    public bool EditsDefaultLanguage => EditLanguage == DefaultLanguage;

    /// <summary>Starts editing in the default language.</summary>
    public void SetLanguages(LanguageContext language)
    {
        DefaultLanguage = language.DefaultCode;
        EditLanguage = language.DefaultCode;
    }

    /// <summary>Text of the default language as a hint (placeholder) while translating; <c>null</c> in the default language.</summary>
    public string? Fallback(LocalizedText text) => EditsDefaultLanguage ? null : text[DefaultLanguage];

    protected string? Read(LocalizedText text) => text[EditLanguage];

    protected LocalizedText Write(LocalizedText text, string? value) => text.With(EditLanguage, value);
}
