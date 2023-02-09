using DataAOT.Test.IntegrationTests.Fixtures;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace DataAOT.Test.IntegrationTests;

/// <summary>
/// Test DataAOT using Sqlite
/// </summary>
public class SqliteDatabaseIntegrationTests : DatabaseIntegrationTests, IClassFixture<SqliteIntegrationTestFixture>
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public SqliteDatabaseIntegrationTests(SqliteIntegrationTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }
}

