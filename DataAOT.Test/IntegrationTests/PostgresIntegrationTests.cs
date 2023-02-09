using DataAOT.Test.IntegrationTests.Fixtures;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DataAOT.Test.IntegrationTests;

/// <summary>
/// Test DataAOT using Sqlite
/// </summary>
public class PostgresIntegrationTests : DatabaseIntegrationTests, IClassFixture<PostgresIntegrationTestFixture>
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public PostgresIntegrationTests(PostgresIntegrationTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }
}

