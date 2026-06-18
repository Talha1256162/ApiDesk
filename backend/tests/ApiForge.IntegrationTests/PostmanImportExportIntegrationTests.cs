using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class PostmanImportExportIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task Import_export_reimport_preserves_request_count_and_folder_structure()
    {
        var session = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(session);
        var importPayload = new
        {
            name = $"Postman Migration {Guid.NewGuid():N}",
            description = "Imported from Postman v2.1 preview payload.",
            folders = new[]
            {
                new[] { "Auth" },
                new[] { "Customers" },
                new[] { "Customers", "Payments" }
            },
            requests = new object[]
            {
                new
                {
                    name = "Login",
                    description = "Login request",
                    method = "POST",
                    url = "{{baseUrl}}/auth/login",
                    authType = (string?)null,
                    authConfigJson = (string?)null,
                    bodyType = "rawJson",
                    bodyContent = "{\"email\":\"{{email}}\",\"password\":\"{{password}}\"}",
                    preRequestScript = "pm.environment.set('started', Date.now());",
                    testScript = "pm.test('status', () => pm.response.to.have.status(200));",
                    timeoutMs = 5000,
                    followRedirects = true,
                    sslVerification = true,
                    headers = new[] { new { key = "Content-Type", value = "application/json", enabled = true, isSecret = false } },
                    queryParams = Array.Empty<object>(),
                    pathParams = Array.Empty<object>(),
                    folderPath = new[] { "Auth" }
                },
                new
                {
                    name = "Get customer",
                    description = "Customer lookup",
                    method = "GET",
                    url = "{{baseUrl}}/customers/{{customerId}}",
                    authType = "Bearer",
                    authConfigJson = "{\"token\":\"{{token}}\"}",
                    bodyType = "none",
                    bodyContent = (string?)null,
                    preRequestScript = (string?)null,
                    testScript = "pm.test('fast', () => pm.expect(pm.response.responseTime).to.be.below(500));",
                    timeoutMs = 5000,
                    followRedirects = true,
                    sslVerification = true,
                    headers = Array.Empty<object>(),
                    queryParams = new[] { new { key = "include", value = "profile", enabled = true, isSecret = false } },
                    pathParams = Array.Empty<object>(),
                    folderPath = new[] { "Customers" }
                },
                new
                {
                    name = "Create payment",
                    description = "Payment creation",
                    method = "POST",
                    url = "{{baseUrl}}/customers/{{customerId}}/payments",
                    authType = "ApiKey",
                    authConfigJson = "{\"name\":\"X-API-Key\",\"value\":\"{{apiKey}}\",\"location\":\"header\"}",
                    bodyType = "rawJson",
                    bodyContent = "{\"amount\":1250,\"currency\":\"PKR\"}",
                    preRequestScript = (string?)null,
                    testScript = (string?)null,
                    timeoutMs = 5000,
                    followRedirects = true,
                    sslVerification = true,
                    headers = Array.Empty<object>(),
                    queryParams = Array.Empty<object>(),
                    pathParams = Array.Empty<object>(),
                    folderPath = new[] { "Customers", "Payments" }
                }
            }
        };

        var importedCollectionId = await client.ImportCollectionAsync(session.WorkspaceId, importPayload);
        var importedRequests = await client.GetAsync($"/api/collections/{importedCollectionId}/requests");
        var importedRequestsJson = await importedRequests.ReadJsonAsync();
        importedRequestsJson.Succeeded().Should().BeTrue(importedRequestsJson.ToJsonString());
        importedRequestsJson["data"]!["items"]!.AsArray().Should().HaveCount(3);
        importedRequestsJson["data"]!.ToJsonString().Should().Contain("Payments");

        var export = await client.GetAsync($"/api/collections/{importedCollectionId}/export");
        var exportJson = await export.ReadJsonAsync();
        exportJson.Succeeded().Should().BeTrue(exportJson.ToJsonString());
        exportJson["data"]!["requests"]!.AsArray().Should().HaveCount(3);
        exportJson["data"]!["requests"]!.ToJsonString().Should().Contain("Payments");

        var reimportPayload = new
        {
            name = $"Round Trip {Guid.NewGuid():N}",
            description = "Re-imported export payload",
            requests = exportJson["data"]!["requests"]!.AsArray()
        };
        var reimportedCollectionId = await client.ImportCollectionAsync(session.WorkspaceId, reimportPayload);
        var reimportedRequests = await client.GetAsync($"/api/collections/{reimportedCollectionId}/requests");
        var reimportedRequestsJson = await reimportedRequests.ReadJsonAsync();

        reimportedRequestsJson.Succeeded().Should().BeTrue(reimportedRequestsJson.ToJsonString());
        reimportedRequestsJson["data"]!["items"]!.AsArray().Should().HaveCount(3);
        reimportedRequestsJson["data"]!.ToJsonString().Should().Contain("Payments");
    }
}
