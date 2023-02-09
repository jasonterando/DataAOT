using System.Text;
using DataAOT.Test.IntegrationTests.Fixtures;
using DataAOT.Test.TestClasses;
using Xunit.Abstractions;

namespace DataAOT.Test.IntegrationTests;

/// <summary>
/// Shared integration tests for all database engines
/// </summary>
public abstract class DatabaseIntegrationTests
{
    private readonly IDatabaseTestFixture _fixture;
    private readonly ITestOutputHelper _outputHelper;

    protected DatabaseIntegrationTests(IDatabaseTestFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    [Fact]
    public void CreateRecords()
    {
        const int count = 5;
        using var gateway = _fixture.CreateUserAccountGateway();
        var records = GenerateDatabaseRecords(gateway, count);
        var ids = new HashSet<int>(records.Select(r => r.ID));
        Assert.Equal(count, ids.Count);
    }

    [Fact]
    public void RetrieveRecordByPropertyName()
    {
        using var gateway = _fixture.CreateUserAccountGateway();
        var record = GenerateDatabaseRecords(gateway, 1).First();
        var acct = gateway.Retrieve($"LastName LIKE '%{record.LastName}%'").SingleOrDefault();
        Assert.NotNull(acct);
    }

    [Fact]
    public void RetrieveRecordByFieldName()
    {
        using var gateway = _fixture.CreateUserAccountGateway();
        var record = GenerateDatabaseRecords(gateway, 1).First();
        var acct = gateway.Retrieve($"last_name LIKE '%{record.LastName}%'").SingleOrDefault();
        Assert.NotNull(acct);
    }

    [Fact]
    public void UpdateByInstance()
    {
        using var gateway = _fixture.CreateUserAccountGateway();
        var record = GenerateDatabaseRecords(gateway, 1).First();
        var oldID = record.ID;
        var oldTimestamp = record.UpdateTimestamp;
        record.LastName = "Smith";
        Thread.Sleep(1500); // make sure the timestamp gets updated
        gateway.Update(record);
        Assert.Equal("Smith", record.LastName);
        // Re-retrieve the record just to be sure...
        record = gateway.Retrieve("id=@id", new Dictionary<string, object> 
            {{"@id", oldID}}
            ).First();
        Assert.Equal("Smith", record.LastName);
        Assert.True(record.UpdateTimestamp > oldTimestamp);
    }
    
    [Fact]
    public void UpdateInBulk()
    {
        using var gateway = _fixture.CreateUserAccountGateway();
        var record = GenerateDatabaseRecords(gateway, 5);
        var ids = record.Select(r => r.ID).ToList();
        var parameters = new Dictionary<string, object>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            parameters.Add($"@id{i}", ids[i]);
        }
        gateway.BulkUpdate(
            new Dictionary<string, object?> { {"last_name", "Smith"} }, 
            $"id in ({string.Join(", ", parameters.Keys)})",
            parameters);
    }
    
    [Fact]
    public void DeleteByInstance()
    {
        using var gateway = _fixture.CreateUserAccountGateway();
        var record = GenerateDatabaseRecords(gateway, 1).First();
        var id = record.ID;
        gateway.Delete(record);
        record = gateway.Retrieve($"id={id}").SingleOrDefault();
        Assert.Null(record);
    }
    
    [Fact]
    public void DeleteInBulk()
    {
        using var gateway = _fixture.CreateUserAccountGateway();
        var record = GenerateDatabaseRecords(gateway, 1).First();
        var id = record.ID;
        gateway.BulkDelete($"id={id}");
        record = gateway.Retrieve($"id={id}").SingleOrDefault();
        Assert.Null(record);
    }
    
    [Fact]
    public void ExecuteReader()
    {
        using var gateway = _fixture.CreateUserAccountGateway();
        var records = GenerateDatabaseRecords(gateway, 10);
        var results = gateway
            .ExecuteReader("SELECT ID, FirstName, LastName, CreateTimestamp, UpdateTimestamp FROM user_accounts " +
                           $"WHERE id IN ({string.Join(", ", records.Select(r => r.ID))})")
            .ToList();
        Assert.Equal(records.Count, results.Count);
        Assert.True(results.All(r => records.Any(r1 => r1.ID == r.ID)));
    }
    
    // ReSharper disable once ReturnTypeCanBeEnumerable.Global
    /// <summary>
    /// Generate random database records
    /// </summary>
    /// <param name="gateway"></param>
    /// <param name="number"></param>
    /// <returns></returns>
    private static IList<UserAccountModel> GenerateDatabaseRecords(IDbGateway<UserAccountModel> gateway, int number)
    {
        var results = new List<UserAccountModel>(number);
        for (var i = 0; i < number; i++)
        {
            var acct = new UserAccountModel
            {
                FirstName = RandomStringGenerator.Create(),
                LastName = RandomStringGenerator.Create()
            };
            gateway.Create(acct);
            results.Add(acct);
        }
        return results;
    }    
    
}
