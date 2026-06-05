using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace ApiForge.IntegrationTests;

internal static class IntegrationTestHelpers
{
    public static async Task<Guid> GetRoleIdAsync(this ApiDeskWebApplicationFactory factory, string roleName)
    {
        var roleId = await factory.ExecuteScalarAsync<Guid?>("select top 1 id from roles where name = @RoleName and isDeleted = 0;", new { RoleName = roleName });
        roleId.Should().NotBeNull($"seed role {roleName} must exist");
        return roleId!.Value;
    }

    public static async Task<Guid> GetAdminMemberIdAsync(this ApiDeskWebApplicationFactory factory, AuthSession admin)
    {
        var memberId = await factory.ExecuteScalarAsync<Guid?>(
            "select top 1 id from organizationMembers where organizationId = @OrganizationId and status = 'Active' and isDeleted = 0 order by createdOn;",
            new { admin.OrganizationId });
        memberId.Should().NotBeNull();
        return memberId!.Value;
    }

    public static async Task<Guid> CreateCollectionAsync(this HttpClient client, Guid workspaceId, string name)
    {
        var response = await client.PostJsonAsync("/api/collections", new { workspaceId, name, description = "Integration test collection" });
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        return json["data"]!["id"]!.GetValue<Guid>();
    }

    public static async Task<Guid> ImportCollectionAsync(this HttpClient client, Guid workspaceId, object payload)
    {
        var response = await client.PostJsonAsync($"/api/workspaces/{workspaceId}/collections/import", payload);
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        return json["data"]!["collectionId"]!.GetValue<Guid>();
    }

    public static async Task<Guid> FirstRequestIdAsync(this HttpClient client, Guid collectionId)
    {
        var response = await client.GetAsync($"/api/collections/{collectionId}/requests");
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        return json["data"]![0]!["id"]!.GetValue<Guid>();
    }

    public static async Task<JsonNode> SendRequestAsync(this HttpClient client, Guid requestId, Guid? environmentId = null)
    {
        var response = await client.PostJsonAsync($"/api/requests/{requestId}/send", new { environmentId, saveHistory = true });
        return await response.ReadJsonAsync();
    }

    public static object RequestPayload(
        Guid workspaceId,
        Guid collectionId,
        string name,
        string method,
        string url,
        string? authType = null,
        string? authConfigJson = null,
        string bodyType = "none",
        string? bodyContent = null,
        object[]? headers = null,
        object[]? queryParams = null)
    {
        return new
        {
            workspaceId,
            collectionId,
            name,
            description = "Integration request",
            method,
            url,
            authType,
            authConfigJson,
            bodyType,
            bodyContent,
            preRequestScript = (string?)null,
            testScript = (string?)null,
            timeoutMs = 5000,
            followRedirects = true,
            sslVerification = true,
            headers = headers ?? [],
            queryParams = queryParams ?? [],
            pathParams = Array.Empty<object>(),
            versionNumber = 1
        };
    }
}

internal sealed class LocalHttpEchoServer : IAsyncDisposable
{
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource cts = new();
    private readonly Task loopTask;

    public Uri BaseUri { get; }
    public IReadOnlyList<EchoRequest> Requests => requests;

    private readonly List<EchoRequest> requests = [];

    public LocalHttpEchoServer()
    {
        var port = FreeTcpPort();
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        listener.Prefixes.Add(BaseUri.ToString());
        listener.Start();
        loopTask = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        while (!cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch when (cts.IsCancellationRequested || !listener.IsListening)
            {
                break;
            }

            var body = await new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEndAsync();
            var request = new EchoRequest(
                context.Request.HttpMethod,
                context.Request.RawUrl ?? "/",
                context.Request.Headers.AllKeys.ToDictionary(k => k!, k => context.Request.Headers[k!] ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                body);

            lock (requests)
            {
                requests.Add(request);
            }

            var responseBody = JsonSerializer.Serialize(new
            {
                request.Method,
                request.Path,
                request.Headers,
                request.Body
            });
            var bytes = Encoding.UTF8.GetBytes(responseBody);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        listener.Stop();
        listener.Close();
        try
        {
            await loopTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Listener shutdown can race with an in-flight GetContextAsync in tests.
        }

        cts.Dispose();
    }

    private static int FreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed record EchoRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers, string Body);
