using System.Data;

namespace DataAOT;

/// <summary>
/// This is a default implementation of IDbGateway, with functionality
/// based upon IDbConnection and DbCommand
/// </summary>
/// <typeparam name="T">The data model that will be handled by this gateway</typeparam>
public abstract class DbGateway<T> : IDbGateway<T> where T : class
{
    /// <summary>
    /// Do *not*
    /// </summary>
    private IDbConnection? _dbConnection;

    private readonly Func<IDbConnection>? _dbConnectionFactory;
    private string? _identityCommand;

    /// <summary>
    /// Initialize the gateway with an active connection
    /// </summary>
    /// <param name="dbConnection"></param>
    protected DbGateway(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    /// <summary>
    /// Initialize the gateway with a connection factory,
    /// which will be called generate a connection when required
    /// </summary>
    /// <param name="dbConnectionFactory"></param>
    protected DbGateway(Func<IDbConnection> dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    /// <summary>
    /// If we used a factory to create the connection
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_dbConnectionFactory == null || _dbConnection == null) return;
        _dbConnection.Close();
        _dbConnection.Dispose();
        _dbConnection = null;
    }

    /// <summary>
    /// Retrieve a database connection, passed in by reference or created by factory.
    /// Ensures the connection is open
    /// </summary>
    /// <exception cref="Exception"></exception>
    public IDbConnection Connection
    {
        get
        {
            var connection = _dbConnection;
            if (connection == null)
            {
                if (_dbConnectionFactory == null)
                {
                    throw new Exception("No connection factory defined");
                }

                connection = _dbConnectionFactory();
            }

            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            _dbConnection = connection;
            return _dbConnection;
        }
    }

    /// <summary>
    /// Returns the SQL function to retrieve the generated auto-increment ID
    /// of the last inserted record.  Uses connection type name to guess,
    /// can be overriden for different SQL flavors. 
    /// </summary>
    public virtual string IdentityCommand
    {
        get
        {
            if (_identityCommand != null) return _identityCommand;
            var name = Connection.GetType().Name.ToLower();
            
            if (name.Contains("mysql"))
            {
                _identityCommand = "LAST_INSERT_ID()";
                return _identityCommand;
            }

            if (name.Contains("npgsql") || name.Contains("postgres"))
            {
                _identityCommand = "lastval()";
                return _identityCommand;
            }
            
            if (name.Contains("sqlconnection"))
            {
                _identityCommand = "SCOPE_IDENTITY()";
                return _identityCommand;
            }

            // ReSharper disable once InvertIf
            if (name.Contains("sqlite"))
            {
                _identityCommand = "last_insert_rowid()";
                return _identityCommand;
            }

            throw new Exception($"Unable to determine identity command, need to override GetIdentityCommand for connection type \"{name}\"");
        }
    }

    /// <summary>
    /// Name of the database table
    /// </summary>
    public abstract string TableName { get; }

    /// <summary>
    /// List of table fields, including type and mapping information
    /// </summary>
    public abstract IList<DbField> Fields { get; }

    // /// <summary>
    // /// Placeholder to create a new record
    // /// </summary>
    // /// <param name="data"></param>
    public abstract void Create(T data);

    //
    /// <summary>
    /// Retrieve records matching the specified criteria
    /// </summary>
    /// <param name="query"></param>
    /// <param name="parameters"></param>
    /// <param name="translatePropertyNames">If True (default) property names will be translated to field names</param>
    /// <returns></returns>
    public abstract IEnumerable<T> Retrieve(string? query = null,
        IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);

    /// <summary>
    /// Update the specified record, refreshes record variable with current
    /// values in database after update, record must have either key and/or
    /// generated identity fields
    /// </summary>
    /// <param name="record">Record to update</param>
    /// <param name="propertiesToUpdate">Limit update to specified field (or property) names (Optional)</param>
    public abstract void Update(T record, params string[]? propertiesToUpdate);

    /// <summary>
    /// Deletes the specified record, record must have either key and/or
    /// generated identity fields    /// </summary>
    /// <param name="data"></param>
    public abstract void Delete(T data);

    /// <summary>
    /// Update records with specified values (named by property or field) filtered
    /// by criteria.  Returns affected records.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="query"></param>
    /// <param name="parameters"></param>
    /// <param name="translatePropertyNames"></param>
    public abstract void BulkUpdate(IDictionary<string, object?> values,
        string query,
        IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);

    /// <summary>
    /// Delete records matching the specified criteria
    /// </summary>
    /// <param name="query"></param>
    /// <param name="parameters"></param>
    /// <param name="translatePropertyNames"></param>
    public abstract void BulkDelete(string query, IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);

    /// <summary>
    /// Executes the SQL statement and populates records based on the results
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <param name="translatePropertyNames"></param>
    /// <returns></returns>
    public abstract IEnumerable<T> ExecuteReader(string sql, IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);
    
    /// <summary>
    /// Executes the SQL statement and returns scalar result
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <param name="translatePropertyNames"></param>
    /// <returns></returns>
    public abstract object? ExecuteScalar(string sql,
        IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);    
    
    /// <summary>
    /// Executes the SQL statement
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="parameters"></param>
    /// <param name="translatePropertyNames"></param>
    /// <returns></returns>
    public abstract void ExecuteNonQuery(string sql,
        IDictionary<string, object>? parameters = null,
        bool translatePropertyNames = true);
}