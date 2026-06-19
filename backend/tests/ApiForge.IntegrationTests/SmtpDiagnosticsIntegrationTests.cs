using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class SmtpDiagnosticsIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task Diagnostics_reports_masked_smtp_configuration_without_exposing_secret()
    {
        var admin = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.GetAsync("/api/admin/smtp-diagnostics");
        var json = await response.ReadJsonAsync();

        json.Succeeded().Should().BeTrue(json.ToJsonString());
        var data = json["data"]!;
        data["enabled"]!.GetValue<bool>().Should().BeTrue();
        data["hostPresent"]!.GetValue<bool>().Should().BeTrue();
        data["usernamePresent"]!.GetValue<bool>().Should().BeTrue();
        data["passwordConfigured"]!.GetValue<bool>().Should().BeTrue();
        data["fromEmailPresent"]!.GetValue<bool>().Should().BeTrue();
        data["isConfigured"]!.GetValue<bool>().Should().BeTrue();
        data["expectedEnvironmentVariables"]!.AsArray().Select(item => item!.GetValue<string>())
            .Should().Contain("Email__Smtp__Password");
        json.ToJsonString().Should().NotContain("integration-secret-not-real");
    }
}
