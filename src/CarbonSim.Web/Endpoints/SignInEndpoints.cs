using System.Globalization;
using System.Net;
using System.Text;
using CarbonSim.Data.Accounts;
using CarbonSim.Web.Accounts;
using CarbonSim.Web.Contracts;
using CarbonSim.Web.Resources;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Endpoints;

/// <summary>
/// How a person signs in, claims a company and signs out from a browser. It is ordinary
/// server-rendered HTML with plain form posts rather than a Blazor screen, because signing in has
/// to write the authentication cookie onto a response, and that is a round trip an already-open
/// interactive circuit cannot make. The labels come from the same resource file as the screens.
/// </summary>
internal static class SignInEndpoints
{
    public static void MapSignInEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/sign-in", PageAsync).AllowAnonymous();
        endpoints.MapPost("/api/accounts/sign-in-form", SignInAsync).AllowAnonymous();
        endpoints.MapPost("/api/accounts/register-form", RegisterAsync).AllowAnonymous();
        // Cast to Delegate so the result is written: taking only an HttpContext, this method would
        // otherwise bind as a RequestDelegate and its redirect would be dropped.
        endpoints.MapGet("/sign-out", (Delegate)SignOutAsync).AllowAnonymous();
    }

    private static async Task<IResult> PageAsync(
        IAntiforgery antiforgery,
        ICompanyRoster roster,
        IStringLocalizer<SharedStrings> strings,
        string? error,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
        IReadOnlyList<string> companies = await roster.ListUnclaimedAsync().ConfigureAwait(false);
        string hidden = Hidden(tokens);

        StringBuilder html = new();
        html.Append("<!DOCTYPE html><html lang=\"")
            .Append(Encode(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName))
            .Append("\"><head><meta charset=\"utf-8\" />")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />")
            .Append("<title>").Append(Encode(strings["SignInTitle"])).Append("</title>")
            .Append("<link rel=\"stylesheet\" href=\"/app.css\" /></head><body><div class=\"plain\">")
            .Append("<h1>").Append(Encode(strings["AppTitle"])).Append("</h1>");

        if (!string.IsNullOrWhiteSpace(error))
        {
            html.Append("<p class=\"notice notice-error\">").Append(Encode(error)).Append("</p>");
        }

        html.Append("<section class=\"card\"><h2>").Append(Encode(strings["SignIn"])).Append("</h2>")
            .Append("<form id=\"sign-in-form\" method=\"post\" action=\"/api/accounts/sign-in-form\">").Append(hidden)
            .Append(Field("email", strings["Email"], "email", required: true))
            .Append(Field("password", strings["Password"], "password", required: true))
            .Append("<button class=\"button\" type=\"submit\">").Append(Encode(strings["SignIn"])).Append("</button>")
            .Append("</form></section>");

        html.Append("<section class=\"card\"><h2>").Append(Encode(strings["Register"])).Append("</h2>")
            .Append("<form id=\"register-form\" method=\"post\" action=\"/api/accounts/register-form\">").Append(hidden)
            .Append(Field("email", strings["Email"], "email", required: true))
            .Append(Field("displayName", strings["DisplayName"], "text", required: true))
            .Append(Field("password", strings["Password"], "password", required: true))
            .Append("<label>").Append(Encode(strings["Company"]))
            .Append("<select name=\"company\" required>");

        foreach (string company in companies)
        {
            html.Append("<option value=\"").Append(Encode(company)).Append("\">").Append(Encode(company)).Append("</option>");
        }

        html.Append("</select></label>")
            .Append(Field("registrationPin", strings["RegistrationPin"], "password", required: true))
            .Append("<button class=\"button\" type=\"submit\">").Append(Encode(strings["Register"])).Append("</button>")
            .Append("</form></section>")
            .Append("</div></body></html>");

        return Results.Content(html.ToString(), "text/html; charset=utf-8");
    }

    private static async Task<IResult> SignInAsync(
        PlayerAccountService accounts,
        [FromForm] string? email,
        [FromForm] string? password,
        [FromForm] string? returnUrl,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AccountOutcome outcome = await accounts
            .LoginAsync(new LoginRequest { Email = email ?? string.Empty, Password = password ?? string.Empty })
            .ConfigureAwait(false);

        if (!outcome.Succeeded || outcome.Account is not PlayerAccount account)
        {
            return Results.Redirect(WithError(outcome.Message));
        }

        await AccountEndpoints.SignInAsync(context, account).ConfigureAwait(false);

        return Results.Redirect(LocalPath(returnUrl));
    }

    private static async Task<IResult> RegisterAsync(
        PlayerAccountService accounts,
        [FromForm] string? email,
        [FromForm] string? displayName,
        [FromForm] string? password,
        [FromForm] string? company,
        [FromForm] string? registrationPin,
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AccountOutcome outcome = await accounts
            .RegisterAsync(new RegisterAccountRequest
            {
                Email = email ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                Password = password ?? string.Empty,
                Company = company ?? string.Empty,
                RegistrationPin = registrationPin ?? string.Empty,
            })
            .ConfigureAwait(false);

        if (!outcome.Succeeded || outcome.Account is not PlayerAccount account)
        {
            return Results.Redirect(WithError(outcome.Message));
        }

        await AccountEndpoints.SignInAsync(context, account).ConfigureAwait(false);

        return Results.Redirect("/");
    }

    private static async Task<IResult> SignOutAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);

        return Results.Redirect("/sign-in");
    }

    private static string WithError(string message) => $"/sign-in?error={Uri.EscapeDataString(message)}";

    private static string Hidden(AntiforgeryTokenSet tokens) =>
        $"<input type=\"hidden\" name=\"{Encode(tokens.FormFieldName)}\" value=\"{Encode(tokens.RequestToken)}\" />";

    private static string Field(string name, string label, string type, bool required)
    {
        return $"<label>{Encode(label)}<input type=\"{Encode(type)}\" name=\"{Encode(name)}\"{(required ? " required" : string.Empty)} /></label>";
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string LocalPath(string? redirect) =>
        redirect is { Length: > 1 } value && value[0] == '/' && value[1] != '/' && value[1] != '\\'
            ? value
            : "/";
}
