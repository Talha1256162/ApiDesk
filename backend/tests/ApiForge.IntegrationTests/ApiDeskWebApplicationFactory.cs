using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using ApiForge.Application.Abstractions.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiForge.IntegrationTests;

public sealed class ApiDeskWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly bool allowPrivateNetworkTargets;
    private readonly string databaseName;
    private static readonly string MasterConnectionString =
        Environment.GetEnvironmentVariable("APIDESK_TEST_SQL_MASTER")
        ?? "Server=.\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public string ConnectionString { get; private set; }

    public JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);
    public CapturingEmailSender EmailSender { get; } = new();

    public ApiDeskWebApplicationFactory()
        : this(true)
    {
    }

    internal ApiDeskWebApplicationFactory(bool allowPrivateNetworkTargets)
    {
        this.allowPrivateNetworkTargets = allowPrivateNetworkTargets;
        databaseName = allowPrivateNetworkTargets ? "ApiForgePro_IntegrationTests" : "ApiForgePro_IntegrationTests_Strict";
        ConnectionString = Environment.GetEnvironmentVariable("APIDESK_TEST_SQL")
            ?? $"Server=.\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApiForge"] = ConnectionString,
                ["Database:RunMigrationsOnStartup"] = "false",
                ["Jwt:Issuer"] = "ApiForge",
                ["Jwt:Audience"] = "ApiForge",
                ["Jwt:SigningKey"] = "LOCAL_DEV_ONLY_CHANGE_THIS_64_CHARACTER_SIGNING_KEY_FOR_APIFORGE",
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "14",
                ["RequestRunner:AllowPrivateNetworkTargets"] = allowPrivateNetworkTargets ? "true" : "false",
                ["Email:PublicBaseUrl"] = "https://test.apidesk.local",
                ["Email:Smtp:Enabled"] = "true",
                ["Email:Smtp:Host"] = "mail.smtp.com",
                ["Email:Smtp:Port"] = "465",
                ["Email:Smtp:Username"] = "info@paysetra.com",
                ["Email:Smtp:Password"] = "integration-secret-not-real",
                ["Email:Smtp:FromName"] = "Paysetra",
                ["Email:Smtp:FromEmail"] = "info@paysetra.com",
                ["Email:Smtp:EncryptionId"] = "2",
                ["Email:Smtp:IsVerified"] = "false",
                ["Swagger:Enabled"] = "false"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton(EmailSender);
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<CapturingEmailSender>());
        });
    }

    public async Task<AuthSession> LoginAsync(string email = "admin@apiforge.local", string password = "Admin@12345")
    {
        using var client = CreateHttpsClient();
        var response = await client.PostJsonAsync("/api/auth/login", new { email, password });
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        var data = json["data"]!;
        return new AuthSession(
            data["accessToken"]!.GetValue<string>(),
            data["refreshToken"]!.GetValue<string>(),
            data["organizationId"]!.GetValue<Guid>(),
            data["workspaceId"]!.GetValue<Guid>());
    }

    public HttpClient CreateAuthenticatedClient(AuthSession session)
    {
        var client = CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    public async Task<AuthSession> RegisterAsync(string emailPrefix)
    {
        using var client = CreateHttpsClient();
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        var response = await client.PostJsonAsync("/api/auth/register", new
        {
            email,
            password = "Admin@12345",
            fullName = "Integration User",
            organizationName = $"Integration Org {Guid.NewGuid():N}",
            workspaceName = "Integration Workspace"
        });
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        var data = json["data"]!;
        return new AuthSession(
            data["accessToken"]!.GetValue<string>(),
            data["refreshToken"]!.GetValue<string>(),
            data["organizationId"]!.GetValue<Guid>(),
            data["workspaceId"]!.GetValue<Guid>());
    }

    public async Task ExecuteSqlAsync(string sql, object? args = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        AddParameters(command, args);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? args = null)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        AddParameters(command, args);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    private static void AddParameters(SqlCommand command, object? args)
    {
        if (args is null)
        {
            return;
        }

        foreach (var property in args.GetType().GetProperties())
        {
            command.Parameters.AddWithValue("@" + property.Name, property.GetValue(args) ?? DBNull.Value);
        }
    }

    public HttpClient CreateHttpsClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private async Task ResetDatabaseAsync()
    {
        await using (var master = new SqlConnection(MasterConnectionString))
        {
            await master.OpenAsync();
            await ExecuteMasterAsync(master, $"""
                if db_id(N'{databaseName}') is not null
                begin
                    alter database [{databaseName}] set single_user with rollback immediate;
                    drop database [{databaseName}];
                end
                create database [{databaseName}];
                """);
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var root = FindRepositoryRoot();
        foreach (var script in Directory.GetFiles(Path.Combine(root, "database", "scripts"), "*.sql").OrderBy(Path.GetFileName))
        {
            var text = await File.ReadAllTextAsync(script);
            foreach (var batch in SplitBatches(text).Select(RemoveDatabaseSwitching).Where(batch => !string.IsNullOrWhiteSpace(batch)))
            {
                await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task ExecuteMasterAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "database", "scripts")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate database/scripts from test output.");
    }

    private static IEnumerable<string> SplitBatches(string script)
    {
        return Regex.Split(script.Replace("\r\n", "\n"), @"(?im)^\s*go\s*$")
            .Where(batch => !string.IsNullOrWhiteSpace(batch));
    }

    private static string RemoveDatabaseSwitching(string batch)
    {
        if (batch.Contains("create database", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var lines = batch
            .Replace("\r\n", "\n")
            .Split('\n')
            .Where(line =>
            {
                var trimmed = line.Trim();
                return !trimmed.StartsWith("use ", StringComparison.OrdinalIgnoreCase)
                    && !trimmed.StartsWith("if db_id", StringComparison.OrdinalIgnoreCase);
            });

        return string.Join(Environment.NewLine, lines);
    }
}

public sealed record AuthSession(string AccessToken, string RefreshToken, Guid OrganizationId, Guid WorkspaceId);

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> messages = [];

    public bool FailNextSend { get; set; }
    public IReadOnlyList<EmailMessage> Messages => messages;

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailNextSend)
        {
            FailNextSend = false;
            return Task.FromResult(EmailSendResult.Failed("test", "Simulated SMTP failure."));
        }

        messages.Add(message);
        return Task.FromResult(EmailSendResult.Sent("test"));
    }
}

public static class HttpTestExtensions
{
    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object payload)
    {
        return client.PostAsync(url, Json(payload));
    }

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object payload)
    {
        return client.PutAsync(url, Json(payload));
    }

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object payload)
    {
        return client.PatchAsync(url, Json(payload));
    }

    public static async Task<JsonNode> ReadJsonAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var request = response.RequestMessage;
            var headers = string.Join(Environment.NewLine, response.Headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}"));
            var contentHeaders = string.Join(Environment.NewLine, response.Content.Headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}"));
            throw new InvalidOperationException($"""
                Response was empty and could not be parsed as JSON.
                Request: {request?.Method} {request?.RequestUri}
                Status: {(int)response.StatusCode} {response.StatusCode}
                Response headers:
                {headers}
                Content headers:
                {contentHeaders}
                Body:
                {body}
                """);
        }

        try
        {
            return JsonNode.Parse(body) ?? throw new InvalidOperationException("Response body parsed to null JSON node.");
        }
        catch (JsonException ex)
        {
            var request = response.RequestMessage;
            var headers = string.Join(Environment.NewLine, response.Headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}"));
            var contentHeaders = string.Join(Environment.NewLine, response.Content.Headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}"));
            throw new InvalidOperationException($"""
                Response was not valid JSON.
                Request: {request?.Method} {request?.RequestUri}
                Status: {(int)response.StatusCode} {response.StatusCode}
                Response headers:
                {headers}
                Content headers:
                {contentHeaders}
                Body:
                {body}
                """, ex);
        }
    }

    public static bool Succeeded(this JsonNode node) => node["succeeded"]?.GetValue<bool>() == true;

    private static StringContent Json(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json");
    }
}
