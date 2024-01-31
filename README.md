[![NuGet Status](https://img.shields.io/nuget/v/Form.svg?style=flat)](https://www.nuget.org/packages/Form/)

# F# Object Relational Mapper

An attribute based ORM for F#.

## Usage
    
To start, enumerate your databases:

```fsharp
type Contexts = 
    | Database1 = 1
    | Database2 = 2
```

We use enumeration here as Unions are not supported by attributes as of yet. The number that each database is set to doesn't really matter,
but at the moment this is the best way we have found to handle multi-database setups here.


Next, use attributes to perform your data modeling:

```fsharp
[<Table("accounts.User", Contexts.Database1)>]
[<Table("Users", Contexts.Database2)>]
type User = 
    { 
        Id : int
        [<Column("Name", Contexts.Database1)>]
        [<Column("Login", Contexts.Database2)>]
        Name : string
        [<Column("Secret", Contexts.Database1)>]
        Password : string
        Salt : string
    }
```

**Table** and **Column** attributes take a name and a context. The name must match that of the relevant object in the database which is referred to by the given context; that is to say, the **User** type refers to a table called "User" in Database1, and "Users" in Database2. If no attribute is given, the underlying logic will default to the name of the type/field, so if you use the same names in your project and your database(s), no **Table**/**Column** attribute is necessary; i.e., the "Id" field will be assumed to map to an "Id" column in both the "User" table in **Database1**, and the same in the "Users" table in **Database2**.

If you want to override the default schema or database, you're able to prepend the table name with the schema name or the schema and database name and everything will translate properly, e.g.:
```fsharp
[<Table("SomeDatabase.aSchema.aReallyGoodTable", Contexts.Database1)>] 
[<Table("bSchema.aReallyGoodTable", Contexts.Database1)>]
```

Before we connect, we must also declare some OrmStates. We will need one for each context:

```fsharp
let db1State = PSQL(db1ConnectionString, Contexts.Database1)
let db2State = MSSQL(db2ConnectionString, Contexts.Database2)
```

The connection strings should just be given as strings, deliver these however you see fit. 

Now we can do some querying:

```fsharp
selectAll<User> db1State |> printfn "%A"
```

This should send a "select *" query to the db1./.User table, if everything was setup correctly. Keep in mind that our querying functions return **Result<'T seq, exn>**, so be prepared to handle those accordingly.

We also allow you to run arbitrary SQL against your database.

# WARNING -- This is not safe. Make sure all sql used this way is either properly escaped or not generated with user input data.

```fsharp 
execute db1State "create table User ( id int not null);" //returns Result<int, exn>
```

Or if you need to read the result, you can supply a function that takes an IDataReader and we'll consume that and pass the results back.

# WARNING -- This is not safe. Make sure all sql used this way is either properly escaped or not generated with user input data.

```fsharp
let query = 
    "select * 
    from 
        user u 
        left join employees e on u.id = e.userId"
(*
    This consumeReader function is used internally by Form but if you don't want to implement your own reader, you can use it.
    Make sure to align the column names in the hand-written sql with what's returned by mapping< ^T >.
*)
let readerConsumer = consumeReader<User> db1State  
let result = executeWithReader db1State None query readerConsumer  //Result<User seq, exn>
```

We also provide functions that allow arbitrary filtering with *Where functions. The reason they are in this format is to prevent sql injection attacks. We do some basic escaping for you but you should always make sure you're using sanitized data:
```fsharp
selectWhere<User> db1State None ("(\"Id\" = :1 and \"Name\" = ':2') OR (\"Id\" != :1 + 1)", [|42, "Jimothy"|])
//Executes "SELECT {column list} FROM accounts.User WHERE ("Id" = 42 and "Name" = 'Jimothy') OR ("Id" != 42 + 1) "

updateWhere<User> db1State None ("(\"Id\" = :1 and \"Name\" = ':2') OR (\"Id\" != :1 + 1)", [|42, "Jimothy"|]) modifiedUser
//Executes an update statement against all columns in accounts.User with "WHERE ("Id" = 42 and "Name" = 'Jimothy') OR ("Id" != 42 + 1)""

deleteWhere<User> db1State None ("(\"Id\" = :1 and \"Name\" = ':2') OR (\"Id\" != :1 + 1)", [|42, "Jimothy"|])
//Executes a delete statement against accounts.User with "WHERE ("Id" = 42 and "Name" = 'Jimothy') OR ("Id" != 42 + 1)""
```

Yes we realize these are the dumbest SQL statements you've ever seen, we hope you got a laugh. 😊 It should get the point across of how the format specification works.


In short, have implemented the basic CRUD operations along with some variations on them. If you'd like to see something, try to implement it yourself and open a pull request or make a request through the issues. 

## Performance

For our purposes, FORM's performance has been excellent. However, we recognize that we may not have been able to push FORM to its absolute limits. We would be interested in any benchmarking you're able to do with it. If you do so, providing us with your results and hardware specs used for the benchmarking will help us improve this library.


## Contribution Guidelines

Currently no strict guidelines, just open a pull request or an issue. Feel free to shoot us an [email](mailto:contributions@hcrd.com) if you have any questions!


Made with 💕 by HCRD.