using System.Security.Cryptography;
using System.Text;

namespace CarbonSim.Web.Accounts;

/// <summary>
/// Compares a secret a caller supplied with the one the host holds, in a time that does not depend
/// on how many leading characters happened to match. Both sides are hashed first so the comparison
/// also hides their lengths.
/// </summary>
internal static class FixedTimeSecret
{
    public static bool Equals(string? candidate, string? expected)
    {
        if (candidate is null || expected is null)
        {
            return candidate is null && expected is null;
        }

        byte[] candidateDigest = SHA256.HashData(Encoding.UTF8.GetBytes(candidate));
        byte[] expectedDigest = SHA256.HashData(Encoding.UTF8.GetBytes(expected));

        return CryptographicOperations.FixedTimeEquals(candidateDigest, expectedDigest);
    }
}
