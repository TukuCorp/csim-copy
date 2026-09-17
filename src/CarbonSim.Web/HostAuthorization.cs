namespace CarbonSim.Web;

/// <summary>Names of the authorization policies the host's endpoints ask for.</summary>
internal static class HostAuthorization
{
    /// <summary>Only an administrator configures the run, drives the clock and administers accounts.</summary>
    public const string AdministratorPolicy = "CarbonSimAdministrator";
}
