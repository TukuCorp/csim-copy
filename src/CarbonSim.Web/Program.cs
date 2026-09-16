// CarbonSim host skeleton. The ASP.NET Core endpoints, SignalR hub and Blazor UI
// arrive in Phase 2/3 (see activeContext.md); until then this project exists only
// to pin the host shape and the dependency direction (Web -> Data -> Engine).
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

app.Run();
