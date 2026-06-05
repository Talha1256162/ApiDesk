using System.Net;
using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class WorkspaceRbacIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task User_cannot_access_workspace_from_another_organization()
    {
        var admin = await factory.LoginAsync();
        var outsider = await factory.RegisterAsync("outsider");
        using var outsiderClient = factory.CreateAuthenticatedClient(outsider);

        var response = await outsiderClient.GetAsync($"/api/workspaces/{admin.WorkspaceId}/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Suspended_member_cannot_access_workspace_dashboard()
    {
        var admin = await factory.LoginAsync();
        var viewerRoleId = await factory.GetRoleIdAsync("Viewer");
        var invitee = await factory.RegisterAsync("suspended");
        using var adminClient = factory.CreateAuthenticatedClient(admin);
        using var inviteeClient = factory.CreateAuthenticatedClient(invitee);

        var invite = await adminClient.PostJsonAsync($"/api/organizations/{admin.OrganizationId}/invites", new
        {
            email = $"suspended-{Guid.NewGuid():N}@example.com",
            roleId = viewerRoleId,
            message = "join"
        });
        var inviteJson = await invite.ReadJsonAsync();
        inviteJson.Succeeded().Should().BeTrue(inviteJson.ToJsonString());
        var inviteToken = inviteJson["data"]!["inviteToken"]!.GetValue<string>();
        var inviteEmail = inviteJson["data"]!["email"]!.GetValue<string>();

        var realInvitee = await RegisterWithEmailAsync(inviteEmail);
        using var realInviteeClient = factory.CreateAuthenticatedClient(realInvitee);
        var accept = await realInviteeClient.PostJsonAsync("/api/organizations/invites/accept", new { token = inviteToken });
        var acceptJson = await accept.ReadJsonAsync();
        acceptJson.Succeeded().Should().BeTrue(acceptJson.ToJsonString());
        var memberId = acceptJson["data"]!["memberId"]!.GetValue<Guid>();

        var suspend = await adminClient.PatchJsonAsync($"/api/organizations/{admin.OrganizationId}/members/{memberId}/status", new { status = "Suspended" });
        var suspendJson = await suspend.ReadJsonAsync();
        suspendJson.Succeeded().Should().BeTrue(suspendJson.ToJsonString());

        var response = await realInviteeClient.GetAsync($"/api/workspaces/{admin.WorkspaceId}/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        return;

        async Task<AuthSession> RegisterWithEmailAsync(string email)
        {
            using var client = factory.CreateHttpsClient();
            var response = await client.PostJsonAsync("/api/auth/register", new
            {
                email,
                password = "Admin@12345",
                fullName = "Suspended Invitee",
                organizationName = $"Invitee Org {Guid.NewGuid():N}",
                workspaceName = "Invitee Workspace"
            });
            var json = await response.ReadJsonAsync();
            json.Succeeded().Should().BeTrue(json.ToJsonString());
            var data = json["data"]!;
            return new AuthSession(data["accessToken"]!.GetValue<string>(), data["refreshToken"]!.GetValue<string>(), data["organizationId"]!.GetValue<Guid>(), data["workspaceId"]!.GetValue<Guid>());
        }
    }

    [Fact]
    public async Task Viewer_cannot_modify_collections_but_editor_can()
    {
        var admin = await factory.LoginAsync();
        var viewerRoleId = await factory.GetRoleIdAsync("Viewer");
        var developerRoleId = await factory.GetRoleIdAsync("Developer");

        var viewer = await AddMemberAsync("viewer", viewerRoleId);
        using (var viewerClient = factory.CreateAuthenticatedClient(viewer))
        {
            var denied = await viewerClient.PostJsonAsync("/api/collections", new { workspaceId = admin.WorkspaceId, name = "Viewer should fail", description = "" });
            denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var editor = await AddMemberAsync("editor", developerRoleId);
        using (var editorClient = factory.CreateAuthenticatedClient(editor))
        {
            var allowed = await editorClient.PostJsonAsync("/api/collections", new { workspaceId = admin.WorkspaceId, name = $"Editor collection {Guid.NewGuid():N}", description = "" });
            var json = await allowed.ReadJsonAsync();
            json.Succeeded().Should().BeTrue(json.ToJsonString());
        }
    }

    [Fact]
    public async Task Sole_owner_cannot_remove_self_or_change_own_owner_role()
    {
        var admin = await factory.LoginAsync();
        var adminRoleId = await factory.GetRoleIdAsync("Admin");
        var memberId = await factory.GetAdminMemberIdAsync(admin);
        using var client = factory.CreateAuthenticatedClient(admin);

        var remove = await client.PatchJsonAsync($"/api/organizations/{admin.OrganizationId}/members/{memberId}/status", new { status = "Removed" });
        var removeJson = await remove.ReadJsonAsync();
        removeJson.Succeeded().Should().BeFalse(removeJson.ToJsonString());

        var changeRole = await client.PatchJsonAsync($"/api/organizations/{admin.OrganizationId}/members/{memberId}/role", new { roleId = adminRoleId });
        var changeRoleJson = await changeRole.ReadJsonAsync();
        changeRoleJson.Succeeded().Should().BeFalse(changeRoleJson.ToJsonString());
    }

    [Fact]
    public async Task Invalid_role_scope_assignment_is_rejected()
    {
        var admin = await factory.LoginAsync();
        var badRoleId = Guid.NewGuid();
        var viewerRoleId = await factory.GetRoleIdAsync("Viewer");
        var target = await factory.RegisterAsync("bad-scope-target");
        var targetUserId = await factory.ExecuteScalarAsync<Guid>("select top 1 userId from organizationMembers where organizationId = @OrganizationId", new { OrganizationId = target.OrganizationId });
        var memberId = Guid.NewGuid();
        await factory.ExecuteSqlAsync("""
            insert into organizationMembers (id, organizationId, userId, roleId, status, invitedByUserId, joinedOn, createdOn, createdBy, isDeleted, versionNumber)
            values (@MemberId, @OrganizationId, @UserId, @ViewerRoleId, 'Active', null, sysutcdatetime(), sysutcdatetime(), null, 0, 1);
            """, new { MemberId = memberId, admin.OrganizationId, UserId = targetUserId, ViewerRoleId = viewerRoleId });
        await factory.ExecuteSqlAsync("""
            insert into roles (id, name, scope, isSystemRole, createdOn, createdBy, isDeleted, versionNumber)
            values (@BadRoleId, 'Bad Scope Role', 'InvalidScope', 0, sysutcdatetime(), null, 0, 1);
            """, new { BadRoleId = badRoleId });
        using var client = factory.CreateAuthenticatedClient(admin);

        var response = await client.PatchJsonAsync($"/api/organizations/{admin.OrganizationId}/members/{memberId}/role", new { roleId = badRoleId });
        var json = await response.ReadJsonAsync();

        json.Succeeded().Should().BeFalse(json.ToJsonString());
        json["errors"]!.ToJsonString().Should().Contain("role.scope_invalid");
    }

    private async Task<AuthSession> AddMemberAsync(string prefix, Guid roleId)
    {
        var admin = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(admin);
        var email = $"{prefix}-{Guid.NewGuid():N}@example.com";
        var invite = await client.PostJsonAsync($"/api/organizations/{admin.OrganizationId}/invites", new { email, roleId, message = "join" });
        var inviteJson = await invite.ReadJsonAsync();
        inviteJson.Succeeded().Should().BeTrue(inviteJson.ToJsonString());
        var token = inviteJson["data"]!["inviteToken"]!.GetValue<string>();

        using var anonymous = factory.CreateHttpsClient();
        var register = await anonymous.PostJsonAsync("/api/auth/register", new
        {
            email,
            password = "Admin@12345",
            fullName = "Role Test User",
            organizationName = $"Role Test Org {Guid.NewGuid():N}",
            workspaceName = "Role Test Workspace"
        });
        var registerJson = await register.ReadJsonAsync();
        registerJson.Succeeded().Should().BeTrue(registerJson.ToJsonString());
        var data = registerJson["data"]!;
        var session = new AuthSession(data["accessToken"]!.GetValue<string>(), data["refreshToken"]!.GetValue<string>(), data["organizationId"]!.GetValue<Guid>(), data["workspaceId"]!.GetValue<Guid>());

        using var invitedClient = factory.CreateAuthenticatedClient(session);
        var accept = await invitedClient.PostJsonAsync("/api/organizations/invites/accept", new { token });
        var acceptJson = await accept.ReadJsonAsync();
        acceptJson.Succeeded().Should().BeTrue(acceptJson.ToJsonString());
        return session;
    }
}
