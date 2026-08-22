using Xunit;

namespace DsaPractice.Api.Tests.Fixtures;

[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<ApiWebApplicationFactory>
{
    public const string Name = "Api integration tests";
}
