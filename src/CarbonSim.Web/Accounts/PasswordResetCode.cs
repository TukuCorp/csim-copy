using System.Globalization;
using System.Security.Cryptography;

namespace CarbonSim.Web.Accounts;

/// <summary>
/// The code a visitor reads out of an email. Six digits, because it is read off a screen and typed
/// back in; drawn from the cryptographic generator, because guessing it is the whole attack.
/// </summary>
internal static class PasswordResetCode
{
    private const int NumberOfCodes = 1_000_000;

    public static string Generate()
    {
        return RandomNumberGenerator.GetInt32(NumberOfCodes).ToString("D6", CultureInfo.InvariantCulture);
    }
}
