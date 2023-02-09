using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataAOT.Generator;

/// <summary>
/// This is a nasty mechanism to build out the per-model partial class.
/// This would be much better implemented using a scripting mechanism
/// like Scriban, but I've been having trouble getting it to work in
/// a Source Generator 
/// </summary>
public class DBGatewayTemplateRenderer
{
    private readonly string _namespaceName;
    private readonly string _gatewayClassName;
    private readonly string _modelClassName;
    private readonly string _tableName;
    private readonly IList<FieldInfo> _fields;
    private readonly string _fieldNameList;

    private readonly IEnumerable<FieldInfo> _nonGeneratedFields;

    private readonly IEnumerable<FieldInfo> _generatedFields;
    private readonly string _generatedFieldNameList;

    private readonly FieldInfo? _identityField;
    private readonly IList<FieldInfo> _keyFields;

    public DBGatewayTemplateRenderer(BaseTypeDeclarationSyntax gatewaySyntax, ISymbol gatewaySymbol,
        INamedTypeSymbol modelTypeSymbol)
    {
        _namespaceName = gatewaySyntax.GetNamespace();
        _gatewayClassName = gatewaySymbol.Name ?? throw new Exception("Unable to get gateway class name");
        _modelClassName = modelTypeSymbol.Name;

        // Determine the table name
        var attrTable = modelTypeSymbol.GetAttributes()
                            .SingleOrDefault(a => a.AttributeClass?.Name == "TableAttribute")
                        ?? throw new Exception("Data model does not have a Table attribute");

        _tableName = attrTable.GetAttributeStringValue(0)
                     ?? attrTable.GetAttributeStringValue("name")
                     ?? _gatewayClassName;

        // TODO:  Deal with primary key

        // Iterate through data model properties to get fields
        var properties = modelTypeSymbol.GetAllMembers()
            .Where(x => x.Kind == SymbolKind.Property)
            .OfType<IPropertySymbol>()
            .Where(x => x.DeclaredAccessibility == Accessibility.Public)
            .Where(x => !x.IsStatic)
            .Where(x => !x.IsIndexer)
            .ToList();

        _fields = new List<FieldInfo>();
        foreach (var property in properties)
        {
            var propertyName = property.Name;
            var attributes = property.GetAttributes();

            var attrColumn = attributes.FirstOrDefault(a =>
                a.AttributeClass?.Name == "ColumnAttribute");
            if (attrColumn == null) continue;
            var columnName = attrColumn.GetAttributeStringValue(0) ??
                             attrColumn.GetAttributeStringValue("name") ?? propertyName;

            var generated = DatabaseGeneratedOption.None;
            var attrGenerated = attributes.FirstOrDefault(a =>
                a.AttributeClass?.Name == "DatabaseGeneratedAttribute");
            if (attrGenerated != null)
            {
                generated = attrGenerated.GetAttributeEnumValue<DatabaseGeneratedOption>(0);
                if (generated == default)
                {
                    generated = attrGenerated.GetAttributeEnumValue<DatabaseGeneratedOption>("databaseGeneratedOption");
                }
            }

            var isKey = attributes.Any(a => a.AttributeClass?.Name == "KeyAttribute");

            var isNullableAnnotated = property.Type.NullableAnnotation == NullableAnnotation.Annotated;
            var isNullableGeneric = !isNullableAnnotated && property.Type.Name == "Nullable";
            var isNullable = isNullableAnnotated || isNullableGeneric;

            string? propertyTypeName;

            if (isNullableGeneric)
            {
                propertyTypeName = property.Type.OriginalDefinition.Name;
            }
            else if (isNullableGeneric)
            {
                propertyTypeName = (property.Type as INamedTypeSymbol)?.TypeArguments.First().Name;
            }
            else if (property.Type is IArrayTypeSymbol arrType)
            {
                propertyTypeName = arrType.ElementType.Name + "[]";
            }
            else
            {
                propertyTypeName = property.Type.Name;
            }

            if (propertyTypeName == null)
            {
                throw new ArgumentException("Unable to derive data type", propertyName);
            }

            var matchingType = DbTypeMap.Keys.FirstOrDefault(m => m.Name == propertyTypeName);
            if (matchingType == null)
            {
                throw new ArgumentException("Unable to assign database data type", propertyName);
            }

            _fields.Add(new FieldInfo
            {
                PropertyName = property.Name,
                PropertyTypeName = propertyTypeName,
                FieldName = columnName,
                FieldType = DbTypeMap[matchingType],
                IsNullable = isNullable,
                IsKey = isKey,
                DatabaseGenerated = generated == default ? DatabaseGeneratedOption.None : generated
            });
        }

        _fieldNameList = string.Join(", ", _fields.Select(f => f.FieldName));

        _nonGeneratedFields = _fields
            .Where(f => f.DatabaseGenerated == DatabaseGeneratedOption.None)
            .ToList();

        _generatedFields = _fields
            .Where(f => f.DatabaseGenerated != DatabaseGeneratedOption.None)
            .ToList();
        _generatedFieldNameList = string.Join(", ", _generatedFields.Select(f => f.FieldName));

        _identityField = _fields.FirstOrDefault(f => f.DatabaseGenerated == DatabaseGeneratedOption.Identity);
        _keyFields = _fields.Where(f => f.IsKey).ToList();
    }

    /// <summary>
    /// Render the gateway class file
    /// </summary>
    /// <returns></returns>
    public string Render()
    {
        var sb = new StringBuilder(4096);
        RenderFileStart(sb);
        RenderCreate(sb);
        RenderRetrieve(sb);
        RenderUpdate(sb);
        RenderDelete(sb);
        RenderBulkUpdate(sb);
        RenderBulkDelete(sb);
        RenderExecuteReader(sb);
        RenderExecuteScalar(sb);
        RenderExecuteNonQuery(sb);
        RenderSubstitutePropertyNames(sb);
        RenderPopulateFromDataReader(sb);
        RenderFileEnd(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Render the start of the gateway class file
    /// </summary>
    /// <param name="sb"></param>
    private void RenderFileStart(StringBuilder sb)
    {
        sb.Append(@$"using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using DataAOT;

namespace {_namespaceName}
{{
    #nullable enable
    public partial class {_gatewayClassName}
    {{
        private readonly IList<DbField> _fields = new List<DbField> {{");

        foreach (var field in _fields)
        {
            sb.Append($@"
            new () {{
                PropertyName = ""{field.PropertyName}"",
                PropertyType = typeof({field.PropertyTypeName}),
                FieldName = ""{field.FieldName}"",
                FieldType = DbType.{field.FieldType},
                IsNullable = {(field.IsNullable ? "true" : "false")},
                IsKey = {(field.IsKey ? "true" : "false")},
                DatabaseGenerated = DatabaseGeneratedOption.{field.DatabaseGenerated}
            }},");
        }

        sb.Append($@"
        }};

        public {_gatewayClassName}(IDbConnection dbConnection): base(dbConnection) {{}}
            
        public {_gatewayClassName}(Func<IDbConnection> dbConnectionFactory): base(dbConnectionFactory) {{}}

        public override string TableName {{ get => ""{_tableName}""; }}
            
        public override IList<DbField> Fields {{ get => _fields; }}");
    }

    /// <summary>
    /// Render create record function
    /// </summary>
    /// <param name="sb"></param>
    private void RenderCreate(StringBuilder sb)
    {
        var insertParameterList = string.Join(", ",
            _nonGeneratedFields.Select(f => $@"@{f.PropertyName}"));
        var insertFieldNameList = string.Join(", ",
            _nonGeneratedFields.Select(f => f.FieldName));

        sb.Append($@"

        public override void Create({_modelClassName} data)
        {{
            var sql = $""INSERT INTO {_tableName} ({insertFieldNameList}) VALUES ({insertParameterList})"";");
        var retrieveGeneratedFields = _generatedFields.Any() && (_keyFields.Any() || _identityField != null);
        if (retrieveGeneratedFields)
        {
            if (_identityField == null)
            {
                var keyFieldsWhereParams = _keyFields.Select(f => $@"{f.FieldName}=@{f.PropertyName}");
                sb.Append($@"
            sql += "";SELECT {_generatedFieldNameList} FROM {_tableName} WHERE {keyFieldsWhereParams};""");
            }
            else
            {
                sb.Append(@$"
            sql += $"";SELECT {_generatedFieldNameList} FROM {_tableName} WHERE {_identityField.FieldName}={{IdentityCommand}};"";");
            }
        }

        sb.Append(@"
            using var cmd = Connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;");

        foreach (var field in _nonGeneratedFields)
        {
            var paramName = $@"param_{field.PropertyName}";
            sb.Append($@"

            var {paramName} = cmd.CreateParameter();
            {paramName}.ParameterName = ""@{field.PropertyName}"";
            {paramName}.Value = {(field.IsNullable ? $@"(data.{field.PropertyName} == null) ? DBNull.Value : " : "")}data.{field.PropertyName};
            {paramName}.DbType = DbType.{field.FieldType.ToString()};
            {paramName}.Direction = ParameterDirection.Input;
            cmd.Parameters.Add({paramName});");
        }

        sb.Append(@"

            ");

        if (retrieveGeneratedFields)
        {
            sb.Append(@"var rdr = cmd.ExecuteReader();
            if (! rdr.Read()) throw new Exception(""Unable to access inserted record"");");
            var i = 0;
            foreach (var generatedField in _generatedFields)
            {
                sb.Append(GenerateFieldConverter(generatedField, i++, "data"));
            }

            sb.Append(@"
            rdr.Close();");
        }

        else
        {
            sb.Append("cmd.ExecuteNonQuery();");
        }

        sb.Append(@"
        }");
    }

    /// <summary>
    /// Render the retrieval function
    /// </summary>
    /// <param name="sb"></param>
    private void RenderRetrieve(StringBuilder sb)
    {
        sb.Append($@"

        public override IEnumerable<{_modelClassName}> Retrieve(string? query = null,
            IDictionary<string, object>? parameters = null,
            bool translatePropertyNames = true)
        {{
            var sql = ""SELECT {_fieldNameList} FROM {_tableName}"";
            if (! string.IsNullOrEmpty(query)) {{
                sql += $"" WHERE {{(translatePropertyNames ? SubstitutePropertyNames(query) : query)}}"";
            }}
            using var cmd = Connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;
            if (parameters != null) {{
                AddParameters(cmd, parameters);    
            }}
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {{
                var result = new {_modelClassName}();
                PopulateFromDataReader(rdr, result);
                yield return result;
            }}
        }}");
    }

    /// <summary>
    /// Render the update function
    /// </summary>
    /// <param name="sb"></param>
    private void RenderUpdate(StringBuilder sb)
    {
        sb.Append($@"

        public override void Update({_modelClassName} data, params string[]? propertiesToUpdate)
        {{");
        var updateCriteriaFields = _identityField == null
            ? _keyFields
            : new List<FieldInfo> {_identityField};
        
        if (! updateCriteriaFields.Any())
        {
            sb.Append(@"
            throw new Exception(""Entities without key or generated identity fields must be updated using BulkUpdate"");");
        }
        else
        {
            var updateWhere = string.Join(" AND ", updateCriteriaFields.Select(f =>
                $"{f.FieldName}=@{f.FieldName}"));

            var retrieveGeneratedFields = _generatedFields.Any();
            var sqlRetrieve = retrieveGeneratedFields ?
                $"SELECT {_fieldNameList} FROM {_tableName} WHERE {updateWhere};"
                : "";

            sb.Append(@$"
            var fieldsToUpdate = propertiesToUpdate?.Select(fieldName => {{
                var matches = Fields.Where(f => f.FieldName == fieldName || f.PropertyName == fieldName);
                if (! matches.Any()) {{
                    throw new Exception(""${{field}} is not a valid field or property name"");
                }}
                var match = matches.First();
                if (match.DatabaseGenerated != DatabaseGeneratedOption.None) {{
                    throw new Exception(""${{field}} is a generated field"");
                }}
                return match;
            }});
            
            if(fieldsToUpdate?.Any() != true) {{
                fieldsToUpdate = Fields.Where(f => f.DatabaseGenerated == DatabaseGeneratedOption.None);
            }}
            
            var updateParams = string.Join("", "", fieldsToUpdate.Select(f => $""{{f.FieldName}}=@{{f.FieldName}}""));

            using var cmd = Connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $""UPDATE {_tableName} SET {{updateParams}} WHERE {updateWhere};{sqlRetrieve}"";");
            
            // Build out parameters
            foreach (var field in _fields.Where(f => f.DatabaseGenerated == DatabaseGeneratedOption.None))
            {
                sb.Append($@"

            if (fieldsToUpdate.Any(f => f.FieldName == ""{field.FieldName}""))
            {{
                var upd_param = cmd.CreateParameter();
                upd_param.ParameterName = ""@{field.FieldName}"";
                upd_param.Value = data.{field.PropertyName}; 
                upd_param.DbType = DbType.{field.FieldType};
                upd_param.Direction = ParameterDirection.Input;
                cmd.Parameters.Add(upd_param);
            }}");
            }

            // Where clause for the UPDATE and then the SELECT statement
            var paramIndex = 0;
            foreach (var field in updateCriteriaFields)
            {
                var paramName = $"param{paramIndex++}";
                sb.Append($@"

            var {paramName} = cmd.CreateParameter();
            {paramName}.ParameterName = ""@{field.FieldName}"";
            {paramName}.Value = {(field.IsNullable ? $@"(data.{field.PropertyName} == null) ? DBNull.Value : " : "")}data.{field.PropertyName};
            {paramName}.DbType = DbType.{field.FieldType.ToString()};
            {paramName}.Direction = ParameterDirection.Input;
            cmd.Parameters.Add({paramName});");
            }

            if (retrieveGeneratedFields)
            {
                
            }
            else
            {
                sb.Append(@"
            cmd.ExecuteNonQuery();");
            }
            
            sb.Append(@"

            using var rdr = cmd.ExecuteReader();
            if(! rdr.Read()) throw new Exception(""Unable to retrieve updated record"");

            PopulateFromDataReader(rdr, data);");
        }

        sb.Append(@"
        }");
    }

    /// <summary>
    /// Render the delete function
    /// </summary>
    /// <param name="sb"></param>
    private void RenderDelete(StringBuilder sb)
    {
        sb.Append($@"

        public override void Delete({_modelClassName} data)
        {{");
        var criteriaFields = _fields.Where(f => f.IsKey).ToArray();
        if (!criteriaFields.Any())
        {
            criteriaFields = _fields.Where(f => f.DatabaseGenerated == DatabaseGeneratedOption.Identity)
                .ToArray();
        }

        if (!criteriaFields.Any())
        {
            sb.Append(@"
            throw new Exception(""Entities without key or generated identity fields must be deleted by using BulkDelete"");");
        }
        else
        {
            var where = string.Join(" AND ", criteriaFields.Select(f =>
                $"{f.FieldName}=@{f.FieldName}"));

            sb.Append(@$"
            using var cmd = Connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = ""DELETE FROM {_tableName} WHERE {where}"";");
            for (var i = 0; i < criteriaFields.Length; i++)
            {
                var field = criteriaFields[i];
                var paramName = $"param{i}";
                sb.Append($@"

            var {paramName} = cmd.CreateParameter();
            {paramName}.ParameterName = ""@{field.FieldName}"";
            {paramName}.Value = {(field.IsNullable ? $@"(data.{field.PropertyName} == null) ? DBNull.Value : " : "")}data.{field.PropertyName};
            {paramName}.DbType = DbType.{field.FieldType.ToString()};
            {paramName}.Direction = ParameterDirection.Input;
            cmd.Parameters.Add({paramName});");
            }

            sb.Append(@"

            cmd.ExecuteNonQuery();");
        }

        sb.Append(@"
        }");
    }

    /// <summary>
    /// Render the bulk update function
    /// </summary>
    /// <param name="sb"></param>
    private void RenderBulkUpdate(StringBuilder sb)
    {
        sb.Append($@"

        public override void BulkUpdate(IDictionary<string, object?> values, 
            string query, IDictionary<string, object>? parameters = null, 
            bool translatePropertyNames = true)
        {{
            if(! values.Any()) {{
                throw new ArgumentException(""One or more update values are required"", ""values"");    
            }}

            if(string.IsNullOrEmpty(query)) {{
                throw new ArgumentException(""Filtering query is required"", ""query"");
            }}

            var fieldsToUpdate = new List<DbField>(values.Count);
            using var cmdUpdate = Connection.CreateCommand();
            cmdUpdate.CommandType = CommandType.Text;                
            foreach(var name in values.Keys) {{
                var matches = Fields.Where(f => f.PropertyName == name || f.FieldName == name);
                if(! matches.Any()) {{
                    throw new ArgumentException(""Invalid field: {{name}}"", ""values"");
                }}
                var field = matches.First();
                var upd_param = cmdUpdate.CreateParameter();
                upd_param.ParameterName = $""@upd_{{field.FieldName}}"";
                upd_param.Value = values[name]; 
                upd_param.DbType = field.FieldType;
                upd_param.Direction = ParameterDirection.Input;
                cmdUpdate.Parameters.Add(upd_param);
                fieldsToUpdate.Add(field);
            }}
                
            var updateParams = string.Join("", "", fieldsToUpdate.Select(f => $""{{f.FieldName}}=@upd_{{f.FieldName}}""));
            var where = translatePropertyNames ? SubstitutePropertyNames(query) : query;
            cmdUpdate.CommandText = $""UPDATE {_tableName} SET {{updateParams}} WHERE {{where}};"";
            if (parameters != null) {{
                AddParameters(cmdUpdate, parameters);    
            }}
                
            cmdUpdate.ExecuteNonQuery();
        }}");
    }

    /// <summary>
    /// Render the bulk delete function
    /// </summary>
    /// <param name="sb"></param>
    private void RenderBulkDelete(StringBuilder sb)
    {
        sb.Append($@"

        public override void BulkDelete(string query, IDictionary<string, object>? parameters = null,
            bool translatePropertyNames = true)
        {{
            if (string.IsNullOrEmpty(query)) {{
                throw new Exception(""Delete requires query"");
            }}
            using var cmd = Connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = $""DELETE FROM {_tableName} WHERE {{(translatePropertyNames ? SubstitutePropertyNames(query) : query)}}"";
            if (parameters != null) {{
                AddParameters(cmd, parameters);    
            }}
            cmd.ExecuteNonQuery();
        }}");
    }

    /// <summary>
    /// Render the ExecuteReader function
    /// </summary>
    /// <param name="sb"></param>
    private void RenderExecuteReader(StringBuilder sb)
    {
        sb.Append($@"
        
        public override IEnumerable<{_modelClassName}> ExecuteReader(string sql,
            IDictionary<string, object>? parameters = null,
            bool translatePropertyNames = true)
        {{
            using var cmd = Connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = translatePropertyNames ? SubstitutePropertyNames(sql) : sql;
            if (parameters != null) {{
                AddParameters(cmd, parameters);    
            }}
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {{
                var result = new {_modelClassName}();
                PopulateFromDataReader(rdr, result);
                yield return result;
            }}
        }}");
    }

    /// <summary>
    /// Render the ExecuteScalar function
    /// </summary>
    /// <param name="sb"></param>
    private static void RenderExecuteScalar(StringBuilder sb)
    {
        sb.Append(@"
        
        public override object? ExecuteScalar(string sql,
            IDictionary<string, object>? parameters = null,
            bool translatePropertyNames = true)
        {{
            using var cmd = Connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;
            if (parameters != null) {{
                AddParameters(cmd, parameters);    
            }}
            return cmd.ExecuteScalar(); 
        }}");
    }

    /// <summary>
    /// Render the ExecuteNonQuery function
    /// </summary>
    /// <param name="sb"></param>
    private static void RenderExecuteNonQuery(StringBuilder sb)
    {
        sb.Append(@"
        
        public override void ExecuteNonQuery(string sql,
            IDictionary<string, object>? parameters = null,
            bool translatePropertyNames = true)
        {{
            using var cmd = Connection.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;
            if (parameters != null) {{
                AddParameters(cmd, parameters);    
            }}
            cmd.ExecuteNonQuery(); 
        }}");
    }

    /// <summary>
    /// Render the SubstitutePropertyNames function
    /// </summary>
    /// <param name="sb"></param>
    private static void RenderSubstitutePropertyNames(StringBuilder sb)
    {
        sb.Append($@"

        protected string SubstitutePropertyNames(string text)
        {{
            foreach (var field in Fields)
            {{
                text = Regex.Replace(text, $@""\b{{field.PropertyName}}\b"", field.FieldName, RegexOptions.IgnoreCase);
            }}
            return text;
        }}");
    }

    /// <summary>
    /// Render the PopulateFromDataReader function
    /// </summary>
    /// <param name="sb"></param>
    private void RenderPopulateFromDataReader(StringBuilder sb)
    {
        sb.Append($@"

        protected void PopulateFromDataReader(IDataReader rdr, {_modelClassName} data)
        {{");
        for (var i = 0; i < _fields.Count; i++)
        {
            sb.Append(GenerateFieldConverter(_fields[i], i, "data"));
        }

        sb.Append(@"
        }");
    }

     private void RenderGetUniqueValues(StringBuilder sb)
     {
         sb.Append(@"

");

     }

    /// <summary>
    /// Render the end of the class file
    /// </summary>
    /// <param name="sb"></param>
    private void RenderFileEnd(StringBuilder sb)
    {
        sb.Append(@"

        private void AddParameters(IDbCommand command, IDictionary<string, object> parameters)
        {
            foreach(var parameter in parameters)
            {
                var t = parameter.Value.GetType();
                var dbt = DbTypeMap.ContainsKey(t) ? DbTypeMap[t] : DbType.String;
                var p = command.CreateParameter();
                p.ParameterName = parameter.Key;
                p.Value = dbt == DbType.String ? parameter.Value.ToString() : parameter.Value;
                p.DbType = dbt;
                p.Direction = ParameterDirection.Input;
                command.Parameters.Add(p);
            }
        }

        private static readonly Dictionary<Type, DbType> DbTypeMap = new()
        {");
        foreach (var t in DbTypeMap)
        {
            sb.Append($@"
            {{ typeof({t.Key.Name}), DbType.{t.Value} }},");
        }

        sb.Append(@"
        };
    }
    #nullable restore
}");
    }

    /// <summary>
    /// Generate code to convert DataReader variable (GetInt32, etc.) to a property
    /// </summary>
    /// <param name="field"></param>
    /// <param name="fieldNumber"></param>
    /// <param name="dataObjectName"></param>
    /// <returns></returns>
    private static string GenerateFieldConverter(FieldInfo field, int fieldNumber, string dataObjectName)
    {
        var sb1 = new StringBuilder(@$"
            
            {(fieldNumber == 0 ? "object " : "")}value = rdr[{fieldNumber}];
            if(value == DBNull.Value) 
            {{");
        sb1.Append(field.IsNullable
            ? $@"
                {dataObjectName}.{field.PropertyName} = null;"
            : $@"
                throw new Exception(""Database value of Null cannot be assigned to {field.PropertyName}"");");

        sb1.Append($@"
            }}
            else
            {{");
        var setter = field.FieldType switch
        {
            DbType.String => $@"{dataObjectName}.{field.PropertyName} = value.ToString()!;",
            DbType.StringFixedLength =>
                $@"{dataObjectName}.{field.PropertyName} = Convert.ToString(value)!.ToCharArray();",
            DbType.Binary => $@"{dataObjectName}.{field.PropertyName} = (value as byte[])!;",
            DbType.DateTimeOffset =>
                $@"{dataObjectName}.{field.PropertyName} = DateTimeOffset.Parse(value.ToString()!);",
            _ => $@"{dataObjectName}.{field.PropertyName} = Convert.To{field.PropertyTypeName}(value);"
        };

        sb1.Append($@"
                {setter}
            }}");

        return sb1.ToString();
    }

    [SuppressMessage("ReSharper", "BuiltInTypeReferenceStyle")]
    private static readonly Dictionary<Type, DbType> DbTypeMap =
        new()
        {
            {typeof(Byte), DbType.Byte},
            {typeof(SByte), DbType.SByte},
            {typeof(Int16), DbType.Int16},
            {typeof(UInt16), DbType.UInt16},
            {typeof(Int32), DbType.Int32},
            {typeof(UInt32), DbType.UInt32},
            {typeof(Int64), DbType.Int64},
            {typeof(UInt64), DbType.UInt64},
            {typeof(Single), DbType.Single},
            {typeof(Double), DbType.Double},
            {typeof(Decimal), DbType.Decimal},
            {typeof(Boolean), DbType.Boolean},
            {typeof(String), DbType.String},
            {typeof(Char[]), DbType.StringFixedLength},
            {typeof(Guid), DbType.Guid},
            {typeof(DateTime), DbType.DateTime},
            {typeof(DateTimeOffset), DbType.DateTimeOffset},
            {typeof(Byte[]), DbType.Binary}
        };
}

internal class FieldInfo
{
    public string PropertyName { get; set; } = default!;
    public string PropertyTypeName { get; set; } = default!;
    public string FieldName { get; set; } = default!;
    public DbType FieldType { get; set; }
    public bool IsNullable { get; set; }
    public bool IsKey { get; set; }
    public DatabaseGeneratedOption DatabaseGenerated { get; set; } = DatabaseGeneratedOption.None;

    private bool? _renderAsString;
    public bool RenderAsString =>
        _renderAsString ??=
            FieldType is DbType.String or DbType.StringFixedLength or DbType.AnsiStringFixedLength;
}