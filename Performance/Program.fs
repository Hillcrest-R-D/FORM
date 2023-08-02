open System
open Form 
open Form.Attributes
open Microsoft.Data.Sqlite
open Dapper

DotNetEnv.Env.Load "../" |> printfn "%A"


type Context = 
    | SQLite = 1

type Sanic = {
    [<PrimaryKey("id", Context.SQLite)>]
    id: int 
    name: string
    modified: DateTime
}



module Utilities = 
    let mapOver = Seq.map ( fun x -> { x with modified = System.DateTime.Now } )
    let timeIt f = 
        let stopwatch = Diagnostics.Stopwatch.StartNew()
        f() |> ignore
        stopwatch.Stop()
        stopwatch.Elapsed
    let create = 
        "create table Sanic (
            id int not null,
            name varchar(32) not null,
            modified timestamp not null
        )"

    let drop = "drop table if exists Sanic"

module Data = 
    let collection1k = [ for i in 1..1000 -> { id = i; name = "John Doe"; modified = DateTime.Now } ]
    let collection10k = [ for i in 1..10000 -> { id = i; name = "Jane Doe"; modified = DateTime.Now } ]
    let modifiedCollection1k = Utilities.mapOver collection1k
    let modifiedCollection10k = Utilities.mapOver collection10k
    let sqliteConnectionString () = System.Environment.GetEnvironmentVariable("sqlite_connection_string")
    let postgresConnectionString () = System.Environment.GetEnvironmentVariable("postgres_connection_string")


let sqliteState = SQLite( Data.sqliteConnectionString (), Context.SQLite )

let formInsert collection = 
    Orm.insertMany<Sanic> sqliteState true collection None |> ignore

let formUpdate collection = 
    Orm.updateMany<Sanic> sqliteState collection None |> ignore

let formSelect limit = 
    Orm.selectLimit<Sanic> sqliteState limit None |> ignore



// setup sqlite
Orm.execute sqliteState Utilities.drop None |> printfn "%A"
Orm.execute sqliteState Utilities.create None |> printfn "%A"

Utilities.timeIt ( fun _ -> formInsert Data.collection1k ) |> printfn "Form insert 1k: %A"
Utilities.timeIt ( fun _ -> formInsert Data.collection10k ) |> printfn "Form insert 10k: %A"
Utilities.timeIt ( fun _ -> formSelect 1000 ) |> printfn "Form select 1k: %A"
Utilities.timeIt ( fun _ -> formSelect 10000 ) |> printfn "Form select 10k: %A"
Utilities.timeIt ( fun _ -> formUpdate Data.modifiedCollection1k ) |> printfn "Form update 1k: %A"
Utilities.timeIt ( fun _ -> formUpdate Data.modifiedCollection10k ) |> printfn "Form update 10k: %A"

