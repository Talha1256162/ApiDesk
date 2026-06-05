using System.Net;
using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class AuthIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task Login_valid_credentials_returns_access_and_refresh_tokens()
    {
        var session = await factory.LoginAsync();

        session.AccessToken.Should().NotBeNullOrWhiteSpace();
        session.RefreshToken.Should().NotBeNullOrWhiteSpace();
        session.OrganizationId.Should().NotBeEmpty();
        session.WorkspaceId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_invalid_credentials_returns_failure()
    {
        using var client = factory.CreateHttpsClient();

        var response = await client.PostJsonAsync("/api/auth/login", new { email = "admin@apiforge.local", password = "wrong-password" });
        var json = await response.ReadJsonAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        json.Succeeded().Should().BeFalse();
    }

    [Fact]
    public async Task Protected_routes_reject_anonymous_requests()
    {
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_token_rotates_and_logout_revokes_refresh_token()
    {
        var session = await factory.LoginAsync();
        using var client = factory.CreateHttpsClient();

        var refresh = await client.PostJsonAsync("/api/auth/refresh", new { session.RefreshToken });
        var refreshJson = await refresh.ReadJsonAsync();
        refreshJson.Succeeded().Should().BeTrue(refreshJson.ToJsonString());
        var rotatedRefresh = refreshJson["data"]!["refreshToken"]!.GetValue<string>();

        var logout = await client.PostJsonAsync("/api/auth/logout", new { refreshToken = rotatedRefresh });
        var logoutJson = await logout.ReadJsonAsync();
        logoutJson.Succeeded().Should().BeTrue(logoutJson.ToJsonString());

        var revokedRefresh = await client.PostJsonAsync("/api/auth/refresh", new { refreshToken = rotatedRefresh });
        var revokedJson = await revokedRefresh.ReadJsonAsync();
        revokedJson.Succeeded().Should().BeFalse(revokedJson.ToJsonString());
    }
}
