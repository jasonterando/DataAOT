using DataAOT.Test.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace DataAOT.Test.IntegrationTests;

/// <summary>
/// Test DataAOT using Sqlite
/// </summary>
public class SqlServerIntegrationTests : DatabaseIntegrationTests, IClassFixture<SqlServerIntegrationTestFixture>
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public SqlServerIntegrationTests(SqlServerIntegrationTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }
}

