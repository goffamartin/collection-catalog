using Microsoft.AspNetCore.Identity;

namespace CollectionCatalog.Web.Data;

/// <summary>
/// User of the administration. Signs in with user name and password (set by the <c>user-set-password</c> command).
/// </summary>
public class AppUser : IdentityUser
{
    [PersonalData]
    public string? DisplayName { get; set; }
}
