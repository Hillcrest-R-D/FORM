namespace Benchmarks

open System
open Form 
open Form.Attributes
open Microsoft.Data.Sqlite
open Dapper
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Diagnostics.dotTrace
open Configs



[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter
>]
type InsertBenchmark() =
    
    let _sqliteState = SQLite( Benchmarks.Data.sqliteConnectionString (), Data.Context.SQLite )
    let mutable _data = List.empty
    
    static member public DataValues = [ Data.collectionSmall; Data.collectionBig ]
    
    [<ParamsSource(nameof(InsertBenchmark.DataValues))>]
    member public _.Data 
        with get() = _data
        and set (value) = _data <- value
    
    
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
        ) _data
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.InsertManyForm () = 
        let transaction = Orm.beginTransaction _sqliteState
        Orm.insertMany<Data.Sanic> _sqliteState transaction true _data
        |> ignore
        Orm.commitTransaction transaction
        
    [<Benchmark>]
    member _.InsertDapper () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("insert into \"Sanic\" values (@id, @name, @optional, @modified)", _data) |> ignore
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
            | None -> paramOptional.Value <- DBNull.Value
            paramModified.Value <- item.modified
            cmd.ExecuteNonQuery() |> ignore
        ) _data
        transaction.Commit()

[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter
>]
type UpdateBenchmark() =
    
    let _sqliteState = SQLite( Benchmarks.Data.sqliteConnectionString (), Data.Context.SQLite )
    let mutable _data = List.empty
    
    static member public DataValues = [ Utilities.mapOver Data.collectionSmall; Utilities.mapOver Data.collectionBig  ]
    
    [<ParamsSource(nameof(InsertBenchmark.DataValues))>]
    member public _.Data 
        with get() = _data
        and set (value) = _data <- value
    
    [<GlobalSetup>]
    member _.Setup () = 
        Orm.execute _sqliteState None Benchmarks.Utilities.drop  |> printfn "%A"
        Orm.execute _sqliteState None Benchmarks.Utilities.create  |> printfn "%A"
        let transaction = Orm.beginTransaction _sqliteState
        Orm.insertMany<Data.Sanic> _sqliteState transaction true _data |> ignore
        Orm.commitTransaction transaction |> ignore
        ()
    
    [<Benchmark>]
    member _.Form () = 
        let transaction = Orm.beginTransaction _sqliteState
        _data 
        |> Orm.updateMany<Data.Sanic> _sqliteState transaction 
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.Dapper () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("update \"Sanic\" set name = @name, optional = @optional, modified = @modified where id = @id",  _data, transaction) |> ignore
        transaction.Commit()
    
    [<Benchmark>]
    member _.Microsoft () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        use cmd = new SqliteCommand( "update \"Sanic\" set name = @name, optional = @optional, modified = @modified where id = @id", connection )
        cmd.Transaction <- transaction
        let paramId = SqliteParameter()
        paramId.ParameterName <- "@id"
        let paramName = SqliteParameter()
        paramName.ParameterName <- "@name"
        let paramOptional = SqliteParameter()
        paramOptional.ParameterName <- "@optional"
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
            | None -> paramOptional.Value <- DBNull.Value
            paramModified.Value <- item.modified
            cmd.ExecuteNonQuery() |> ignore
        ) _data
        transaction.Commit()


[<
  MemoryDiagnoser;
  Config(typeof<BenchmarkConfig>);
  RPlotExporter;
  DotTraceDiagnoser
>]
type SelectBenchmark() =

    let _sqliteState = SQLite( Benchmarks.Data.sqliteConnectionString (), Data.Context.SQLite )
    let mutable _data = 0
    static member public DataValues = [ Data.small; Data.big  ]
    
    [<ParamsSource(nameof(InsertBenchmark.DataValues))>]
    member public _.Data 
        with get() = _data
        and set (value) = _data <- value


    [<GlobalSetup>]
    member _.Setup () = 
        Orm.execute _sqliteState None Benchmarks.Utilities.drop  |> printfn "%A"
        Orm.execute _sqliteState None Benchmarks.Utilities.create  |> printfn "%A"
        let transaction = Orm.beginTransaction _sqliteState
        Orm.insertMany<Data.Sanic> _sqliteState transaction true ( Data.collectionSmall @ Data.collectionBig ) |> ignore
        Orm.commitTransaction transaction |> ignore
        ()
    

    [<Benchmark>]
    member this.Form () = 
        Orm.selectLimit<Data.Sanic> _sqliteState None _data  
        |> Result.map ( Seq.iter ignore )

    [<Benchmark>]
    member this.Dapper () = 
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        for item in connection.Query<Data.Sanic>($"select * from Sanic limit {_data};") do () |> ignore
    
    [<Benchmark>]
    member this.Microsoft () = 
        
        use connection = new SqliteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use cmd = new SqliteCommand( $"select * from \"Sanic\" limit {_data}", connection )
        let reader = cmd.ExecuteReader()
        let mutable data = List.empty
        // Orm.consumeReader<Data.Sanic> _sqliteState reader 
        // |> Seq.iter ignore
        seq {
            while (reader.Read()) do
                { 
                    id = reader.GetValue(0) :?> int64 //104abc3e
                    name = reader.GetValue(1) :?> string
                    optional = 
                        reader.GetValue(2) |> function 
                        | :? int64 as i -> Some i
                        | _ -> None
                    modified = reader.GetValue(3) :?> string
                } : Data.Sanic
        } |> Seq.iter ignore

module Main = 
    [<EntryPoint>]
    let main _ =
        DotNetEnv.Env.Load "../" |> printfn "%A"
        
        // BenchmarkRunner.Run<InsertBenchmark>() |> ignore
        // BenchmarkRunner.Run<UpdateBenchmark>() |> ignore
        BenchmarkRunner.Run<SelectBenchmark>() |> ignore
        
        0
        