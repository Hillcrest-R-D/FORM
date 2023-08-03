module HCRD.FORM.Tests.Setup 

open Form
open Form.Orm 
open Form.Attributes

DotNetEnv.Env.Load() |> ignore

let psqlConnectionString () = System.Environment.GetEnvironmentVariable("postgres_connection_string")
let mysqlConnectionString () = ""
let mssqlConnectionString () = ""
let sqliteConnectionString () = System.Environment.GetEnvironmentVariable("sqlite_connection_string")

type Contexts =
    | PSQL = 1
    | MySQL = 2
    | MSSQL = 4
    | SQLite = 8

let psqlState () =     PSQL( psqlConnectionString (), Contexts.PSQL )
let mysqlState () =    MySQL( mysqlConnectionString (), Contexts.MySQL )
let mssqlState () =    MSSQL( mssqlConnectionString (), Contexts.MSSQL )
let sqliteState () =   SQLite( sqliteConnectionString (), Contexts.SQLite )

[<Table("SubFact", Contexts.PSQL)>]
[<Table("SubFact", Contexts.MySQL)>]
[<Table("SubFact", Contexts.MSSQL)>]
[<Table("SubFact", Contexts.SQLite)>]
type SubFact = 
    {
        factId : int64 
        subFact : string
    }

[<Table("Fact", Contexts.PSQL)>]
[<Table("Fact", Contexts.MySQL)>]
[<Table("Fact", Contexts.MSSQL)>]
[<Table("Fact", Contexts.SQLite)>]
type Fact =
    {
        [<Id(Contexts.PSQL)>]
        [<Id(Contexts.SQLite)>]
        [<Id(Contexts.MySQL)>]
        [<Id(Contexts.MSSQL)>]
        [<On(typeof<SubFact>, "factId", JoinDirection.Left, Contexts.PSQL)>]
        [<On(typeof<SubFact>, "factId", JoinDirection.Left, Contexts.SQLite)>]
        indexId: int64
        [<PrimaryKey("pk",Contexts.PSQL)>]
        [<PrimaryKey("pk",Contexts.MySQL)>]
        [<PrimaryKey("pk",Contexts.MSSQL)>]
        [<PrimaryKey("pk",Contexts.SQLite)>]
        id: string
        [<Column("psqlName", Contexts.PSQL)>]
        [<Column("mysqlName", Contexts.MySQL)>]
        [<Column("mssqlName", Contexts.MSSQL)>]
        [<Column("sqliteName", Contexts.SQLite)>]
        [<SQLType("varchar(16)", Contexts.PSQL)>]
        [<SQLType("varchar(16)", Contexts.MySQL)>]
        [<SQLType("varchar(16)", Contexts.MSSQL)>]
        // [<SQLType("varchar(16)", Contexts.SQLite)>] !!! Won't work, sqlite doesn't have varchar
        name: string 
        [<Constraint("DEFAULT GETDATE()", Contexts.MSSQL)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP", Contexts.PSQL)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP()", Contexts.MySQL)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP", Contexts.SQLite)>]
        timeStamp: string
        [<Unique("group1", Contexts.PSQL)>]    
        specialChar : string
        [<SQLType("boolean", Contexts.PSQL)>]
        maybeSomething : string 
        [<Unique("group1", Contexts.PSQL)>] 
        sometimesNothing : int64 option
        [<Unique("group2", Contexts.PSQL)>]
        biteSize : string
        [<ByJoin(typeof<SubFact>, Contexts.PSQL)>]
        [<ByJoin(typeof<SubFact>, Contexts.SQLite)>]
        subFact : string 
    }

    //lookup = { id =  Orm.Node (  {_type = typeof<int>; value = 1 }, Orm.Leaf  { _type= typeof<string>; value = indexId }); value = None}
    // member Relationship (lookup) = 
    //     ^A 

    static member Relation (id, indexId) =
        { id =  Orm.Node (  {_type = typeof<int>; value = id }, Orm.Leaf  { _type= typeof<string>; value = indexId }); value = None}

module Fact = 
    let init () = 
        {
            indexId = 1
            id = System.Guid.NewGuid().ToString()
            name = "Gerry McGuire"
            timeStamp = System.DateTime.Now.ToString()
            specialChar = "Δ"
            maybeSomething = "true"
            sometimesNothing = Some 1
            biteSize =  "!aBite"
            subFact = "sooper dooper secret fact"
        }

    
type SerializedLogger() =

    // create the mailbox processor
    let agent = MailboxProcessor.Start(fun inbox ->

        // the message processing function
        let rec messageLoop () = async{

            // read a message
            let! msg = inbox.Receive()

            // write it to the log
            printfn "%A" msg

            // loop to top
            return! messageLoop ()
            }

        // start the loop
        messageLoop ()
        )

    // public interface
    member _.Log msg = agent.Post msg

// test in isolation
let logger = SerializedLogger()