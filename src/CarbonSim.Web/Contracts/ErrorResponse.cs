namespace CarbonSim.Web.Contracts;

/// <summary>Why a request was refused, in a sentence the caller can act on.</summary>
public sealed record ErrorResponse(string Message);
