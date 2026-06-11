using System.Net;
using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class EnvironmentSecurityIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task Workspace_member_can_list_environments_but_other_tenant_cannot()
    {
        var admin = await factory.LoginAsync();
        var outsider = await factory.RegisterAsync("env-outsider");
        using var adminClient = factory.CreateAuthenticatedClient(admin);
        using var outsiderClient = factory.CreateAuthenticatedClient(outsider);

        var own = await adminClient.GetAsync($"/api/workspaces/{admin.WorkspaceId}/environments?offset=0&count=10");
        var ownJson = await own.ReadJsonAsync();
        ownJson.Succeeded().Should().BeTrue(ownJson.ToJsonString());

        var otherTenant = await outsiderClient.GetAsync($"/api/workspaces/{admin.WorkspaceId}/environments?offset=0&count=10");
        otherTenant.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_cannot_upsert_environment_variables()
    {
        var admin = await factory.LoginAsync();
        var environmentId = await CreateEnvironmentAsync(admin, "Viewer Write Guard");
        var viewer = await AddWorkspaceMemberAsync(admin, "env-viewer", "Viewer");
        using var viewerClient = factory.CreateAuthenticatedClient(viewer);

        var response = await viewerClient.PutJsonAsync($"/api/environments/{environmentId}/variables", new
        {
            variables = new[] { new { key = "baseUrl", value = "https://example.com", scope = "Environment", isSecret = false, enabled = true } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_upsert_variables_and_secret_values_are_masked_in_response()
    {
        var admin = await factory.LoginAsync();
        var environmentId = await CreateEnvironmentAsync(admin, "Secret Masking");
        using var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PutJsonAsync($"/api/environments/{environmentId}/variables", new
        {
            variables = new[]
            {
                new { key = "baseUrl", value = "https://api.example.test", scope = "Environment", isSecret = false, enabled = true },
                new { key = "token", value = "super-secret-token", scope = "Environment", isSecret = true, enabled = true }
            }
        });
        var json = await response.ReadJsonAsync();

        json.Succeeded().Should().BeTrue(json.ToJsonString());
        var token = json["data"]!.AsArray().Single(item => item!["key"]!.GetValue<string>() == "token")!;
        token["isSecret"]!.GetValue<bool>().Should().BeTrue();
        token["value"]!.GetValue<string>().Should().NotBe("super-secret-token");
        token["value"]!.GetValue<string>().Should().Contain("*");
    }

    [Fact]
    public async Task Admin_can_update_duplicate_and_delete_environments()
    {
        var admin = await factory.LoginAsync();
        var firstEnvironmentId = await CreateEnvironmentAsync(admin, "Editable");
        using var client = factory.CreateAuthenticatedClient(admin);

        var update = await client.PutJsonAsync($"/api/environments/{firstEnvironmentId}", new
        {
            name = $"Edited {Guid.NewGuid():N}",
            isDefault = true
        });
        var updateJson = await update.ReadJsonAsync();
        updateJson.Succeeded().Should().BeTrue(updateJson.ToJsonString());
        updateJson["data"]!["isDefault"]!.GetValue<bool>().Should().BeTrue();

        await client.PutJsonAsync($"/api/environments/{firstEnvironmentId}/variables", new
        {
            variables = new[]
            {
                new { key = "api_url", value = "https://api.example.test", scope = "Environment", isSecret = false, enabled = true },
                new { key = "token", value = "super-secret-token", scope = "Environment", isSecret = true, enabled = true }
            }
        });

        var duplicate = await client.PostJsonAsync($"/api/environments/{firstEnvironmentId}/duplicate", new
        {
            name = $"Duplicated {Guid.NewGuid():N}",
            isDefault = false
        });
        var duplicateJson = await duplicate.ReadJsonAsync();
        duplicateJson.Succeeded().Should().BeTrue(duplicateJson.ToJsonString());
        duplicateJson["data"]!["variableCount"]!.GetValue<int>().Should().Be(2);

        var duplicateId = duplicateJson["data"]!["id"]!.GetValue<Guid>();
        var duplicateVariables = await (await client.GetAsync($"/api/environments/{duplicateId}/variables")).ReadJsonAsync();
        duplicateVariables.Succeeded().Should().BeTrue(duplicateVariables.ToJsonString());
        duplicateVariables["data"]!.AsArray().Should().HaveCount(2);
        duplicateVariables["data"]!.AsArray().Single(item => item!["key"]!.GetValue<string>() == "token")!["value"]!.GetValue<string>().Should().NotBe("super-secret-token");

        var delete = await client.DeleteAsync($"/api/environments/{firstEnvironmentId}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await (await client.GetAsync($"/api/workspaces/{admin.WorkspaceId}/environments?offset=0&count=100")).ReadJsonAsync();
        list.Succeeded().Should().BeTrue(list.ToJsonString());
        list["data"]!["items"]!.AsArray().Any(item => item!["id"]!.GetValue<Guid>() == firstEnvironmentId).Should().BeFalse();
    }

    [Fact]
    public async Task Environment_variable_validation_blocks_invalid_and_duplicate_keys()
    {
        var admin = await factory.LoginAsync();
        var environmentId = await CreateEnvironmentAsync(admin, "Validation");
        using var client = factory.CreateAuthenticatedClient(admin);

        var invalid = await client.PutJsonAsync($"/api/environments/{environmentId}/variables", new
        {
            variables = new[] { new { key = "1 invalid", value = "", scope = "Environment", isSecret = false, enabled = true } }
        });
        var invalidJson = await invalid.ReadJsonAsync();
        invalidJson.Succeeded().Should().BeFalse(invalidJson.ToJsonString());
        invalidJson["errors"]!.AsArray().Single()!["code"]!.GetValue<string>().Should().Be("environment.variable_key_invalid");

        var duplicate = await client.PutJsonAsync($"/api/environments/{environmentId}/variables", new
        {
            variables = new[]
            {
                new { key = "api_url", value = "https://one.example", scope = "Environment", isSecret = false, enabled = true },
                new { key = "API_URL", value = "https://two.example", scope = "Environment", isSecret = false, enabled = true }
            }
        });
        var duplicateJson = await duplicate.ReadJsonAsync();
        duplicateJson.Succeeded().Should().BeFalse(duplicateJson.ToJsonString());
        duplicateJson["errors"]!.AsArray().Single()!["code"]!.GetValue<string>().Should().Be("environment.variable_key_duplicate");
    }

    [Fact]
    public async Task Masked_secret_placeholder_preserves_existing_secret_value()
    {
        var admin = await factory.LoginAsync();
        var environmentId = await CreateEnvironmentAsync(admin, "Secret Preserve");
        using var client = factory.CreateAuthenticatedClient(admin);

        await client.PutJsonAsync($"/api/environments/{environmentId}/variables", new
        {
            variables = new[] { new { key = "token", value = "real-secret-token", scope = "Environment", isSecret = true, enabled = true } }
        });

        var update = await client.PutJsonAsync($"/api/environments/{environmentId}/variables", new
        {
            variables = new[] { new { key = "token", value = "********", scope = "Environment", isSecret = true, enabled = true } }
        });
        var updateJson = await update.ReadJsonAsync();
        updateJson.Succeeded().Should().BeTrue(updateJson.ToJsonString());

        var rawValue = await factory.ExecuteScalarAsync<string?>(
            "select [value] from environmentVariables where environmentId = @EnvironmentId and [key] = 'token' and isDeleted = 0",
            new { EnvironmentId = environmentId });
        rawValue.Should().Be("real-secret-token");
    }

    private async Task<Guid> CreateEnvironmentAsync(AuthSession session, string name)
    {
        using var client = factory.CreateAuthenticatedClient(session);
        var response = await client.PostJsonAsync("/api/environments", new { workspaceId = session.WorkspaceId, name = $"{name} {Guid.NewGuid():N}", isDefault = false });
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        return json["data"]!["id"]!.GetValue<Guid>();
    }

    private async Task<AuthSession> AddWorkspaceMemberAsync(AuthSession ownerSession, string prefix, string roleName)
    {
        var member = await factory.RegisterAsync(prefix);
        var roleId = await factory.GetRoleIdAsync(roleName);
        await factory.ExecuteSqlAsync("""
            insert into organizationMembers (id, organizationId, userId, roleId, status, invitedByUserId, joinedOn, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @OrganizationId, @UserId, @RoleId, 'Active', @OwnerUserId, sysutcdatetime(), sysutcdatetime(), @OwnerUserId, 0, 1);

            insert into workspaceMembers (id, organizationId, workspaceId, userId, roleId, status, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @OrganizationId, @WorkspaceId, @UserId, @RoleId, 'Active', sysutcdatetime(), @OwnerUserId, 0, 1);
            """,
            new { ownerSession.OrganizationId, ownerSession.WorkspaceId, UserId = await factory.ExecuteScalarAsync<Guid>("select id from users where email = (select top 1 email from users order by createdOn desc)"), RoleId = roleId, OwnerUserId = await factory.ExecuteScalarAsync<Guid>("select top 1 userId from organizationMembers where organizationId = @OrganizationId and isDeleted = 0", new { ownerSession.OrganizationId }) });
        return member;
    }
}
