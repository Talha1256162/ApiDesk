using System.Net;
using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class BetaFeedbackIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task Anonymous_user_cannot_submit_beta_feedback()
    {
        using var client = factory.CreateHttpsClient();

        var response = await client.PostJsonAsync("/api/beta-feedback", new
        {
            organizationId = Guid.NewGuid(),
            workspaceId = (Guid?)null,
            category = "UX",
            sentiment = "Neutral",
            rating = 4,
            title = "Improve onboarding",
            message = "The import flow needs a clearer first step.",
            route = "dashboard",
            browserInfo = "integration"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_can_submit_feedback_and_admin_can_review_it()
    {
        var admin = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(admin);

        var create = await client.PostJsonAsync("/api/beta-feedback", FeedbackPayload(admin));
        var createJson = await create.ReadJsonAsync();
        createJson.Succeeded().Should().BeTrue(createJson.ToJsonString());
        var feedbackId = createJson["data"]!["id"]!.GetValue<Guid>();
        createJson["data"]!["status"]!.GetValue<string>().Should().Be("New");

        var list = await client.GetAsync($"/api/organizations/{admin.OrganizationId}/beta-feedback?count=10");
        var listJson = await list.ReadJsonAsync();
        listJson.Succeeded().Should().BeTrue(listJson.ToJsonString());
        listJson["data"]!["items"]!.AsArray().Should().Contain(item => item!["id"]!.GetValue<Guid>() == feedbackId);

        var update = await client.PatchJsonAsync($"/api/beta-feedback/{feedbackId}/status", new { status = "Reviewed", adminNotes = "Accepted for beta polish queue." });
        var updateJson = await update.ReadJsonAsync();
        updateJson.Succeeded().Should().BeTrue(updateJson.ToJsonString());
        updateJson["data"]!["status"]!.GetValue<string>().Should().Be("Reviewed");
        updateJson["data"]!["adminNotes"]!.GetValue<string>().Should().Contain("Accepted");
    }

    [Fact]
    public async Task User_from_another_organization_cannot_read_feedback()
    {
        var admin = await factory.LoginAsync();
        var outsider = await factory.RegisterAsync("feedback-outsider");
        using var outsiderClient = factory.CreateAuthenticatedClient(outsider);

        var denied = await outsiderClient.GetAsync($"/api/organizations/{admin.OrganizationId}/beta-feedback?count=10");

        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Checklist_marks_feedback_complete_after_submission()
    {
        var admin = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(admin);

        var before = await client.GetAsync($"/api/organizations/{admin.OrganizationId}/beta-checklist?workspaceId={admin.WorkspaceId}");
        var beforeJson = await before.ReadJsonAsync();
        beforeJson.Succeeded().Should().BeTrue(beforeJson.ToJsonString());
        beforeJson["data"]!["items"]!.AsArray().First(item => item!["key"]!.GetValue<string>() == "submit-feedback")!["completed"]!.GetValue<bool>().Should().BeFalse();

        var create = await client.PostJsonAsync("/api/beta-feedback", FeedbackPayload(admin));
        var createJson = await create.ReadJsonAsync();
        createJson.Succeeded().Should().BeTrue(createJson.ToJsonString());

        var after = await client.GetAsync($"/api/organizations/{admin.OrganizationId}/beta-checklist?workspaceId={admin.WorkspaceId}");
        var afterJson = await after.ReadJsonAsync();
        afterJson.Succeeded().Should().BeTrue(afterJson.ToJsonString());
        afterJson["data"]!["items"]!.AsArray().First(item => item!["key"]!.GetValue<string>() == "submit-feedback")!["completed"]!.GetValue<bool>().Should().BeTrue();
    }

    private static object FeedbackPayload(AuthSession session) => new
    {
        organizationId = session.OrganizationId,
        workspaceId = session.WorkspaceId,
        category = "UX",
        sentiment = "Neutral",
        rating = 4,
        title = "Make beta onboarding clearer",
        message = "Closed beta users should see exactly where to import Postman collections and where to report issues.",
        route = "dashboard",
        browserInfo = "integration-test"
    };
}
