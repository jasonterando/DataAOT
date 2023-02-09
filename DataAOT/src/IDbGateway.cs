using System.Data;

namespace DataAOT;

public interface IDbGateway<T>: IDisposable where T: class
{
    public string TableName { get; }
    public IList<DbField> Fields { get; }
    
    public IDbConnection Connection { get; }
    
    public string IdentityCommand { get; }
    
    public void Create(T data);
    
    public IEnumerable<T> Retrieve(string? query = null,
        IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);
    
    public void Update(T data, params string[]? propertiesToUpdate);

    public void Delete(T data);
    
    public void BulkUpdate(IDictionary<string, object?> values,
        string query, 
        IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);
    
    public void BulkDelete(string query, IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);

    public IEnumerable<T> ExecuteReader(string sql, IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);
    
    public abstract object? ExecuteScalar(string sql,
        IDictionary<string, object>? parameters = null, bool translatePropertyNames = true);    
    
    public abstract void ExecuteNonQuery(string sql,
        IDictionary<string, object>? parameters = null, bool translatePropertyNames = true);    
}