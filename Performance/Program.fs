namespace Benchmarks

open System
open Form 
open Form.Attributes
open Microsoft.Data.Sqlite
open Dapper
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open Configs
// module Old =
//     let formInsert collection = 
//         let transaction = Orm.beginTransaction Data.sqliteState
//         List.map ( fun item -> 
//             Orm.insert<Data.Sanic> Data.sqliteState true item transaction  
//         ) collection 
//         |> ignore
//         Orm.commitTransaction transaction

//     let formUpdate collection = 
//         let transaction = Orm.beginTransaction Data.sqliteState
//         List.map ( fun item -> 
//             Orm.update<Data.Sanic> Data.sqliteState item transaction
//         ) collection 
//         |> ignore
//         Orm.commitTransaction transaction

//     let formSelect limit = 
//         let transaction = Orm.beginTransaction Data.sqliteState
//         Orm.selectLimit<Data.Sanic> Data.sqliteState limit transaction |> ignore
//         Orm.commitTransaction transaction

//     let dapperInsert collection = 
//         use connection = new SqliteConnection( Data.sqliteConnectionString() )
//         connection.Open()
//         use transaction = connection.BeginTransaction()
//         connection.Execute("insert into \"Sanic\" values (@id, @name, @modified)", collection)
//         transaction.Commit()

//     //Dapper has no built-in support for bulk-inserting
//     let dapperUpdate collection = 
//         use connection = new SqliteConnection( Data.sqliteConnectionString() )
//         connection.Open()
//         use transaction = connection.BeginTransaction()
//         connection.Execute("update \"Sanic\" set name = @name, modified = @modified where id = @id", collection, transaction)
//         transaction.Commit()

//     let dapperSelect limit = 
//         use connection = new SqliteConnection( Data.sqliteConnectionString() )
//         connection.Query<Data.Sanic>($"select * from Sanic limit {limit};")

//     let microsoftInsert collection = 
//         use connection = new SqliteConnection( Data.sqliteConnectionString() )
//         connection.Open()
//         use transaction = connection.BeginTransaction()
//         use cmd = new SqliteCommand( "insert into \"Sanic\" values (@id, @name, @modified)", connection )
//         cmd.Transaction <- transaction
//         let paramId = SqliteParameter()
//         paramId.ParameterName <- "@id"
//         let paramName = SqliteParameter()
//         paramName.ParameterName <- "@name"
//         let paramModified = SqliteParameter()
//         paramModified.ParameterName <- "@modified"
//         cmd.Parameters.Add(paramId) |> ignore
//         cmd.Parameters.Add(paramName) |> ignore
//         cmd.Parameters.Add(paramModified) |> ignore
//         List.iter ( fun ( item : Data.Sanic ) -> 
//             paramId.Value <- item.id
//             paramName.Value <- item.name
//             paramModified.Value <- item.modified
//             cmd.ExecuteNonQuery() |> ignore
//         ) collection
//         transaction.Commit()


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


[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter
>]
type FormBenchmark() = 
    let _sqliteState = SQLite( Benchmarks.Data.sqliteConnectionString (), Data.Context.SQLite )
    [<GlobalSetup>]
    member _.Setup() = 
        Orm.execute _sqliteState Benchmarks.Utilities.drop None |> printfn "%A"
        Orm.execute _sqliteState Benchmarks.Utilities.create None |> printfn "%A"

    [<Benchmark>]
    member _.InsertSmall () = 
        let transaction = Orm.beginTransaction _sqliteState
        List.map ( fun item -> 
            Orm.insert<Data.Sanic> _sqliteState true item transaction  
        ) Data.collectionSmall
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.UpdateSmall () = 
        let transaction = Orm.beginTransaction _sqliteState
        Data.modifiedCollectionSmall ()
        |> List.map ( fun item -> 
            Orm.updateWhere<Data.Sanic> _sqliteState $"id = {item.id}" item transaction
        ) 
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.SelectSmall () = 
        Orm.selectLimit<Data.Sanic> _sqliteState Data.small None 
        |> Result.map ( Seq.iter ignore )
        
[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter
>]
type DapperBenchmark() = 
    let _sqliteState = SQLite( Benchmarks.Data.sqliteConnectionString (), Data.Context.SQLite )
    [<GlobalSetup>]
    member _.Setup() = 
        Orm.execute _sqliteState Benchmarks.Utilities.drop None |> printfn "%A"
        Orm.execute _sqliteState Benchmarks.Utilities.create None |> printfn "%A"

    [<Benchmark>]
    member _.InsertSmall () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("insert into \"Sanic\" values (@id, @name, @modified)", Data.collectionSmall) |> ignore
        transaction.Commit()

    [<Benchmark>]
    member _.UpdateSmall () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("update \"Sanic\" set name = @name, modified = @modified where id = @id", Data.collectionSmall, transaction)
        transaction.Commit()

    [<Benchmark>]
    member _.SelectSmall () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        for item in connection.Query<Data.Sanic>($"select * from Sanic limit {Data.small};") do () |> ignore
        
[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter
>]
type MicrosoftBenchmark() = 
    let _sqliteState = SQLite( Benchmarks.Data.sqliteConnectionString (), Data.Context.SQLite )
    [<GlobalSetup>]
    member _.Setup() = 
        Orm.execute _sqliteState Benchmarks.Utilities.drop None |> printfn "%A"
        Orm.execute _sqliteState Benchmarks.Utilities.create None |> printfn "%A"

    [<Benchmark>]
    member _.InsertSmall () = 
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
        cmd.Parameters.Add(paramId) |> ignore
        cmd.Parameters.Add(paramName) |> ignore
        cmd.Parameters.Add(paramModified) |> ignore
        List.iter ( fun ( item : Data.Sanic ) -> 
            paramId.Value <- item.id
            paramName.Value <- item.name
            paramModified.Value <- item.modified
            cmd.ExecuteNonQuery() |> ignore
        ) Data.collectionSmall
        transaction.Commit()

    [<Benchmark>]
    member _.UpdateSmall () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        use cmd = new SqliteCommand( "update \"Sanic\" set name = @name, modified = @modified where id = @id", connection )
        cmd.Transaction <- transaction
        let paramId = SqliteParameter()
        paramId.ParameterName <- "@id"
        let paramName = SqliteParameter()
        paramName.ParameterName <- "@name"
        let paramModified = SqliteParameter()
        paramModified.ParameterName <- "@modified"
        cmd.Parameters.Add(paramId) |> ignore
        cmd.Parameters.Add(paramName) |> ignore
        cmd.Parameters.Add(paramModified) |> ignore
        List.iter ( fun ( item : Data.Sanic ) -> 
            paramId.Value <- item.id
            paramName.Value <- item.name
            paramModified.Value <- item.modified
            cmd.ExecuteNonQuery() |> ignore
        ) Data.collectionSmall
        transaction.Commit()

    [<Benchmark>]
    member _.SelectSmall () = 
        
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use cmd = new SqliteCommand( $"select * from \"Sanic\" limit {Data.small}", connection )
        let reader = cmd.ExecuteReader()
        let mutable data = List.empty 
        while (reader.Read()) do
            let datum : Data.Sanic = { id = reader.GetValue(0) |> unbox<int>; name = reader.GetValue(1) |> unbox<string>; modified = reader.GetValue(2) |> unbox<System.DateTime>; }
            data <- data @ [ datum ]
                
        

module Main = 
    [<EntryPoint>]
    let main _ =
        DotNetEnv.Env.Load "../" |> printfn "%A"
        BenchmarkRunner.Run<FormBenchmark>() |> ignore
        BenchmarkRunner.Run<DapperBenchmark>() |> ignore
        BenchmarkRunner.Run<MicrosoftBenchmark>() |> ignore
        0
        