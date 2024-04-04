namespace rec Benchmarks

module Utilities =
    open System
    open Dapper
    let mapOver = Array.map ( fun ( x : Data.Sanic )-> { x with modified = System.DateTime.Now.ToString("yyyy-MM-dd") } )
    let timeIt f = 
        let stopwatch = Diagnostics.Stopwatch.StartNew()
        f() |> ignore
        stopwatch.Stop()
        stopwatch.Elapsed

    let create = 
        "create table if not exists \"Sanic\" (
            id int,
            name varchar(32) not null,
            optional int null,
            modified varchar(16) not null
        )"

    let drop = "drop table if exists \"Sanic\";"

    let truncate = "delete from \"Sanic\";"

    
    type OptionHandler<'T> () =
        inherit SqlMapper.TypeHandler<option<'T>> ()

        override __.SetValue (param, value) =
            let valueOrNull =
                match value with
                | Some x -> box x
                | None   -> null
            param.Value <- valueOrNull

        override __.Parse value =
            if Object.ReferenceEquals(value, null) || value = box DBNull.Value
            then None
            else Some (value :?> 'T)

    

module Data =
    open System
    open Form.Attributes

    type Context = 
    | SQLite = 1

    [<CLIMutable>]
    type Sanic = {
        [<PrimaryKey("id", Context.SQLite)>]
        id: int 
        name: string
        optional : int option 
        modified: string
    }
    let small = 1000
    let big = 10000
    let collectionSmall = 
        [| for i in 1..small -> 
            { 
                id = i
                name = "John Doe" 
                optional = if i % 2 = 0 then None else Some i 
                modified = DateTime.Now.ToString("yyyy-MM-dd")
            } 
        |]
    let collectionBig = 
        [| for i in 1001..(big) -> 
            { 
                id = i
                name = "Jane Doe"
                optional = if i % 2 = 0 then None else Some i
                modified = DateTime.Now.ToString("yyyy-MM-dd") 
            } 
        |]
    let collections = [|collectionSmall; collectionBig|]

    let modifiedCollectionSmall () = Utilities.mapOver collectionSmall
    let modifiedCollectionBig () = Utilities.mapOver collectionBig
    let sqliteConnectionString () = System.Environment.GetEnvironmentVariable("sqlite_connection_string")
    let postgresConnectionString () = System.Environment.GetEnvironmentVariable("postgres_connection_string")
    let sqliteState = SQLite( sqliteConnectionString (), Context.SQLite )