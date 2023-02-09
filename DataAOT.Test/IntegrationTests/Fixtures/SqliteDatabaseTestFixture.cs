using System.Data;
using DataAOT.Test.TestClasses;
using Microsoft.Data.Sqlite;

namespace DataAOT.Test.IntegrationTests.Fixtures;

/// <summary>
/// Create a test fixture to bring up a Sqlite in-memory database that's available during unit tests.
/// Note that we keep the starting connection open for the lifetime of the fixture so that the database
/// doesn't "go away" in between tests
/// </summary>
public class SqliteIntegrationTestFixture : IDatabaseTestFixture
{
    private const string ConnectionString = "Data Source=file:AoTIntegrationTest?mode=memory&cache=shared";
    private readonly IDbConnection _connection;
    
    public SqliteIntegrationTestFixture()
    {
        _connection = CreateConnection();
        _connection.Open();
 
        // Create our test table

        using var cmd = _connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = @"CREATE TABLE user_accounts (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            first_name VARCHAR(100),
            last_name VARCHAR(100),
            create_timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
            update_timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
        )";
        cmd.ExecuteNonQuery();

        // Create an update trigger
        cmd.CommandText = @"CREATE TRIGGER [UpdateLastTimestamp] 
                    BEFORE UPDATE OF first_name, last_name ON user_accounts 
                    FOR EACH ROW    
                    BEGIN
                    UPDATE user_accounts SET update_timestamp=CURRENT_TIMESTAMP WHERE id=OLD.id;
                    END;";
        cmd.ExecuteNonQuery();
    }

    private static IDbConnection CreateConnection() => new SqliteConnection(ConnectionString);

    public IDbGateway<UserAccountModel> CreateUserAccountGateway() => new UserAccountGateway(CreateConnection);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Close();
        _connection.Dispose();
    }
}