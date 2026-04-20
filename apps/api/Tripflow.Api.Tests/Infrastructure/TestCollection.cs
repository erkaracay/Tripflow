namespace Tripflow.Api.Tests.Infrastructure;

[CollectionDefinition("api")]
public sealed class TestCollection : ICollectionFixture<PostgresFixture>
{
}
