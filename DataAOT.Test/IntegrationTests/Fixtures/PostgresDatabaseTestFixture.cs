using System.Data;
using DataAOT.Test.TestClasses;
using Npgsql;

namespace DataAOT.Test.IntegrationTests.Fixtures;

public class PostgresIntegrationTestFixture : IDatabaseTestFixture
{
    private readonly string _connectionString;
    private readonly string _connectionStringWithDatabase;
    private readonly string _databaseName;
    private readonly bool _active;
    
    public PostgresIntegrationTestFixture()
    {
        _connectionString = "Server=localhost;Port=5432;Username=postgres;Password=bingo123!;SslMode=Disable;";
        _databaseName = $"test_{DateTime.Now.Ticks}";

        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        // Create a test database
        using var cmdCreate = conn.CreateCommand();
        cmdCreate.CommandText = $"CREATE DATABASE {_databaseName};";
        cmdCreate.ExecuteNonQuery();
        conn.Close();
        
        // Update connection string to use the test database and reopen
        _connectionStringWithDatabase = $"{_connectionString};Database={_databaseName};";
        conn = new NpgsqlConnection(_connectionStringWithDatabase);
        conn.Open();

        // Create test table and trigger
        using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $@"CREATE TABLE user_accounts (
            id SERIAL PRIMARY KEY,
            first_name VARCHAR(100),
            last_name VARCHAR(100),
            create_timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            update_timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        )";
        cmd.ExecuteNonQuery();

        // Create an update trigger
        cmd.CommandText = $@"CREATE FUNCTION update_timestamp() 
            RETURNS trigger AS $update_timestamp$
            BEGIN
            NEW.update_timestamp := NOW();
            RETURN NEW;
            END;
            $update_timestamp$ LANGUAGE plpgsql;
            CREATE TRIGGER update_timestamp BEFORE INSERT OR UPDATE ON user_accounts
            FOR EACH ROW EXECUTE PROCEDURE update_timestamp();";
        cmd.ExecuteNonQuery();
        conn.Close();
        _active = true;
    }

    public IDbGateway<UserAccountModel> CreateUserAccountGateway()
    {
        var conn = new NpgsqlConnection(_connectionStringWithDatabase);
        conn.Open();
        return new UserAccountGateway(conn); 
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (!_active) return;
        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT pg_terminate_backend(pg_stat_activity.pid)
            FROM pg_stat_activity
            WHERE pg_stat_activity.datname = '{_databaseName}'
              AND pid <> pg_backend_pid();
            DROP DATABASE {_databaseName}";
        cmd.CommandType = CommandType.Text;
        cmd.ExecuteNonQuery();
        conn.Close();
    }
}