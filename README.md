# DataAOT

## Overview

This is a proof-of-concept C# [source generator](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/) to provide rudimentary data access code for trimmed [AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) applications that currently cannot leverage EntityFramework.Core. It is not meant to be a substitute for full-featured ORMs, but rather as an add-on to System.Data to make CRUD operations a little easier.

[Data Model](#data-models) classes that you build are used when creating [Gateway](#data-gateway) classes that implement CRUD functionality.  The biggest benefit offered is the automatic mapping of SQL operations and results to and from your data model class.  You can also roll your own SQL statements and bind them in your Gateway class by utilizing the `PopulateFromDataReader` method. 

All that said, here's the sort of thing you can do with it...

```c#
// Data model utilizing "standard" data annotations
[Table( "user_accounts")]
public class Account
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ID { get; set; }

    [Column("account_name")]
    public string AccountName { get; set; } = default!;
}

// This triggers the source generator, don't forget the "partial"!
public partial class AccountGateway: DbGateway<Account>
{
}

// Do some CRUD...
public void DoStuff()
{
    using db = new MySqlConnection("...");
    using gateway = new AccountGateway(CreateConnection);
    // Create a new record
    var record = new Account {
        AccountName = "Foo"
    };
    gateway.Create(record);
    Console.WriteLine($"New Record ID: {record.ID}");
    
    // Update that record
    record.AccountName = "Bar";
    gateway.Update(record);
    
    // Retrieve the record
    var match = gateway.Retrieve($"ID = {{record.ID}}").SingleOrDefault()
        ?? throw new Exception("Not found"); 
}
```

The source generator extends the AccountGateway class to add CRUD functionality, with some additional bells and whistles.  See description below.

Current supported databases include:
* Sqlite
* MySQL
* PostgreSQL
* Microsoft SQL Server

### Requirements

To use this package:

1. Add this package to your project from NuGet
2. Create your data model classes.  You will need to leverage attributes found in System.ComponentModel.DataAnnotations (see below)
3. Create a partial class that inherits `DbGateway<[Your Data Model]>`
4. If you want to have the generated code added to your project (hint: _you do_), add the following to your .csproj file:
    ```xml
    <PropertyGroup>
       <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
       <GeneratedFolder>generated</GeneratedFolder>
       <CompilerGeneratedFilesOutputPath>$(GeneratedFolder)</CompilerGeneratedFilesOutputPath>
    </PropertyGroup>
    ```
5. Make sure your project has Nullable types enabled
6. Add your System.Data-compatible database packages

See the **DataAOT.Test.Application** for an example.

## Usage

### Data Models

DataAOT relies upon data models defined using the System.ComponentModel.DataAnnotations attributes **Table**, **Column**, **Key** and **DatabaseGenerated**. 

1. Classes used to create data objects require the **Table** attribute.  If a **name** is specified for the **Table** attribute, that will be used as the table name, otherwise, the class name will be used.  No attempt is made to automatically determine database table names (ex. translate class name to plural, etc.)  
2. Properties with a **Column** attribute are mapped to database fields.  All other properties are ignored for purposes of database operations.  If a **name** is specified for the **Column** attribute, that will be used for the database field, otherwise, the property name will be used.  No attempt is made to automatically determine database field names (ex. translate to/from kabob or snake case). 
3. Properties with a **Key** attribute are treated as the primary key.  Operations like [Update](#update) and [Delete](#delete) which take data models as parameters rely upon the **Key** properties to locate records.
4. Properties with a **DatabaseGenerated** attributes  are ignored when inserting or updating data, since those values are generated by the database.

> A property with a DatabaseGeneratedOption.Identity attribute will be treated as a Key property if no properties have a Key attribute. 

 
### Data Gateway

The source generator will create a Data Gateway implementation when it encounters a partial class derived from IDbGateway<[ModelName]>:

```c#
public partial class MyGateway: DbGateway<MyDataModel>
{
}
```

You can define something as simple as the empty class shown above.  You can also add your own methods in that leverage the generated DbGateway's protected methods and properties.

Here is a list of generated public methods and protected methods and properties:

#### Create 
```c#
void Create([Model] data)
```

Generates and executes a SQL INSERT statement for the specified **data** object, and then retrieves any database generated values and updates the data object.

> Note:  In order to retrieve generated values to update the data object, the object's data model class must include properties with Key and/or  DatabaseGenerated.Identity attributes.

Example:
```c#
    var record = new Account {
        AccountName = "Foo"
    };
    gateway.Create(record);
    Console.WriteLine($"New Record ID: {record.ID}");
```

#### Retrieve 
```c#
IEnumerable<TModel> Retrieve(string? query,
    IDictionary<string, object>? parameters = null,
    bool translatePropertyNames = true)`
```

Generates and executes a SQL SELECT statement to retrieve one or more records more records using **query** as the WHERE criteria.  The **query** can contain named parameters that can be passed in using the **parameters** argument.  By default, any property names in **query** will be translated to database field names automatically, you can disable this by setting **translatePropertyNames** to false.

Example:
```c#
    var firstMatch = gateway
        .Retrieve("Name=@name", new Dictionary<string, object> { { "@name", "Foo" } })
        .FirstOrDefault(); 
    Console.WriteLine($"First match ID: {(firstMatch?.ID ?? "No match")}); 
```

#### Update
```c#
public void Update(TModel data, params string[]? propertiesToUpdate);
```

Generates and executes a SQL UPDATE statement for the specified **data** object, and then retrieves any database generated values and updates the data object.  The data model _must_ include Key and/or GeneratedDatabase.Identity properties.  By default, all non-generated fields are updated unless **propertiesToUpdate** is set, which will limit which properties (named by property name or field name) will be updated.

Example:
```c#
    record.AccountName = "New Name";
    gateway.Update(record);
```

#### Delete
```c#
public void Delete(TModel data);
```

Generates and executes a SQL DELETE statement for the specified **data** object.  The data model _must_ include Key and/or GeneratedDatabase.Identity properties.

Example:
```c#
    gateway.Delete(record);
```

#### BulkUpdate
```c#
public void BulkUpdate(IDictionary<string, object?> values, 
            string query, IDictionary<string, object>? parameters = null, 
            bool translatePropertyNames = true
```

Generates and executes a SQL UPDATE statement which will update the the field name/value pairs specified in **values**.  The **query** (required) can contain named parameters that can be passed in using the **parameters** argument.  By default, any property names in **query** will be translated to database field names automatically, you can disable this by setting **translatePropertyNames** to false.  

#### BulkDelete
```c#
public void BulkDelete(string query, IDictionary<string, object>? parameters = null, 
            bool translatePropertyNames = true
```

Generates and executes a SQL DELETE statement.  The **query** (required) can contain named parameters that can be passed in using the **parameters** argument.  By default, any property names in **query** will be translated to database field names automatically, you can disable this by setting **translatePropertyNames** to false.

#### TableName (Protected Property)

Name of the database table the gateway will be reading and writing data from.

#### Fields (Protected Property)

A list of database fields with the following properties:

* **PropertyName**:  The name of the property in the data model representing the field
* **PropertyType**:  The C# type of the property in the data model representing the field
* **FieldName**: The name of the database field
* **FieldType**:  The enumerated database field type (DBType)
* **IsNullable**:  If True, the database field is nullable
* **IsKey**: If True, the database field is part of the table's primary key
* **DatabaseGenerated**:  If set to **DatabaseGeneratedOption.Identity**, the value is populated as an auto-incremented value, and can be used to identify records; if set to **DatabaseGeneratedOptions.Computed**, the value is computed by the database.  **DatabaseGeneratedOption.None** means that the databse does not set the value.  Only fields with a value of **DatabaseGeneratedOption.None** will be set during INSERT and UPDATE statements.  

#### PopulateFromDataReader (Protected Method)
```c#
protected void PopulateFromDataReader(IDataReader reader, TModel data)
```

Populates properties in **data** from values retrieved when iterating **reader**.  Field-to-property mapping is done using the **Fields** property shown above.

## Testing

The integration test use Docker-launched database engines.  Run `docker-compose up` from `DataAOT.Test`.

## To Finish
* ILogger support

## TODO

* Pagination
* Joins
* "Dirty" property support
* Telemetry hooks
