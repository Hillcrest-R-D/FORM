namespace rec Benchmarks

module Utilities =
    open System
    let mapOver = List.map ( fun ( x : Data.Sanic )-> { x with modified = System.DateTime.Now } )
    let timeIt f = 
        let stopwatch = Diagnostics.Stopwatch.StartNew()
        f() |> ignore
        stopwatch.Stop()
        stopwatch.Elapsed

    let create = 
        "create table if not exists \"Sanic\" (
            id int,
            name varchar(32) not null,
            modified timestamp not null
        )"

    let drop = "drop table if exists \"Sanic\";"

    let truncate = "delete from \"Sanic\";"

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
        modified: DateTime
    }
    let small = 1000
    let big = 10000
    let collectionSmall = [ for i in 1..small -> { id = i; name = "John Doe"; modified = DateTime.Now } ]
    let collectionBig = [ for i in 1001..(small+big) -> { id = i; name = "Jane Doe"; modified = DateTime.Now } ]
    let modifiedCollectionSmall () = Utilities.mapOver collectionSmall
    let modifiedCollectionBig () = Utilities.mapOver collectionBig
    let sqliteConnectionString () = System.Environment.GetEnvironmentVariable("sqlite_connection_string")
    let postgresConnectionString () = System.Environment.GetEnvironmentVariable("postgres_connection_string")
    let sqliteState = SQLite( sqliteConnectionString (), Context.SQLite )