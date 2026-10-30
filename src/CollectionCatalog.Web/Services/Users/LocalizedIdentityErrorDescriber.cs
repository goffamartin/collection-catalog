using CollectionCatalog.Web.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace CollectionCatalog.Web.Services.Users;

/// <summary>
/// Translated texts of Identity errors that can be shown to the user (passwords, user name).
/// </summary>
public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<AppText> text) : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = text["The password must have at least {0} characters.", length] };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = text["Incorrect password."] };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = text["User {0} already exists.", userName] };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = text["The user name „{0}“ contains characters that are not allowed.", userName ?? ""] };
}
