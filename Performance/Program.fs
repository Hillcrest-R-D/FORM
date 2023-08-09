namespace Benchmarks

open System
open Form 
open Form.Attributes
open Microsoft.Data.Sqlite
open Dapper
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open Configs


[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter
>]
type InsertSmallBenchmark() =

    let _sqliteState = SQLite( Benchmarks.Data.sqliteConnectionString (), Data.Context.SQLite )
    [<IterationSetup>]
    member _.Setup() = 
        Orm.execute _sqliteState None Benchmarks.Utilities.drop  |> printfn "%A"
        Orm.execute _sqliteState None Benchmarks.Utilities.create  |> printfn "%A"
    
    [<Benchmark>]
    member _.InsertForm () = 
        let transaction = Orm.beginTransaction _sqliteState
        // Orm.insertMany<Data.Sanic> _sqliteState transaction true Data.collectionSmall
        List.map ( fun item -> 
            Orm.insert<Data.Sanic> _sqliteState transaction true item   
        ) Data.collectionSmall
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.InsertManyForm () = 
        let transaction = Orm.beginTransaction _sqliteState
        Orm.insertMany<Data.Sanic> _sqliteState transaction true Data.collectionSmall
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.InsertDapper () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("insert into \"Sanic\" values (@id, @name, @optional, @modified)", Data.collectionSmall) |> ignore
        transaction.Commit()
    
    
    [<Benchmark>]
    member _.InsertMicrosoft () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        use cmd = new SqliteCommand( "insert into \"Sanic\" values (@id, @name, @optional, @modified)", connection )
        cmd.Transaction <- transaction
        let paramId = SqliteParameter()
        paramId.ParameterName <- "@id"
        let paramName = SqliteParameter()
        paramName.ParameterName <- "@name"
        let paramOptional = SqliteParameter()
        paramOptional.IsNullable <- true
        let paramModified = SqliteParameter()
        paramModified.ParameterName <- "@modified"
        cmd.Parameters.Add(paramId) |> ignore
        cmd.Parameters.Add(paramName) |> ignore
        cmd.Parameters.Add(paramOptional) |> ignore
        cmd.Parameters.Add(paramModified) |> ignore
        List.iter ( fun ( item : Data.Sanic ) -> 
            paramId.Value <- item.id
            paramName.Value <- item.name
            match item.optional with 
            | Some i -> paramOptional.Value <- i 
            | None -> 
                paramOptional.ParameterName <- "@optional"
                paramOptional.Value <- DBNull.Value
            paramModified.Value <- item.modified
            cmd.ExecuteNonQuery() |> ignore
        ) Data.collectionSmall
        transaction.Commit()

[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter
>]
type InsertBigBenchmark() =

    let _sqliteState = SQLite( Benchmarks.Data.sqliteConnectionString (), Data.Context.SQLite )
    [<IterationSetup>]
    member _.Setup() = 
        Orm.execute _sqliteState None Benchmarks.Utilities.drop  |> printfn "%A"
        Orm.execute _sqliteState None Benchmarks.Utilities.create  |> printfn "%A"

    [<Benchmark>]
    member _.InsertForm () = 
        let transaction = Orm.beginTransaction _sqliteState
        List.map ( fun item -> 
            Orm.insert<Data.Sanic> _sqliteState transaction true item   
        ) Data.collectionBig
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.InsertManyForm () = 
        let transaction = Orm.beginTransaction _sqliteState
        Orm.insertMany<Data.Sanic> _sqliteState transaction true Data.collectionBig |> ignore
        Orm.commitTransaction transaction
    

    [<Benchmark>]
    member _.InsertDapper () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("insert into \"Sanic\" values (@id, @name, @optional, @modified)", Data.collectionBig) |> ignore
        transaction.Commit()
    
    
    [<Benchmark>]
    member _.InsertMicrosoft () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        use cmd = new SqliteCommand( "insert into \"Sanic\" values (@id, @name, @optional, @modified)", connection )
        cmd.Transaction <- transaction
        let paramId = SqliteParameter()
        paramId.ParameterName <- "@id"
        let paramName = SqliteParameter()
        paramName.ParameterName <- "@name"
        let paramOptional = SqliteParameter()
        paramOptional.ParameterName <- "@optional"
        let paramModified = SqliteParameter()
        paramModified.ParameterName <- "@modified"
        cmd.Parameters.Add(paramId) |> ignore
        cmd.Parameters.Add(paramName) |> ignore
        cmd.Parameters.Add(paramOptional) |> ignore
        cmd.Parameters.Add(paramModified) |> ignore
        List.iter ( fun ( item : Data.Sanic ) -> 
            paramId.Value <- item.id
            paramName.Value <- item.name
            match item.optional with 
            | Some i -> paramOptional.Value <- i 
            | None -> 
                paramOptional.IsNullable <- true
                paramOptional.Value <- DBNull.Value
            paramModified.Value <- item.modified
            cmd.ExecuteNonQuery() |> ignore
        ) Data.collectionBig
        transaction.Commit()

[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter
>]
type FormBenchmark() = 
    let _sqliteState = SQLite( Benchmarks.Data.sqliteConnectionString (), Data.Context.SQLite )
    [<GlobalSetup>]
    member _.Setup() = 
        Orm.execute _sqliteState None Benchmarks.Utilities.drop  |> printfn "%A"
        Orm.execute _sqliteState None Benchmarks.Utilities.create  |> printfn "%A"

    [<Benchmark>]
    member _.InsertSmall () = 
        let transaction = Orm.beginTransaction _sqliteState
        List.map ( fun item -> 
            Orm.insert<Data.Sanic> _sqliteState transaction true item   
        ) Data.collectionSmall
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.UpdateSmall () = 
        let transaction = Orm.beginTransaction _sqliteState
        Data.modifiedCollectionSmall ()
        |> List.map ( fun item -> 
            Orm.updateWhere<Data.Sanic> _sqliteState transaction $"id = {item.id}" item 
        ) 
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.SelectSmall () = 
        Orm.selectLimit<Data.Sanic> _sqliteState None Data.small  
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
        Orm.execute _sqliteState None Benchmarks.Utilities.drop |> printfn "%A"
        Orm.execute _sqliteState None Benchmarks.Utilities.create |> printfn "%A"

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
        connection.Execute("update \"Sanic\" set name = @name, modified = @modified where id = @id", Data.collectionSmall, transaction) |> ignore
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
        Orm.execute _sqliteState None Benchmarks.Utilities.drop |> printfn "%A"
        Orm.execute _sqliteState None Benchmarks.Utilities.create |> printfn "%A"

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
            let datum : Data.Sanic = 
                { 
                    id = reader.GetValue(0) |> unbox<int> 
                    name = reader.GetValue(1) |> unbox<string> 
                    optional = reader.GetValue(2) |> unbox<int option>  
                    modified = reader.GetValue(3) |> unbox<System.DateTime>; 
                }
            data <- data @ [ datum ]
                
        

module Main = 
    [<EntryPoint>]
    let main _ =
        DotNetEnv.Env.Load "../" |> printfn "%A"
        
        // BenchmarkRunner.Run<FormBenchmark>() |> ignore
        // BenchmarkRunner.Run<DapperBenchmark>() |> ignore
        // BenchmarkRunner.Run<MicrosoftBenchmark>() |> ignore
        BenchmarkRunner.Run<InsertSmallBenchmark>() |> ignore
        BenchmarkRunner.Run<InsertBigBenchmark>() |> ignore
        
        0
        