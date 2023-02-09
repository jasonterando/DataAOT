using System.Data;
using System.Text.Json;
using DataAOT.Test.Application.Models;
using Microsoft.Data.Sqlite;

namespace DataAOT.Test.Application;

public static class Demonstration
{
    public static void Main()
    {
        const string connectionString = "Data Source=file::memory:?cache=shared";
        // Open up the Sqlite connection
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        
        // Create our test table
        var cmd = connection.CreateCommand();
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
        
        // Set up the user account gateway and do some stuff...
        using var gateway = new UserAccountGateway(() => new SqliteConnection(connectionString));
        
        // Create a record
        Console.WriteLine("Creating Account #1: John Doe");
        var acct1 = new UserAccount
        {
            FirstName = "John",
            LastName = "Doe"
        };
        gateway.Create(acct1);
        Console.WriteLine($"Account #1: {acct1}");

        // Create another record
        Console.WriteLine("Creating Account #2: Jane Smith");
        var acct2 = new UserAccount
        {
            FirstName = "Jane",
            LastName = "Smith"
        };
        gateway.Create(acct2);
        Console.WriteLine($"Account #2: {acct2}");

        // Retrieve records
        Console.WriteLine("Retrieving records with First names starting with \"J\"");
        var matches = gateway.Retrieve("FirstName LIKE 'J%'");
        foreach (var match in matches)
        {
            Console.WriteLine(match.ToString());
        }
        
        // Delete the second record
        Console.WriteLine("Deleting Account #2");
        // gateway.Delete($"ID={acct2.ID}");
        gateway.Delete(acct2);
        
        // Make sure it got deleted
        var match1 = gateway.Retrieve($"ID={acct2.ID}").SingleOrDefault();
        if (match1 == null)
            Console.WriteLine("Account #2 was deleted successfully");
        else
            throw new Exception("Unable to delete Account #2");
        
        Thread.Sleep(1000); // make sure we get an updated timestamp
        
        Console.WriteLine("Change Account #1 first name to Jim");
        acct1.FirstName = "Jim";
        gateway.Update(acct1, "FirstName");
        Console.WriteLine($"Updated: {acct1}");
        
        // Pull up the first record (Doe)
        Console.WriteLine("Searching for first account with last name like DOE");
        var match2 = gateway.Retrieve("last_name LIKE '%DOE%'").SingleOrDefault();
        if (match2 == null) throw new Exception("Unable to find account with last name like '%DOE%'");
        Console.WriteLine($"Matched: {match2}");
    }
}

