using FluentAssertions;

namespace ApiForge.IntegrationTests;

[Collection(ApiDeskTestCollection.Name)]
public sealed class PaginationIntegrationTests(ApiDeskWebApplicationFactory factory)
{
    [Fact]
    public async Task Collections_endpoint_applies_search_sorting_and_returns_distinct_pages()
    {
        var admin = await factory.LoginAsync();
        using var client = factory.CreateAuthenticatedClient(admin);
        var prefix = $"Pagination Proof {Guid.NewGuid():N}";

        for (var index = 0; index < 6; index++)
        {
            await client.CreateCollectionAsync(admin.WorkspaceId, $"{prefix} {index:00}");
        }

        var pageOne = await (await client.GetAsync($"/api/workspaces/{admin.WorkspaceId}/collections?offset=0&count=2&searchString={Uri.EscapeDataString(prefix)}&sorting=name%20asc")).ReadJsonAsync();
        var pageTwo = await (await client.GetAsync($"/api/workspaces/{admin.WorkspaceId}/collections?offset=2&count=2&searchString={Uri.EscapeDataString(prefix)}&sorting=name%20asc")).ReadJsonAsync();

        pageOne.Succeeded().Should().BeTrue(pageOne.ToJsonString());
        pageTwo.Succeeded().Should().BeTrue(pageTwo.ToJsonString());

        pageOne["data"]!["totalCount"]!.GetValue<int>().Should().Be(6);
        pageTwo["data"]!["totalCount"]!.GetValue<int>().Should().Be(6);
        pageOne["data"]!["count"]!.GetValue<int>().Should().Be(2);
        pageTwo["data"]!["offset"]!.GetValue<int>().Should().Be(2);

        var firstPageNames = pageOne["data"]!["items"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).ToArray();
        var secondPageNames = pageTwo["data"]!["items"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).ToArray();

        firstPageNames.Should().Equal($"{prefix} 00", $"{prefix} 01");
        secondPageNames.Should().Equal($"{prefix} 02", $"{prefix} 03");
        firstPageNames.Should().NotIntersectWith(secondPageNames);
    }
}
