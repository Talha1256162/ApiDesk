using System.Net;
using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class MockDocsSecurityIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task Mock_server_requires_valid_api_key_when_configured()
    {
        var session = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(session);
        var requestId = await CreateMockableRequestAsync(client, session);
        await SaveExampleAsync(client, requestId);
        var collectionId = await factory.ExecuteScalarAsync<Guid>("select collectionId from requests where id = @RequestId", new { RequestId = requestId });

        var mock = await client.PostJsonAsync($"/api/workspaces/{session.WorkspaceId}/mock-servers", new
        {
            collectionId,
            name = $"Private Mock {Guid.NewGuid():N}",
            isPublic = false,
            apiKeyRequired = true,
            delayMs = 0
        });
        var mockJson = await mock.ReadJsonAsync();
        mockJson.Succeeded().Should().BeTrue(mockJson.ToJsonString());
        var slug = mockJson["data"]!["slug"]!.GetValue<string>();

        using var anonymous = factory.CreateHttpsClient();
        (await anonymous.GetAsync($"/api/mock/{slug}/users/42")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var invalid = new HttpRequestMessage(HttpMethod.Get, $"/api/mock/{slug}/users/42");
        invalid.Headers.Add("X-API-Desk-Key", "wrong-key");
        (await anonymous.SendAsync(invalid)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var keyResponse = await client.PostJsonAsync($"/api/organizations/{session.OrganizationId}/api-keys", new
        {
            workspaceId = session.WorkspaceId,
            name = "Mock test key",
            expiresOn = (DateTime?)null
        });
        var keyJson = await keyResponse.ReadJsonAsync();
        keyJson.Succeeded().Should().BeTrue(keyJson.ToJsonString());
        var plainKey = keyJson["data"]!["plainTextKey"]!.GetValue<string>();

        var valid = new HttpRequestMessage(HttpMethod.Get, $"/api/mock/{slug}/users/42");
        valid.Headers.Add("X-API-Desk-Key", plainKey);
        var validResponse = await anonymous.SendAsync(valid);

        validResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await validResponse.Content.ReadAsStringAsync()).Should().Contain("Ada");
    }

    [Fact]
    public async Task Documentation_private_and_password_protected_flows_are_enforced()
    {
        var session = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(session);
        var requestId = await CreateMockableRequestAsync(client, session);
        await SaveExampleAsync(client, requestId);
        var collectionId = await factory.ExecuteScalarAsync<Guid>("select collectionId from requests where id = @RequestId", new { RequestId = requestId });

        var privateSlug = $"private-doc-{Guid.NewGuid():N}";
        var privateDoc = await client.PostJsonAsync($"/api/workspaces/{session.WorkspaceId}/published-docs", new { collectionId, slug = privateSlug, isPublic = false, password = (string?)null, brandJson = (string?)null });
        var privateDocJson = await privateDoc.ReadJsonAsync();
        privateDocJson.Succeeded().Should().BeTrue(privateDocJson.ToJsonString());

        using var anonymous = factory.CreateHttpsClient();
        var privateRead = await anonymous.GetAsync($"/api/docs/{privateSlug}");
        privateRead.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var protectedSlug = $"protected-doc-{Guid.NewGuid():N}";
        var protectedDoc = await client.PostJsonAsync($"/api/workspaces/{session.WorkspaceId}/published-docs", new { collectionId, slug = protectedSlug, isPublic = true, password = "docs-pass", brandJson = (string?)null });
        var protectedDocJson = await protectedDoc.ReadJsonAsync();
        protectedDocJson.Succeeded().Should().BeTrue(protectedDocJson.ToJsonString());

        var locked = await anonymous.GetAsync($"/api/docs/{protectedSlug}");
        locked.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var wrongPassword = await anonymous.PostJsonAsync($"/api/docs/{protectedSlug}/unlock", new { password = "wrong" });
        var wrongPasswordJson = await wrongPassword.ReadJsonAsync();
        wrongPasswordJson.Succeeded().Should().BeFalse(wrongPasswordJson.ToJsonString());

        var unlocked = await anonymous.PostJsonAsync($"/api/docs/{protectedSlug}/unlock", new { password = "docs-pass" });
        var unlockedJson = await unlocked.ReadJsonAsync();
        unlockedJson.Succeeded().Should().BeTrue(unlockedJson.ToJsonString());
        unlockedJson["data"]!["requests"]!.AsArray().Should().NotBeEmpty();
    }

    private static async Task SaveExampleAsync(HttpClient client, Guid requestId)
    {
        var response = await client.PostJsonAsync($"/api/requests/{requestId}/examples", new
        {
            name = "Success",
            statusCode = 200,
            headersJson = "{}",
            body = "{\"id\":42,\"name\":\"Ada\"}"
        });
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
    }

    private static async Task<Guid> CreateMockableRequestAsync(HttpClient client, AuthSession session)
    {
        var collectionId = await client.CreateCollectionAsync(session.WorkspaceId, $"Mock Docs {Guid.NewGuid():N}");
        var response = await client.PostJsonAsync($"/api/collections/{collectionId}/requests", IntegrationTestHelpers.RequestPayload(session.WorkspaceId, collectionId, "Get user", "GET", "https://api.example.test/users/42"));
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        return json["data"]!["id"]!.GetValue<Guid>();
    }
}
