using CollectionCatalog.Web.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CollectionCatalog.Web.Components.Account;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/ucet/odhlasit", async (HttpContext context, [FromServices] IAntiforgery antiforgery, [FromServices] SignInManager<AppUser> signInManager) =>
        {
            // Minimal APIs reject a request without an antiforgery token only when binding a form, which is not the case here –
            // validate explicitly, otherwise a foreign page could sign the user out (LogoutButton sends the token).
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest();
            }

            await signInManager.SignOutAsync();
            return Results.LocalRedirect("~/");
        });

        return endpoints;
    }
}
