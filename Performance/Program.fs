open System
open Form 
open Form.Attributes
open Microsoft.Data.Sqlite
open Dapper
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

DotNetEnv.Env.Load "../" |> printfn "%A"


type Context = 
    | SQLite = 1

[<CLIMutable>]
type Sanic = {
    [<PrimaryKey("id", Context.SQLite)>]
    id: int 
    name: string
    modified: DateTime
}

module Utilities = 
    let mapOver = List.map ( fun x -> { x with modified = System.DateTime.Now } )
    let timeIt f = 
        let stopwatch = Diagnostics.Stopwatch.StartNew()
        f() |> ignore
        stopwatch.Stop()
        stopwatch.Elapsed
    let create = 
        "create table if not exists \"Sanic\" (
            id int primary key,
            name varchar(32) not null,
            modified timestamp not null
        )"

    let drop = "drop table if exists \"Sanic\";"

    let truncate = "delete from \"Sanic\";"

module Data = 
    let small = 1000
    let big = 10000
    let collectionSmall = [ for i in 1..small -> { id = i; name = "John Doe"; modified = DateTime.Now } ]
    let collectionBig = [ for i in 1001..(small+big) -> { id = i; name = "Jane Doe"; modified = DateTime.Now } ]
    let modifiedCollectionSmall () = Utilities.mapOver collectionSmall
    let modifiedCollectionBig () = Utilities.mapOver collectionBig
    let sqliteConnectionString () = System.Environment.GetEnvironmentVariable("sqlite_connection_string")
    let postgresConnectionString () = System.Environment.GetEnvironmentVariable("postgres_connection_string")

let sqliteState = SQLite( Data.sqliteConnectionString (), Context.SQLite )

module Old =
    let formInsert collection = 
        let transaction = Orm.beginTransaction sqliteState
        List.map ( fun item -> 
            Orm.insert<Sanic> sqliteState true item transaction  
        ) collection 
        |> ignore
        Orm.commitTransaction transaction

    let formUpdate collection = 
        let transaction = Orm.beginTransaction sqliteState
        List.map ( fun item -> 
            Orm.update<Sanic> sqliteState item transaction
        ) collection 
        |> ignore
        Orm.commitTransaction transaction

    let formSelect limit = 
        let transaction = Orm.beginTransaction sqliteState
        Orm.selectLimit<Sanic> sqliteState limit transaction |> ignore
        Orm.commitTransaction transaction

    let dapperInsert collection = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("insert into \"Sanic\" values (@id, @name, @modified)", collection)
        transaction.Commit()

    //Dapper has no built-in support for bulk-inserting
    let dapperUpdate collection = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("update \"Sanic\" set name = @name, modified = @modified where id = @id", collection, transaction)
        transaction.Commit()

    let dapperSelect limit = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Query<Sanic>($"select * from Sanic limit {limit};")

    let microsoftInsert collection = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        use cmd = new SqliteCommand( "insert into \"Sanic\" values (@id, @name, @modified)", connection )
        cmd.Transaction <- transaction
        let paramId = SqliteParameter()
        paramId.ParameterName <- "@id"
        let paramName = SqliteParameter()
        paramName.ParameterName <- "@name"
        let paramModified = SqliteParameter()
        paramModified.ParameterName <- "@modified"
        cmd.Parameters.Add(paramId)
        cmd.Parameters.Add(paramName)
        cmd.Parameters.Add(paramModified)
        List.iter ( fun item -> 
            paramId.Value <- item.id
            paramName.Value <- item.name
            paramModified.Value <- item.modified
            cmd.ExecuteNonQuery() |> ignore
        ) collection
        transaction.Commit()


    // let microsoftUpdate collection = 
    //     use connection = new SqliteConnection( Data.sqliteConnectionString() )
    //     use
    //     connection.Execute("update \"Sanic\" set name = @name, modified = @modified where id = @id", collection)

    // let microsoftSelect limit = 
    //     use connection = new SqliteConnection( Data.sqliteConnectionString() )
    //     use
    //     connection.Query<Sanic>($"select * from Sanic limit {limit};")

    // setup sqlite
    // Orm.execute sqliteState Utilities.drop None |> printfn "%A"
    // Orm.execute sqliteState Utilities.create None |> printfn "%A"

    // Utilities.timeIt ( fun _ -> formInsert Data.collectionSmall ) |> printfn "Form insert %i: %A" Data.small
    // Utilities.timeIt ( fun _ -> formInsert Data.collectionBig ) |> printfn "Form insert %i: %A" Data.big
    // Utilities.timeIt ( fun _ -> formSelect 1000 ) |> printfn "Form select %i: %A" Data.small
    // Utilities.timeIt ( fun _ -> formSelect 10000 ) |> printfn "Form select %i: %A" Data.big
    // Utilities.timeIt ( fun _ -> formUpdate ( Data.modifiedCollectionSmall () ) ) |> printfn "Form update %i: %A" Data.small
    // Utilities.timeIt ( fun _ -> formUpdate ( Data.modifiedCollectionBig () ) ) |> printfn "Form update %i: %A" Data.big

    // Orm.execute sqliteState Utilities.truncate None |> printfn "%A"

    // Utilities.timeIt ( fun _ -> dapperInsert Data.collectionSmall ) |> printfn "Dapper insert %i: %A" Data.small
    // Utilities.timeIt ( fun _ -> dapperInsert Data.collectionBig ) |> printfn "Dapper insert %i: %A" Data.big
    // Utilities.timeIt ( fun _ -> dapperSelect 1000 ) |> printfn "Dapper select %i: %A" Data.small
    // Utilities.timeIt ( fun _ -> dapperSelect 10000 ) |> printfn "Dapper select %i: %A" Data.big
    // Utilities.timeIt ( fun _ -> dapperUpdate ( Data.modifiedCollectionSmall () ) ) |> printfn "Dapper update %i: %A" Data.small
    // Utilities.timeIt ( fun _ -> dapperUpdate ( Data.modifiedCollectionBig () ) ) |> printfn "Dapper update %i: %A" Data.big

    // Orm.execute sqliteState Utilities.truncate None |> printfn "%A"

    // Utilities.timeIt ( fun _ -> microsoftInsert Data.collectionSmall ) |> printfn "Microsoft insert %i: %A" Data.small
    // Utilities.timeIt ( fun _ -> microsoftInsert Data.collectionBig ) |> printfn "Microsoft insert %i: %A" Data.big


type FormBenchmark() = 
    
    [<GlobalSetup>]
    member _.Setup() = 
        Orm.execute sqliteState Utilities.drop None |> printfn "%A"
        Orm.execute sqliteState Utilities.create None |> printfn "%A"

    [<Benchmark>]
    member _.InsertSmall () = 
        let transaction = Orm.beginTransaction sqliteState
        List.map ( fun item -> 
            Orm.insert<Sanic> sqliteState true item transaction  
        ) Data.collectionSmall
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.UpdateSmall () = 
        let transaction = Orm.beginTransaction sqliteState
        Data.modifiedCollectionSmall ()
        |> List.map ( fun item -> 
            Orm.update<Sanic> sqliteState item transaction
        ) 
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.SelectSmall () = 
        Orm.selectLimit<Sanic> sqliteState Data.small None |> ignore


BenchmarkRunner.Run<FormBenchmark>() |> ignore