namespace Benchmarks

open System
open Form 
open Form.Attributes
open System.Data.SQLite
open Dapper
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Diagnostics.dotTrace
open Configs



[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter;
  DotTraceDiagnoser
>]
type InsertBenchmark() =
    
    let _sqliteState = SQLite( Data.sqliteConnectionString (), Data.Context.SQLite )
    let mutable _data = Array.empty
    
    static member public DataValues = Data.collections
    
    [<ParamsSource(nameof(InsertBenchmark.DataValues))>]
    member public _.Data 
        with get() = _data
        and set (value) = _data <- value
    
    
    [<IterationSetup>]
    member _.Setup() = 
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int>())
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int64>())
        Orm.execute _sqliteState None Utilities.drop |> ignore
        Orm.execute _sqliteState None Utilities.create |> ignore
    
    [<Benchmark>]
    member _.Form () = 
        let transaction = Orm.beginTransaction _sqliteState
        Array.map ( Orm.insert<Data.Sanic> _sqliteState transaction true ) _data
        |> ignore
        Orm.commitTransaction transaction

    [<Benchmark>]
    member _.FormMany () = 
        let transaction = Orm.beginTransaction _sqliteState
        Orm.insertMany<Data.Sanic> _sqliteState transaction true _data
        |> ignore
        Orm.commitTransaction transaction
        
    [<Benchmark>]
    member _.Dapper () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("insert into \"Sanic\" values (@id, @name, @optional, @modified)", _data, transaction) |> ignore
        transaction.Commit()
        connection.Close()
    
    
    [<Benchmark(Baseline = true)>]
    member _.System () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        use cmd = new SQLiteCommand( "insert into \"Sanic\" values (@id, @name, @optional, @modified)", connection )
        cmd.Transaction <- transaction
        let paramId = SQLiteParameter()
        paramId.ParameterName <- "@id"
        let paramName = SQLiteParameter()
        paramName.ParameterName <- "@name"
        let paramOptional = SQLiteParameter()
        paramOptional.ParameterName <- "@optional"
        paramOptional.IsNullable <- true
        let paramModified = SQLiteParameter()
        paramModified.ParameterName <- "@modified"
        cmd.Parameters.Add(paramId) |> ignore
        cmd.Parameters.Add(paramName) |> ignore
        cmd.Parameters.Add(paramOptional) |> ignore
        cmd.Parameters.Add(paramModified) |> ignore
        Array.iter ( fun ( item : Data.Sanic ) -> 
            paramId.Value <- item.id
            paramName.Value <- item.name
            match item.optional with 
            | Some i -> paramOptional.Value <- i 
            | None -> paramOptional.Value <- DBNull.Value
            paramModified.Value <- item.modified
            cmd.ExecuteNonQuery() |> ignore
        ) _data
        transaction.Commit()
        connection.Close()

[<
  MemoryDiagnoser; 
  Config(typeof<BenchmarkConfig>);
  RPlotExporter;
  DotTraceDiagnoser
>]
type UpdateBenchmark() =
    
    let _sqliteState = SQLite( Data.sqliteConnectionString (), Data.Context.SQLite )
    let mutable _data = Array.empty
    
    static member public DataValues = Data.collections
    
    [<ParamsSource(nameof(InsertBenchmark.DataValues))>]
    member public _.Data 
        with get() = _data
        and set (value) = _data <- value
    
    [<GlobalSetup>]
    member _.Setup () = 
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int>())
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int64>())
        Orm.execute _sqliteState None Utilities.drop |> ignore
        Orm.execute _sqliteState None Utilities.create |> ignore
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
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        connection.Execute("update \"Sanic\" set name = @name, optional = @optional, modified = @modified where id = @id",  _data, transaction) |> ignore
        transaction.Commit()
        connection.Close()
    
    [<Benchmark(Baseline = true)>]
    member _.System () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use transaction = connection.BeginTransaction()
        use cmd = new SQLiteCommand( "update \"Sanic\" set name = @name, optional = @optional, modified = @modified where id = @id", connection )
        cmd.Transaction <- transaction
        let paramId = SQLiteParameter()
        paramId.ParameterName <- "@id"
        let paramName = SQLiteParameter()
        paramName.ParameterName <- "@name"
        let paramOptional = SQLiteParameter()
        paramOptional.ParameterName <- "@optional"
        paramOptional.IsNullable <- true
        let paramModified = SQLiteParameter()
        paramModified.ParameterName <- "@modified"
        cmd.Parameters.Add(paramId) |> ignore
        cmd.Parameters.Add(paramName) |> ignore
        cmd.Parameters.Add(paramOptional) |> ignore
        cmd.Parameters.Add(paramModified) |> ignore
        Array.iter ( fun ( item : Data.Sanic ) -> 
            paramId.Value <- item.id
            paramName.Value <- item.name
            match item.optional with 
            | Some i -> paramOptional.Value <- i 
            | None -> paramOptional.Value <- DBNull.Value
            paramModified.Value <- item.modified
            cmd.ExecuteNonQuery() |> ignore
        ) _data
        transaction.Commit()
        connection.Close()


[<
  MemoryDiagnoser;
  Config(typeof<BenchmarkConfig>);
  RPlotExporter;
  DotTraceDiagnoser
>]
type SelectBenchmark() =

    let _sqliteState = SQLite( Data.sqliteConnectionString (), Data.Context.SQLite )
    let mutable _data = 0
    static member public DataValues = [| Data.small; Data.big |]
    
    [<ParamsSource(nameof(InsertBenchmark.DataValues))>]
    member public _.Data 
        with get() = _data
        and set (value) = _data <- value


    [<GlobalSetup>]
    member _.Setup () = 
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int>())
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int64>())
        Orm.execute _sqliteState None Utilities.drop |> ignore
        Orm.execute _sqliteState None Utilities.create |> ignore
        let transaction = Orm.beginTransaction _sqliteState
        Orm.insertMany<Data.Sanic> _sqliteState transaction true ( [| yield! Data.collectionSmall; yield! Data.collectionBig |] ) |> ignore
        Orm.commitTransaction transaction |> ignore
        ()
    

    [<Benchmark>]
    member _.Form () = 
        Orm.selectLimit<Data.Sanic> _sqliteState None _data  
        |> Result.map ( Seq.iter ignore )

    [<Benchmark>]
    member _.Dapper () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        for _ in connection.Query<Data.Sanic>($"select * from Sanic limit {_data};") do () 
    
    [<Benchmark(Baseline = true)>]
    member _.System () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use cmd = new SQLiteCommand( $"select * from \"Sanic\" limit {_data}", connection )
        let reader = cmd.ExecuteReader()
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
        BenchmarkRunner.Run<InsertBenchmark>() |> ignore
        BenchmarkRunner.Run<UpdateBenchmark>() |> ignore
        BenchmarkRunner.Run<SelectBenchmark>() |> ignore
        
        0
        