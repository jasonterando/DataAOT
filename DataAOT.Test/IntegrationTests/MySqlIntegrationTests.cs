using DataAOT.Test.IntegrationTests.Fixtures;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DataAOT.Test.IntegrationTests;

/// <summary>
/// Test DataAOT using Sqlite
/// </summary>
public class MySqlDatabaseIntegrationTests : DatabaseIntegrationTests, IClassFixture<MySqlIntegrationTestFixture>
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public MySqlDatabaseIntegrationTests(MySqlIntegrationTestFixture fixture)
        : base(fixture, new TestOutputHelper())
    {
    }
}
