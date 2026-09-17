namespace CarbonSim.Web.Contracts;

/// <summary>What a visitor posts to exchange an emailed code for a new password.</summary>
public sealed record ConfirmPasswordResetRequest
{
    public string? Email { get; init; }

    /// <summary>The numeric code from the email.</summary>
    public string? Code { get; init; }

    public string? NewPassword { get; init; }
}
