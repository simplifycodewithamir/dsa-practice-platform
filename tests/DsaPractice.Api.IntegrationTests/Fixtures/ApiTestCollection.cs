using Xunit;

namespace DsaPractice.Api.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<ApiWebApplicationFactory>
{
    public const string Name = "Api integration tests";
}
