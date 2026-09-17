using System.Globalization;
using System.Security.Claims;
using CarbonSim.Data.Accounts;
using CarbonSim.Web.Accounts;
using CarbonSim.Web.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CarbonSim.Web.Endpoints;

/// <summary>
/// The account surface of the host: claim a company, sign in and out, and recover a password with
/// an emailed code. Everything is JSON, so the Blazor screens of the next phase call the same
/// endpoints the tests do.
/// </summary>
internal static class AccountEndpoints
{
    private const string ResetRequestAcceptedMessage =
        "If that address has an account, a reset code is on its way to it.";

    public static void MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder accounts = endpoints.MapGroup("/api/accounts");

        accounts.MapPost("/register", RegisterAsync).AllowAnonymous();
        accounts.MapPost("/login", LoginAsync).AllowAnonymous();
        accounts.MapPost("/logout", LogoutAsync).RequireAuthorization();
        accounts.MapGet("/me", ReadMeAsync).RequireAuthorization();
        accounts.MapGet("/companies", ListCompaniesAsync).AllowAnonymous();
        accounts.MapPost("/password-reset/request", RequestPasswordResetAsync).AllowAnonymous();
        accounts.MapPost("/password-reset/confirm", ConfirmPasswordResetAsync).AllowAnonymous();
    }

    private static async Task<IResult> RegisterAsync(
        RegisterAccountRequest? request,
        PlayerAccountService accounts,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new ErrorResponse("A registration body is required."));
        }

        AccountOutcome outcome = await accounts.RegisterAsync(request, cancellationToken).ConfigureAwait(false);

        return outcome is { Succeeded: true, Account: PlayerAccount account }
            ? Results.Created($"/api/accounts/{account.Id}", AccountResponse.From(account))
            : Refusal(outcome);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest? request,
        PlayerAccountService accounts,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new ErrorResponse("A login body is required."));
        }

        AccountOutcome outcome = await accounts.LoginAsync(request, cancellationToken).ConfigureAwait(false);

        if (!outcome.Succeeded || outcome.Account is not PlayerAccount account)
        {
            return Refusal(outcome);
        }

        await SignInAsync(httpContext, account).ConfigureAwait(false);

        return Results.Ok(AccountResponse.From(account));
    }

    private static async Task LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);

        httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static async Task<IResult> ReadMeAsync(
        ClaimsPrincipal user,
        IAccountStore store,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out int accountId))
        {
            return Results.Unauthorized();
        }

        PlayerAccount? account = await store.FindByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        return account is null ? Results.Unauthorized() : Results.Ok(AccountResponse.From(account));
    }

    private static async Task<IResult> ListCompaniesAsync(
        ICompanyRoster roster,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> companies = await roster.ListUnclaimedAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(companies);
    }

    private static async Task<IResult> RequestPasswordResetAsync(
        PasswordResetRequest? request,
        PlayerAccountService accounts,
        CancellationToken cancellationToken)
    {
        if (request is not null)
        {
            await accounts.RequestPasswordResetAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // The same answer whatever happened, so the reply cannot be used to find out which
        // addresses are registered.
        return Results.Json(
            new AcknowledgmentResponse(ResetRequestAcceptedMessage),
            statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> ConfirmPasswordResetAsync(
        ConfirmPasswordResetRequest? request,
        PlayerAccountService accounts,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new ErrorResponse("A password reset body is required."));
        }

        AccountOutcome outcome = await accounts.ConfirmPasswordResetAsync(request, cancellationToken).ConfigureAwait(false);

        return outcome.Succeeded ? Results.NoContent() : Refusal(outcome);
    }

    /// <summary>Signs the account in by writing its role into the authentication cookie.</summary>
    private static Task SignInAsync(HttpContext httpContext, PlayerAccount account)
    {
        ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, account.DisplayName),
                new Claim(ClaimTypes.Email, account.Email),
                new Claim(ClaimTypes.Role, account.Role.ToString()),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    /// <summary>Maps a refusal onto the status code that tells the caller what to fix.</summary>
    private static IResult Refusal(AccountOutcome outcome)
    {
        return outcome.Problem switch
        {
            AccountProblem.InvalidRequest => Results.BadRequest(new ErrorResponse(outcome.Message)),
            AccountProblem.CompanyUnavailable => Results.BadRequest(new ErrorResponse(outcome.Message)),
            AccountProblem.ResetCodeRejected => Results.BadRequest(new ErrorResponse(outcome.Message)),
            AccountProblem.AddressOrCompanyTaken => Results.Conflict(new ErrorResponse(outcome.Message)),
            AccountProblem.InvalidCredentials => Results.Json(
                new ErrorResponse(outcome.Message),
                statusCode: StatusCodes.Status401Unauthorized),
            AccountProblem.WrongRegistrationPin => Results.Json(
                new ErrorResponse(outcome.Message),
                statusCode: StatusCodes.Status403Forbidden),
            AccountProblem.RegistrationClosed => Results.Json(
                new ErrorResponse(outcome.Message),
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Problem(outcome.Message, statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
