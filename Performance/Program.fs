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
    
    static member public DataValues = Data.collectionsSanic
    
    [<ParamsSource(nameof(InsertBenchmark.DataValues))>]
    member public _.Data 
        with get() = _data
        and set (value) = _data <- value
    
    
    [<IterationSetup>]
    member _.Setup() = 
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int>())
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int64>())
        Form.Orm.execute _sqliteState None Utilities.drop |> ignore
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
    member _.Baseline () = 
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
    
    static member public DataValues = Data.collectionsSanic
    
    [<ParamsSource(nameof(InsertBenchmark.DataValues))>]
    member public _.Data 
        with get() = _data
        and set (value) = _data <- value
    
    [<GlobalSetup>]
    member _.Setup () = 
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int>())
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
    member _.Baseline () = 
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
    
    let _sanicSelect = Utilities.queryBase<Data.Sanic> _sqliteState 

    static member public DataValues = [| Data.small; Data.big |]
    
    [<ParamsSource(nameof(InsertBenchmark.DataValues))>]
    member public _.Data 
        with get() = _data
        and set (value) = _data <- value


    [<GlobalSetup>]
    member _.Setup () =
        printfn "%A" _sanicSelect 
        SqlMapper.AddTypeHandler (Utilities.OptionHandler<int>())
        SqlMapper.AddTypeHandler (Utilities.RelationHandler<Data.Sanic, Data.Knockles>())
        Orm.execute _sqliteState None Utilities.drop |> ignore
        Orm.execute _sqliteState None Utilities.create |> ignore
        let transaction = Orm.beginTransaction _sqliteState
        Orm.insertMany<Data.Knockles> _sqliteState transaction true ( [| yield! Data.collectionSmallKnockles; yield! Data.collectionBigKnockles |] ) |> ignore
        Orm.insertMany<Data.Sanic> _sqliteState transaction true ( [| yield! Data.collectionSmallSanic; yield! Data.collectionBigSanic |] ) |> ignore
        Orm.commitTransaction transaction |> ignore
        Orm.selectLimit<Data.Sanic> _sqliteState None 10 |> Result.toResultSeq |> ignore
        Orm.selectLimit<Data.Knockles> _sqliteState None 10 |> Result.toResultSeq |> ignore
        printfn "%A\n%A" Form.Utilities._relations Form.Utilities._options
        ()
    
    [<GlobalCleanup>]
    member _.End () =
        printfn "%A\n%A" Form.Utilities._relations Form.Utilities._options
        ()
    [<Benchmark>]
    member _.Form () = 
        let data = Orm.selectLimit<Data.Sanic> _sqliteState None _data
        data
        |> Result.toResultSeq
        |> ignore
        // |> Result.mapError( printfn "Error: %A" )

    [<Benchmark>]
    member _.ConsumeReader() =
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use cmd = new SQLiteCommand( $"select {_sanicSelect} limit {_data}", connection )
        let reader = cmd.ExecuteReader()
        let data = Form.Utilities.consumeReader<Data.Sanic> _sqliteState reader
        data
        |> Result.toResultSeq
        |> ignore

    [<Benchmark>]
    member _.Dapper () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        for _ in connection.Query<Data.Sanic>($"select *, null from Sanic limit {_data};") do () 
    
    [<Benchmark>]
    member _.ConsumeReaderRaw () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use cmd = new SQLiteCommand( $"select *, null as \"Knock\" from \"Sanic\" limit {_data}", connection )
        let context = Form.Attributes.Reflection.unpackContext _sqliteState
        let reifiedType = typeof< Data.Sanic >
        let columns = Form.Attributes.Reflection.columnMapping<Data.Sanic> _sqliteState 
        let con = Form.Utilities._constructors[reifiedType]
        let reader = cmd.ExecuteReader()
        let relations = Form.Utilities._relations[(reifiedType, context)]
        let options = Form.Utilities._options[reifiedType]
        let ordinals = [| for i in 0..reader.FieldCount-1 do 
                            reader.GetOrdinal(columns[i].SqlName)
                        |] 
        let data =
            seq {
                while reader.Read( ) do
                    
                    con
                        [| for i in 0..reader.FieldCount-1 do 
                            reader.GetValue( ordinals[i] )
                            |> options[i] 
                            |> relations[i]
                        |] 
                    :?> Data.Sanic
                    |> Ok
            }
        data
        |> Result.toResultSeq
        |> ignore

    
    [<Benchmark(Baseline = true)>]
    member _.Baseline () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use cmd = new SQLiteCommand( $"select *, null from \"Sanic\" limit {_data}", connection )
        let reader = cmd.ExecuteReader()
        let data =
            seq {
                while (reader.Read()) do
                    ({ 
                        id = reader.GetInt32(0) //104abc3e
                        name = reader.GetString(1)
                        optional = 
                            if reader.IsDBNull(2) 
                            then None 
                            else Some <| reader.GetInt32(2)
                        modified = reader.GetString(3)
                        knockId = reader.GetInt32(4)
                        knock = Form.Utilities.Relation<Data.Sanic,Data.Knockles>(1, _sqliteState)
                    } : Data.Sanic)
                    |>Ok
            }
        for _ in data do ()
    
    [<Benchmark>]
    member _.BaselineEnumerateResultSeq () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use cmd = new SQLiteCommand( $"select * from \"Sanic\" limit {_data}", connection )
        let reader = cmd.ExecuteReader()
        let data =
            seq {
                while (reader.Read()) do
                    ({ 
                        id = reader.GetInt32(0) //104abc3e
                        name = reader.GetString(1)
                        optional = 
                            if reader.IsDBNull(2) 
                            then None 
                            else Some <| reader.GetInt32(2)
                        modified = reader.GetString(3)
                        knockId = reader.GetInt32(4)
                        knock = Form.Utilities.Relation<Data.Sanic,Data.Knockles>(1, _sqliteState)
                    } : Data.Sanic)
                    |>Ok
            }
        for _ in data do ()
    
    [<Benchmark>]
    member _.BaselineEnumerateResultSeqWithLift () = 
        use connection = new SQLiteConnection( Data.sqliteConnectionString() )
        connection.Open()
        use cmd = new SQLiteCommand( $"select * from \"Sanic\" limit {_data}", connection )
        let reader = cmd.ExecuteReader()
        let data =
            seq {
                while (reader.Read()) do
                    ({ 
                        id = reader.GetInt32(0) //104abc3e
                        name = reader.GetString(1)
                        optional = 
                            if reader.IsDBNull(2) 
                            then None 
                            else Some <| reader.GetInt32(2)
                        modified = reader.GetString(3)
                        knockId = reader.GetInt32(4)
                        knock = Form.Utilities.Relation<Data.Sanic,Data.Knockles>(1, _sqliteState)
                    } : Data.Sanic)
                    |> Ok
            }
        data
        |> Result.toResultSeq
        |> ignore

module Main = 
    [<EntryPoint>]
    let main _ =
        DotNetEnv.Env.Load "../" |> printfn "%A"
        // BenchmarkRunner.Run<InsertBenchmark>() |> ignore
        // BenchmarkRunner.Run<UpdateBenchmark>() |> ignore
        BenchmarkRunner.Run<SelectBenchmark>() |> ignore
        
        0
        