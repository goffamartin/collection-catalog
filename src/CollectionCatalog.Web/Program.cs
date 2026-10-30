using CollectionCatalog.Web.Components;
using CollectionCatalog.Web.Components.Account;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Infrastructure;
using CollectionCatalog.Web.Services.Codebooks;
using CollectionCatalog.Web.Services.Inquiries;
using CollectionCatalog.Web.Services.Items;
using CollectionCatalog.Web.Services.Languages;
using CollectionCatalog.Web.Services.Listings;
using CollectionCatalog.Web.Services.Public;
using CollectionCatalog.Web.Services.Site;
using CollectionCatalog.Web.Services.Users;
using Microsoft.AspNetCore.HttpOverrides;

// CLI commands are not passed to the configuration (arguments like --dry-run would be parsed as keys).
var isCommand = CliCommands.IsCommand(args);
var builder = WebApplication.CreateBuilder(isCommand ? [] : args);

builder.AddStorage();
builder.AddAppAuthentication();

builder.Services.AddScoped<AdminItemQuery>();
builder.Services.AddScoped<ItemEditorService>();
builder.Services.AddScoped<CodebookService>();
builder.Services.AddScoped<PublicItemQuery>();
builder.Services.AddScoped<ListingService>();
builder.Services.AddScoped<InquiryService>();
builder.Services.AddSingleton<InquiryRateLimiter>();
builder.Services.AddSingleton<LoginAttemptLimiter>();
builder.Services.AddHostedService<InquiryRetentionService>();
builder.Services.AddSingleton<SiteSettingsService>();
builder.Services.AddSingleton<LanguageService>();
builder.Services.AddLocalization();
var siteOptions = builder.Configuration.GetSection(SiteOptions.SectionName).Get<SiteOptions>() ?? new SiteOptions();
builder.Services.AddSingleton(siteOptions);
AppTime.Configure(siteOptions.TimeZone);
Money.Configure(siteOptions.Currency);
builder.Services.AddScoped<CollectionCatalog.Web.Components.Admin.Shared.ListNavigation>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The only cookie of the public website (form protection) – over HTTPS with the Secure flag (the default does not set it).
// X-Frame-Options is sent by SecurityHeaders (DENY) – antiforgery would otherwise add a second, conflicting SAMEORIGIN.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    // One cookie for all languages – by default it would be scoped to the language prefix (/en).
    options.Cookie.Path = "/";
    options.SuppressXFrameOptionsHeader = true;
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// The visitor's IP feeds the per-IP limits of sign-in and inquiries, so forwarded headers are trusted only from a proxy.
// By default only from a reverse proxy on the same machine (loopback, the ASP.NET Core default).
// Proxy:CloudflareTunnel = true – the app runs behind a Cloudflare Tunnel, which terminates TLS, and is not reachable from
// the internet directly, so proxies are not restricted to specific addresses. The visitor's IP comes from CF-Connecting-IP:
// Cloudflare always sets it itself (overwriting a client's value), whereas a client can append to X-Forwarded-For.
// Without the tunnel the header would come straight from the client – never enable it then.
var behindCloudflareTunnel = builder.Configuration.GetValue<bool>("Proxy:CloudflareTunnel");
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (behindCloudflareTunnel)
    {
        options.ForwardedForHeaderName = "CF-Connecting-IP";
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

var app = builder.Build();

await app.InitializeDatabaseAsync();
await app.Services.GetRequiredService<LanguageService>().LoadAsync();

if (isCommand)
{
    return await CliCommands.RunAsync(app.Services, args, Console.Out);
}

app.UseForwardedHeaders();
// Language prefix (/en/…) → path base and culture. Before everything that looks at the path (noindex, routing)
// and before the error pages, whose re-execution must keep the detected language.
app.UseLanguagePath();
app.UsePrivatePagesNoIndex();
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseRouting();

// Explicitly after UseForwardedHeaders – otherwise WebApplication adds them automatically at the start of the pipeline
// and sign-in redirects and cookies would use the http scheme instead of https (behind a TLS-terminating proxy).
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapAccountEndpoints();
app.MapMediaEndpoints();
app.MapSeoEndpoints();
app.MapRazorComponents<App>()
    // CSP frame-ancestors is sent by SecurityHeaders ('none') – Blazor would otherwise add a second header with 'self'.
    .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = null);

await app.RunAsync();
return 0;
