using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class ApiRunnerIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task Runner_sends_bearer_basic_api_key_and_oauth_auth_types()
    {
        var session = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(session);
        await using var echo = new LocalHttpEchoServer();
        var collectionId = await client.CreateCollectionAsync(session.WorkspaceId, $"Runner Auth {Guid.NewGuid():N}");

        var cases = new[]
        {
            new { Name = "Bearer auth", AuthType = "Bearer", Config = JsonSerializer.Serialize(new { token = "bearer-token" }), ExpectedHeader = "Authorization", ExpectedValue = "Bearer bearer-token", PathContains = "/bearer" },
            new { Name = "OAuth auth", AuthType = "OAuth2", Config = JsonSerializer.Serialize(new { token = "oauth-token" }), ExpectedHeader = "Authorization", ExpectedValue = "Bearer oauth-token", PathContains = "/oauth" },
            new { Name = "Basic auth", AuthType = "Basic", Config = JsonSerializer.Serialize(new { username = "api", password = "secret" }), ExpectedHeader = "Authorization", ExpectedValue = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("api:secret")), PathContains = "/basic" },
            new { Name = "Header api key", AuthType = "ApiKey", Config = JsonSerializer.Serialize(new { name = "X-Test-Key", value = "header-key", location = "header" }), ExpectedHeader = "X-Test-Key", ExpectedValue = "header-key", PathContains = "/api-key-header" }
        };

        foreach (var item in cases)
        {
            var create = await client.PostJsonAsync($"/api/collections/{collectionId}/requests", IntegrationTestHelpers.RequestPayload(session.WorkspaceId, collectionId, item.Name, "GET", new Uri(echo.BaseUri, item.PathContains.TrimStart('/')).ToString(), item.AuthType, item.Config));
            var createJson = await create.ReadJsonAsync();
            createJson.Succeeded().Should().BeTrue(createJson.ToJsonString());
            var requestId = createJson["data"]!["id"]!.GetValue<Guid>();

            var result = await client.SendRequestAsync(requestId);
            result.Succeeded().Should().BeTrue(result.ToJsonString());
            var latest = echo.Requests.Last();
            latest.Headers[item.ExpectedHeader].Should().Be(item.ExpectedValue);
        }

        var queryKeyCreate = await client.PostJsonAsync($"/api/collections/{collectionId}/requests", IntegrationTestHelpers.RequestPayload(
            session.WorkspaceId,
            collectionId,
            "Query api key",
            "GET",
            new Uri(echo.BaseUri, "api-key-query").ToString(),
            "ApiKey",
            JsonSerializer.Serialize(new { name = "api_key", value = "query-key", location = "query" })));
        var queryKeyJson = await queryKeyCreate.ReadJsonAsync();
        queryKeyJson.Succeeded().Should().BeTrue(queryKeyJson.ToJsonString());
        await client.SendRequestAsync(queryKeyJson["data"]!["id"]!.GetValue<Guid>());
        echo.Requests.Last().Path.Should().Contain("api_key=query-key");
    }

    [Theory]
    [InlineData("rawJson", "{\"name\":\"Apeiron\"}", "application/json")]
    [InlineData("rawText", "plain body", "text/plain")]
    [InlineData("formUrlEncoded", "name=Apeiron\nmode=test", "application/x-www-form-urlencoded")]
    [InlineData("formData", "name=Apeiron\nmode=test", "multipart/form-data")]
    public async Task Runner_sends_supported_body_types(string bodyType, string body, string expectedContentType)
    {
        var session = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(session);
        await using var echo = new LocalHttpEchoServer();
        var collectionId = await client.CreateCollectionAsync(session.WorkspaceId, $"Runner Body {Guid.NewGuid():N}");
        var create = await client.PostJsonAsync($"/api/collections/{collectionId}/requests", IntegrationTestHelpers.RequestPayload(session.WorkspaceId, collectionId, $"{bodyType} body", "POST", new Uri(echo.BaseUri, bodyType).ToString(), bodyType: bodyType, bodyContent: body));
        var createJson = await create.ReadJsonAsync();
        createJson.Succeeded().Should().BeTrue(createJson.ToJsonString());

        var result = await client.SendRequestAsync(createJson["data"]!["id"]!.GetValue<Guid>());

        result.Succeeded().Should().BeTrue(result.ToJsonString());
        var latest = echo.Requests.Last();
        latest.Method.Should().Be("POST");
        latest.Headers["Content-Type"].Should().Contain(expectedContentType);
        latest.Body.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Runner_rejects_non_http_urls_and_unresolved_variables()
    {
        var session = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(session);
        var collectionId = await client.CreateCollectionAsync(session.WorkspaceId, $"Runner Guards {Guid.NewGuid():N}");

        var invalid = await client.PostJsonAsync($"/api/collections/{collectionId}/requests", IntegrationTestHelpers.RequestPayload(session.WorkspaceId, collectionId, "Invalid scheme", "GET", "file:///c:/windows/win.ini"));
        var invalidJson = await invalid.ReadJsonAsync();
        invalidJson.Succeeded().Should().BeTrue(invalidJson.ToJsonString());
        var invalidResult = await client.SendRequestAsync(invalidJson["data"]!["id"]!.GetValue<Guid>());
        invalidResult.Succeeded().Should().BeFalse(invalidResult.ToJsonString());
        invalidResult["errors"]!.ToJsonString().Should().Contain("Only HTTP and HTTPS");

        var missingVariable = await client.PostJsonAsync($"/api/collections/{collectionId}/requests", IntegrationTestHelpers.RequestPayload(session.WorkspaceId, collectionId, "Missing variable", "GET", "https://example.test/{{missing}}"));
        var missingVariableJson = await missingVariable.ReadJsonAsync();
        missingVariableJson.Succeeded().Should().BeTrue(missingVariableJson.ToJsonString());
        var missingResult = await client.SendRequestAsync(missingVariableJson["data"]!["id"]!.GetValue<Guid>());
        missingResult.Succeeded().Should().BeFalse(missingResult.ToJsonString());
        missingResult["errors"]!.ToJsonString().Should().Contain("request.variables_missing");
    }

    [Fact]
    public async Task Runner_blocks_private_network_targets_when_production_ssrf_guard_is_enabled()
    {
        await using var strictFactory = new ApiDeskWebApplicationFactory(allowPrivateNetworkTargets: false);
        await strictFactory.InitializeAsync();
        var session = await strictFactory.LoginAsync();
        using var client = strictFactory.CreateAuthenticatedClient(session);
        var collectionId = await client.CreateCollectionAsync(session.WorkspaceId, $"Strict SSRF {Guid.NewGuid():N}");
        var create = await client.PostJsonAsync($"/api/collections/{collectionId}/requests", IntegrationTestHelpers.RequestPayload(session.WorkspaceId, collectionId, "Blocked local", "GET", "http://127.0.0.1:9/blocked"));
        var createJson = await create.ReadJsonAsync();
        createJson.Succeeded().Should().BeTrue(createJson.ToJsonString());

        var result = await client.SendRequestAsync(createJson["data"]!["id"]!.GetValue<Guid>());

        result.Succeeded().Should().BeFalse(result.ToJsonString());
        result["errors"]!.ToJsonString().Should().Contain("Private, localhost, and internal network targets are blocked");
    }
}
