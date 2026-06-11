namespace ApiForge.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiDeskTestCollection : ICollectionFixture<ApiDeskWebApplicationFactory>
{
    public const string Name = "Apeiron integration tests";
}
