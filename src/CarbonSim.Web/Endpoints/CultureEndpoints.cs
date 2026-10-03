using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace CarbonSim.Web.Endpoints;

/// <summary>
/// The language switch. It writes the culture cookie and sends the player back where they were, so
/// a screen keeps its run and its page when the language changes.
/// </summary>
internal static class CultureEndpoints
{
    public static void MapCultureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/culture/set", SetAsync).AllowAnonymous();
    }

    private static IResult SetAsync(HttpContext context, string? culture, string? redirect)
    {
        ArgumentNullException.ThrowIfNull(context);

        string chosen = culture is not null && CarbonSimCultures.Supported.Contains(culture, StringComparer.OrdinalIgnoreCase)
            ? culture.ToLowerInvariant()
            : CarbonSimCultures.Default;

        context.Response.Cookies.Append(
            CarbonSimCultures.CookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(chosen)),
            new CookieOptions
            {
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
            });

        return Results.Redirect(LocalPath(redirect));
    }

    /// <summary>Only a path inside this host is followed, so the switch cannot be used to bounce elsewhere.</summary>
    private static string LocalPath(string? redirect) =>
        redirect is { Length: > 1 } value
            && value[0] == '/'
            && value[1] != '/'
            && value[1] != '\\'
                ? value
                : "/";
}
