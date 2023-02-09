using System.Data;
using System.Data.SqlClient;
using DataAOT.Test.TestClasses;

namespace DataAOT.Test.IntegrationTests.Fixtures;

public class SqlServerIntegrationTestFixture : IDatabaseTestFixture
{
    private const string ConnectionString = "Server=localhost;User Id=sa;Password=bingo123!;";
    private readonly string _connectionStringWithDatabase;
    private readonly string _databaseName = "TEST" + DateTime.Now.Ticks;
    private readonly bool _active;
    
    public SqlServerIntegrationTestFixture()
    {
        var conn = new SqlConnection();
        conn.ConnectionString = ConnectionString;
        conn.Open();
        
        // Create our test table
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE {_databaseName};";
        cmd.ExecuteNonQuery();

        _connectionStringWithDatabase = $"{ConnectionString};Database={_databaseName};";

        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $@"USE {_databaseName};
            CREATE TABLE user_accounts (
            id INT NOT NULL IDENTITY PRIMARY KEY,
            first_name VARCHAR(100),
            last_name VARCHAR(100),
            create_timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
            update_timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
        )";
        cmd.ExecuteNonQuery();

        // Create an update trigger
        cmd.CommandText = $@"CREATE TRIGGER .trgAfterUpdate ON user_accounts AFTER INSERT, UPDATE 
            AS UPDATE f set update_timestamp=CURRENT_TIMESTAMP 
            FROM user_accounts AS f 
            INNER JOIN inserted 
            AS i 
            ON f.id = i.id;";
        cmd.ExecuteNonQuery();
        
        _active = true;
    }

    public IDbGateway<UserAccountModel> CreateUserAccountGateway()
    {
        var conn = new SqlConnection(_connectionStringWithDatabase);
        conn.Open();
        return new UserAccountGateway(conn);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (!_active) return;
        using var conn = new SqlConnection(ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $@"USE master;
        ALTER DATABASE {_databaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        DROP DATABASE {_databaseName};";
        cmd.ExecuteNonQuery();
    }
}