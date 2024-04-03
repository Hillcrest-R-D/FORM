namespace rec Benchmarks



module Data =
    open System
    open Form.Attributes
    open Form.Utilities

    type Context = 
    | SQLite = 1

    [<CLIMutable>]
    type Knockles =
        {
            id: int
            name: string
        }

    let sqliteConnectionString () = System.Environment.GetEnvironmentVariable("sqlite_connection_string")
    let postgresConnectionString () = System.Environment.GetEnvironmentVariable("postgres_connection_string")
    let sqliteState = SQLite( sqliteConnectionString (), Context.SQLite )
    [<CLIMutable>]
    type Sanic = {
        [<PrimaryKey("id", Context.SQLite)>]
        id: int 
        name: string
        optional : int option 
        modified: string
        [<On(typeof<Knockles>, 1, 1, "id", JoinDirection.Left, Context.SQLite)>]
        knockId: int 
        [<ByJoin(typeof<Knockles>, Context.SQLite)>]
        [<Arguments(1, Context.SQLite)>]
        knock: Form.Utilities.Relation<Sanic, Knockles>
    }
    let small = 1000
    let big = 10000
    let defaultKnocklesRelation = Relation<Sanic,Knockles>(1, sqliteState)
    let collectionSmallSanic = 
        [| for i in 1..small -> 
            { 
                id = i
                name = "John Doe" 
                optional = if i % 2 = 0 then None else Some i 
                modified = DateTime.Now.ToString("yyyy-MM-dd")
                knockId = i 
                knock = defaultKnocklesRelation
            } 
        |]
    let collectionSmallKnockles = 
        [| for i in 1..small -> 
            { 
                id = i
                name = "John Doe" 
            } 
        |]
    let collectionBigSanic = 
        [| for i in 1001..big -> 
            { 
                id = i
                name = "Jane Doe"
                optional = if i % 2 = 0 then None else Some i
                modified = DateTime.Now.ToString("yyyy-MM-dd") 
                knockId = i 
                knock = defaultKnocklesRelation
            } 
        |]
    let collectionBigKnockles = 
        [| for i in 1001..big -> 
            { 
                id = i
                name = "Jane Doe"
            } 
        |]
    let collectionsSanic = [|collectionSmallSanic|] //; collectionBigSanic

    let modifiedCollectionSmall () = Utilities.mapOver collectionSmallSanic
    let modifiedCollectionBig () = Utilities.mapOver collectionBigSanic

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
            id int not null,
            name varchar(32) not null,
            optional int null,
            modified varchar(16) not null,
            knockId int not null
        );
        create table if not exists \"Knockles\"(
            id int not null,
            name varchar(32) not null
        );
        "

    let drop = "drop table if exists \"Sanic\"; drop table if exists \"Knockles\";"

    let truncate = "delete from \"Sanic\"; delete from \"Knockles\";"

    
    // type OptionHandler<'T> () =
    //     inherit SqlMapper.TypeHandler<option<'T>> ()

    //     override __.SetValue (param, value) =
    //         let valueOrNull =
    //             match value with
    //             | Some x -> box x
    //             | None   -> null
    //         param.Value <- valueOrNull

    //     override __.Parse value =
    //         if Object.ReferenceEquals(value, null) || value = box DBNull.Value || isNull value
    //         then printfn "IS THIS WORKING?!?!?"; None
    //         else Some (value :?> 'T)

    type RelationHandler () =
        inherit SqlMapper.TypeHandler<Form.Utilities.Relation<Data.Sanic, Data.Knockles>>()

        // static member Default : Dapper.SqlMapper.ITypeHandler = new RelationHandler<Data.Sanic, Data.Knockles>()
        override __.SetValue (param, value) =
            //TODO
            param.Value <- null

        override __.Parse value =
            if Object.ReferenceEquals(value, null) || value = box DBNull.Value || isNull value
            then printfn "Null found!"
            else printfn "Null not found!"
            Form.Utilities.Relation<Data.Sanic, Data.Knockles>(1,Data.sqliteState)