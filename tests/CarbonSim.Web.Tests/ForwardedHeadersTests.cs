using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests;

/// <summary>
/// The demo is reached through a tunnel that terminates TLS and forwards plain HTTP from the same
/// machine. The host has to believe that tunnel's X-Forwarded-Proto, so the sign-in cookie is marked
/// secure, and has to ignore the same header from anybody else, who could otherwise claim https.
/// </summary>
public sealed class ForwardedHeadersTests : IClassFixture<ForwardedHeadersTests.CallerAddressHost>
{
    private readonly CallerAddressHost _host;

    public ForwardedHeadersTests(CallerAddressHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_tunnel_on_this_machine_that_forwards_https_gets_a_secure_cookie()
    {
        string cookie = await SignInCookieAsync(IPAddress.Loopback, "forwarded-local@example.com");

        cookie.Should().Contain("secure", "the browser reached the tunnel over https");
    }

    [Fact]
    public async Task A_remote_caller_cannot_claim_https_with_the_header()
    {
        string cookie = await SignInCookieAsync(IPAddress.Parse("203.0.113.9"), "forwarded-remote@example.com");

        cookie.Should().NotContain("secure", "only a proxy on this machine is trusted to say how the browser connected");
    }

    private async Task<string> SignInCookieAsync(IPAddress caller, string email)
    {
        await _host.SeedAdministratorAsync(email);
        using HttpClient client = _host.CreateClient();
        client.DefaultRequestHeaders.Add(CallerAddressHost.CallerHeader, caller.ToString());
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        HttpResponseMessage response = await client.LoginAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("carbonsim.auth=", StringComparison.Ordinal));
    }

    /// <summary>
    /// The real host, with the caller's address taken from a test header ahead of the whole
    /// pipeline: the test server has no network, so it reports no remote address of its own.
    /// </summary>
    public sealed class CallerAddressHost : CarbonSimWebHost
    {
        public const string CallerHeader = "X-Test-Caller-Address";

        protected override void ConfigureHostServices(IServiceCollection services)
        {
            base.ConfigureHostServices(services);
            services.AddTransient<IStartupFilter, CallerAddressFilter>();
        }

        private sealed class CallerAddressFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    if (IPAddress.TryParse(context.Request.Headers[CallerHeader], out IPAddress? address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    return nextMiddleware(context);
                });

                next(app);
            };
        }
    }
}
