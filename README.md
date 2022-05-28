[![NuGet Status](https://img.shields.io/nuget/v/Form.svg?style=flat)](https://www.nuget.org/packages/Form/)

# F# Object Relational Mapper

An attribute based ORM for fsharp.

## Usage
    
To start, enumerate your databases:

```fsharp
type Contexts = 
    | Database1 = 1
    | Database2 = 2
```

We use enumeration here as Unions are not supported by attributes as of yet. The number that each database is set to doesn't really matter,
but at the moment this is the best way we have found to generalize and organize contextualized attributes.

Next, use attributes to perform your domain modeling:

```fsharp
[<Table("User", Contexts.Database1)>]
[<Table("Users", Contexts.Database2)>]
type User = 
    { Id : int
      [<Column("Name", Contexts.Database1)>]
      [<Column("Login", Contexts.Database2)>]
      Name : string
      [<Column("Secret", Contexts.Database1)>]
      Password : string
      Salt : string
    }
```

**Table** and **Column** attributes take a name and a context. The name must match that of the relevant object in the database which is referred to by the given context; that is to say, the **User** type refers to a table called "User" in Database1, and "Users" in Database2. If no attribute is given, the underlying logic will default to the name of the type/field, so if you use the same names in your project and your database(s), no **Table** attribute is necessary; i.e., the "Id" field will be assumed to map to an "Id" field in both the "User" table in **Database1**, and the same in the "Users" table in **Database2**.

Before we connect, we must also declare some OrmStates. We will need one for each context:

```fsharp
let db1State = PSQL("DB1", Contexts.Database1)
let db1State = MSSQL("DB2", Contexts.Database2)
```

We don't know why you would want to have a project spread over databases of different flavors (here we have one in Postgres and one in SQL Server), but we provide this functionality just in case.

Next, we connect to our databases:

```fsharp
let db1 = connection database1ConnectionString
let db2 = connection database2ConnectionString
```

The connection strings should just be given as strings, deliver these however you see fit (we recommend either putting them in a .env file or setting an environmental variable the will deliver the connection string, we may add some helper functions for this later).

Now we can do some querying:

```fsharp
selectAll<User> db1State |> printfn "%A"
```

This should send a SELECT * query to the db1./.User table, if everything was setup correctly. Keep in mind that our querying functions return **Result<[]^T, {| Message |}>**, so be prepared to handle those accordingly.

We also include a custom query framework; an example:

```fsharp
let query =
    { clauses = 
        [ select<User>
        ; from<User>
        ; join<User> [First ("Id", Equals "42"); And ("Name", NotEquals "'Jim'")]
        ] 
    }.Compile db1State
```
The compilation returns a string, and if printed the above should be:

```fsharp
"SELECT "Id", "Name", "Secret", "Salt" FROM "User" JOIN "User" On "Id" = 42 AND "Name" = 'Jim'"
```

Obviously, you may need to specify Schema in table declarations. We are currently ruminating over the idea of a schema attribute, but for now just add the schema name to the table attribute:

```fsharp
[<Table("SchemaName.User", Contexts.Database1)>]
```

and the logic will sort it all out:

```fsharp
"SELECT "Id", "Name", "Secret", "Salt" FROM "SchemaName"."User" JOIN "SchemaName"."User" On "Id" = 42 AND "Name" = 'Jim'"
```

Disambiguation of columns will be necessary for joins, we will be adding that soon.


Once you have the query you need, simply execute it:

```fsharp
let rowsAffected = execute query db1State //This is a non-query execution, does not return a result set
let queryResults = executeReader query generatereader db1State //This DOES return a result set, in this case a Result<[]User, {| Message |}> 
```

Though this package itself awaits full testing, we have taken most of these pieces from other projects which have been thoroughly tested, so this package should work more or less out of the box.


## Contribution Guidelines

Currently no strict guidelines, just open a pull request or an issue. Feel free to shoot us an [email](mailto:contact@hillcrestrnd.com) if you have any questions!


### Note:

- Contributions are welcome!
- This has yet to be tested fully. 
- Under active development.