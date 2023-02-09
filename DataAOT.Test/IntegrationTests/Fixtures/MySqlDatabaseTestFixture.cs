using System.Data;
using DataAOT.Test.TestClasses;
using MySql.Data.MySqlClient;

namespace DataAOT.Test.IntegrationTests.Fixtures;

public class MySqlIntegrationTestFixture :  IDatabaseTestFixture
{
    private const string ConnectionString = "Server=localhost;Port=3306;Uid=root;Pwd=bingo123!;";
    private readonly string _connectionStringWithDatabase;
    private readonly string _databaseName = "test" + DateTime.Now.Ticks;
    private readonly bool _active;
    
    public MySqlIntegrationTestFixture()
    {
        var conn = new MySqlConnection();
        conn.ConnectionString = ConnectionString;
        conn.Open();

        // Create test database
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE {_databaseName}";
        cmd.ExecuteNonQuery();

        _connectionStringWithDatabase = $"{ConnectionString};Database={_databaseName}";
        
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $@"USE {_databaseName};
            CREATE TABLE {_databaseName}.user_accounts (
            id INTEGER PRIMARY KEY AUTO_INCREMENT,
            first_name VARCHAR(100),
            last_name VARCHAR(100),
            create_timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
            update_timestamp DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
        )";
        cmd.ExecuteNonQuery();

        _active = true;
    }

    private IDbConnection CreateConnection()
    {
        var conn = new MySqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public IDbGateway<UserAccountModel> CreateUserAccountGateway()
    {
        var conn = new MySqlConnection(_connectionStringWithDatabase);
        conn.Open();
        return new UserAccountGateway(conn);
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (!_active) return;
        using var conn = new MySqlConnection(ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE {_databaseName}";
        cmd.CommandType = CommandType.Text;
        cmd.ExecuteNonQuery();
    }
}