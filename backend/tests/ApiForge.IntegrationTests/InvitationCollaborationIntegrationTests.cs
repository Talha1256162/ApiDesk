using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class InvitationCollaborationIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task Invite_email_acceptance_and_shared_workspace_collections_work_across_members()
    {
        var teamLead = await factory.LoginAsync();
        using var leadClient = factory.CreateAuthenticatedClient(teamLead);
        var developerRoleId = await factory.GetRoleIdAsync("Developer");

        var dev1Email = $"dev1-{Guid.NewGuid():N}@example.com";
        var dev2Email = $"dev2-{Guid.NewGuid():N}@example.com";
        var dev1Invite = await SendInviteAsync(leadClient, teamLead, developerRoleId, dev1Email);
        var dev2Invite = await SendInviteAsync(leadClient, teamLead, developerRoleId, dev2Email);

        factory.EmailSender.Messages.Should().Contain(message => message.ToEmail == dev1Email && message.Subject.Contains("Apeiron"));
        factory.EmailSender.Messages.Should().Contain(message => message.ToEmail == dev2Email && message.HtmlBody.Contains("/invite/"));

        var sentLogCount = await factory.ExecuteScalarAsync<int>("select count(1) from emailDeliveryLogs where organizationId = @OrganizationId and status = 'Sent' and recipientEmail in (@Dev1Email, @Dev2Email)", new { teamLead.OrganizationId, Dev1Email = dev1Email, Dev2Email = dev2Email });
        sentLogCount.Should().Be(2);

        var dev1 = await RegisterWithEmailAsync(dev1Email, "Developer One");
        var dev2 = await RegisterWithEmailAsync(dev2Email, "Developer Two");
        using var dev1Client = factory.CreateAuthenticatedClient(dev1);
        using var dev2Client = factory.CreateAuthenticatedClient(dev2);

        await AcceptAsync(dev1Client, dev1Invite["inviteToken"]!.GetValue<string>());
        await AcceptAsync(dev2Client, dev2Invite["inviteToken"]!.GetValue<string>());

        var dev1WorkspaceMember = await factory.ExecuteScalarAsync<int>("select count(1) from workspaceMembers where workspaceId = @WorkspaceId and userId = (select top 1 id from users where email = @Email) and status = 'Active' and isDeleted = 0", new { teamLead.WorkspaceId, Email = dev1Email });
        var dev2WorkspaceMember = await factory.ExecuteScalarAsync<int>("select count(1) from workspaceMembers where workspaceId = @WorkspaceId and userId = (select top 1 id from users where email = @Email) and status = 'Active' and isDeleted = 0", new { teamLead.WorkspaceId, Email = dev2Email });
        dev1WorkspaceMember.Should().Be(1);
        dev2WorkspaceMember.Should().Be(1);

        var importPayload = new
        {
            name = $"Shared Migration {Guid.NewGuid():N}",
            description = "Team lead imported shared collection.",
            requests = new object[]
            {
                new { name = "Shared GET", method = "GET", url = "https://api.example.test/shared", bodyType = "none", bodyContent = (string?)null, headers = Array.Empty<object>(), queryParams = Array.Empty<object>(), pathParams = Array.Empty<object>(), folderPath = new[] { "Shared" } }
            }
        };
        var collectionId = await leadClient.ImportCollectionAsync(teamLead.WorkspaceId, importPayload);

        var leadCollections = await GetCollectionNamesAsync(leadClient, teamLead.WorkspaceId, "Shared Migration");
        var dev1Collections = await GetCollectionNamesAsync(dev1Client, teamLead.WorkspaceId, "Shared Migration");
        var dev2Collections = await GetCollectionNamesAsync(dev2Client, teamLead.WorkspaceId, "Shared Migration");
        leadCollections.Should().Equal(dev1Collections);
        dev2Collections.Should().Equal(leadCollections);

        var created = await dev1Client.PostJsonAsync($"/api/collections/{collectionId}/requests", IntegrationTestHelpers.RequestPayload(teamLead.WorkspaceId, collectionId, "Developer added request", "GET", "https://api.example.test/dev-added"));
        var createdJson = await created.ReadJsonAsync();
        createdJson.Succeeded().Should().BeTrue(createdJson.ToJsonString());
        var requestId = createdJson["data"]!["id"]!.GetValue<Guid>();

        (await RequestNamesAsync(leadClient, collectionId)).Should().Contain("Developer added request");
        (await RequestNamesAsync(dev2Client, collectionId)).Should().Contain("Developer added request");

        var update = await dev2Client.PutJsonAsync($"/api/requests/{requestId}", IntegrationTestHelpers.RequestPayload(teamLead.WorkspaceId, collectionId, "Developer two edited request", "POST", "https://api.example.test/dev2-edited", bodyType: "rawJson", bodyContent: "{\"ok\":true}"));
        var updateJson = await update.ReadJsonAsync();
        updateJson.Succeeded().Should().BeTrue(updateJson.ToJsonString());

        (await RequestNamesAsync(leadClient, collectionId)).Should().Contain("Developer two edited request");
        (await RequestNamesAsync(dev1Client, collectionId)).Should().Contain("Developer two edited request");

        var deleteRequest = await dev1Client.DeleteAsync($"/api/requests/{requestId}");
        var deleteRequestJson = await deleteRequest.ReadJsonAsync();
        deleteRequestJson.Succeeded().Should().BeTrue(deleteRequestJson.ToJsonString());
        (await RequestNamesAsync(leadClient, collectionId)).Should().NotContain("Developer two edited request");

        var outsider = await factory.RegisterAsync("workspace-outsider");
        using var outsiderClient = factory.CreateAuthenticatedClient(outsider);
        var blockedCollections = await outsiderClient.GetAsync($"/api/workspaces/{teamLead.WorkspaceId}/collections");
        blockedCollections.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var protectedRequest = await leadClient.PostJsonAsync($"/api/collections/{collectionId}/requests", IntegrationTestHelpers.RequestPayload(teamLead.WorkspaceId, collectionId, "Protected shared request", "GET", "https://api.example.test/protected"));
        var protectedRequestJson = await protectedRequest.ReadJsonAsync();
        protectedRequestJson.Succeeded().Should().BeTrue(protectedRequestJson.ToJsonString());
        var protectedRequestId = protectedRequestJson["data"]!["id"]!.GetValue<Guid>();
        var blockedRequest = await outsiderClient.GetAsync($"/api/requests/{protectedRequestId}");
        blockedRequest.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteTargetId = await leadClient.CreateCollectionAsync(teamLead.WorkspaceId, $"Delete Target {Guid.NewGuid():N}");
        var childRequest = await leadClient.PostJsonAsync($"/api/collections/{deleteTargetId}/requests", IntegrationTestHelpers.RequestPayload(teamLead.WorkspaceId, deleteTargetId, "Delete target child", "GET", "https://api.example.test/delete-target"));
        var childRequestJson = await childRequest.ReadJsonAsync();
        childRequestJson.Succeeded().Should().BeTrue(childRequestJson.ToJsonString());
        var childRequestId = childRequestJson["data"]!["id"]!.GetValue<Guid>();

        var outsiderDelete = await outsiderClient.DeleteAsync($"/api/collections/{deleteTargetId}");
        outsiderDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteCollection = await leadClient.DeleteAsync($"/api/collections/{deleteTargetId}");
        var deleteCollectionJson = await deleteCollection.ReadJsonAsync();
        deleteCollectionJson.Succeeded().Should().BeTrue(deleteCollectionJson.ToJsonString());
        (await GetCollectionNamesAsync(leadClient, teamLead.WorkspaceId, "Delete Target")).Should().BeEmpty();

        var deletedCollectionRequests = await (await leadClient.GetAsync($"/api/collections/{deleteTargetId}/requests")).ReadJsonAsync();
        deletedCollectionRequests.Succeeded().Should().BeFalse(deletedCollectionRequests.ToJsonString());
        var deletedRequestDetail = await (await leadClient.GetAsync($"/api/requests/{childRequestId}")).ReadJsonAsync();
        deletedRequestDetail.Succeeded().Should().BeFalse(deletedRequestDetail.ToJsonString());

        var activityCount = await factory.ExecuteScalarAsync<int>("select count(1) from activityEvents where workspaceId = @WorkspaceId and entityType in ('Collection','Request') and isDeleted = 0", new { teamLead.WorkspaceId });
        activityCount.Should().BeGreaterThan(0);

        for (var i = 0; i < 5; i++)
        {
            await leadClient.CreateCollectionAsync(teamLead.WorkspaceId, $"Shared Pagination {i:00} {Guid.NewGuid():N}");
        }

        var pageOne = await (await dev1Client.GetAsync($"/api/workspaces/{teamLead.WorkspaceId}/collections?offset=0&count=2&searchString=Shared%20Pagination&sorting=name%20asc")).ReadJsonAsync();
        var pageTwo = await (await dev1Client.GetAsync($"/api/workspaces/{teamLead.WorkspaceId}/collections?offset=2&count=2&searchString=Shared%20Pagination&sorting=name%20asc")).ReadJsonAsync();
        PageNames(pageOne).Should().NotIntersectWith(PageNames(pageTwo));
    }

    [Fact]
    public async Task Failed_invite_email_is_logged_and_returned_without_exposing_smtp_secret()
    {
        var teamLead = await factory.LoginAsync();
        using var leadClient = factory.CreateAuthenticatedClient(teamLead);
        var viewerRoleId = await factory.GetRoleIdAsync("Viewer");
        factory.EmailSender.FailNextSend = true;

        var invite = await leadClient.PostJsonAsync($"/api/organizations/{teamLead.OrganizationId}/invites", new
        {
            email = $"failed-email-{Guid.NewGuid():N}@example.com",
            roleId = viewerRoleId,
            workspaceId = teamLead.WorkspaceId,
            message = "join"
        });
        var json = await invite.ReadJsonAsync();

        json.Succeeded().Should().BeTrue(json.ToJsonString());
        json["data"]!["emailDeliveryStatus"]!.GetValue<string>().Should().Be("Failed");
        json.ToJsonString().Should().NotContain("integration-secret-not-real");

        var failedLogCount = await factory.ExecuteScalarAsync<int>("select count(1) from emailDeliveryLogs where organizationId = @OrganizationId and status = 'Failed'", new { teamLead.OrganizationId });
        failedLogCount.Should().BeGreaterThan(0);
    }

    private async Task<JsonNode> SendInviteAsync(HttpClient client, AuthSession session, Guid roleId, string email)
    {
        var response = await client.PostJsonAsync($"/api/organizations/{session.OrganizationId}/invites", new
        {
            email,
            roleId,
            workspaceId = session.WorkspaceId,
            message = "Join the shared Apeiron workspace."
        });
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        json["data"]!["emailDeliveryStatus"]!.GetValue<string>().Should().Be("Sent");
        json["data"]!["workspaceId"]!.GetValue<Guid>().Should().Be(session.WorkspaceId);
        return json["data"]!;
    }

    private async Task<AuthSession> RegisterWithEmailAsync(string email, string fullName)
    {
        using var client = factory.CreateHttpsClient();
        var response = await client.PostJsonAsync("/api/auth/register", new
        {
            email,
            password = "Admin@12345",
            fullName,
            organizationName = $"Personal Org {Guid.NewGuid():N}",
            workspaceName = "Personal Workspace"
        });
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        var data = json["data"]!;
        return new AuthSession(data["accessToken"]!.GetValue<string>(), data["refreshToken"]!.GetValue<string>(), data["organizationId"]!.GetValue<Guid>(), data["workspaceId"]!.GetValue<Guid>());
    }

    private static async Task AcceptAsync(HttpClient client, string token)
    {
        var response = await client.PostJsonAsync("/api/organizations/invites/accept", new { token });
        var json = await response.ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
    }

    private static async Task<string[]> GetCollectionNamesAsync(HttpClient client, Guid workspaceId, string search)
    {
        var json = await (await client.GetAsync($"/api/workspaces/{workspaceId}/collections?offset=0&count=10&searchString={Uri.EscapeDataString(search)}")).ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        return PageNames(json);
    }

    private static async Task<string[]> RequestNamesAsync(HttpClient client, Guid collectionId)
    {
        var json = await (await client.GetAsync($"/api/collections/{collectionId}/requests")).ReadJsonAsync();
        json.Succeeded().Should().BeTrue(json.ToJsonString());
        return json["data"]!["items"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).ToArray();
    }

    private static string[] PageNames(JsonNode json)
    {
        return json["data"]!["items"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).ToArray();
    }
}
