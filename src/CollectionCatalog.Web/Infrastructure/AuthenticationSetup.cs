using CollectionCatalog.Web.Components.Account;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Services.Users;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace CollectionCatalog.Web.Infrastructure;

public static class AuthenticationSetup
{
    public const int MinPasswordLength = 8;

    /// <summary>
    /// ASP.NET Core Identity with sign-in by user name and password. There is no registration –
    /// accounts are created by the CLI command <c>user-set-password</c>.
    /// </summary>
    public static WebApplicationBuilder AddAppAuthentication(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
        services.AddScoped<IdentityRedirectManager>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        services.AddIdentityCore<AppUser>(options =>
            {
                // v3 also contains the passkeys table – unused for now, kept for a possible later switch-on.
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

                // Length instead of complexity: a longer phrase is easier to remember.
                options.Password.RequiredLength = MinPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "catalog.auth";
            // Sign-in is shared by all languages – by default the cookie would be scoped to the language prefix (/en).
            options.Cookie.Path = "/";
            options.LoginPath = "/prihlaseni";
            options.AccessDeniedPath = "/prihlaseni";
            // "Stay signed in" – the administration is used from a phone, signing in every day would be a nuisance.
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
        });

        services.AddAuthorization();

        return builder;
    }
}
