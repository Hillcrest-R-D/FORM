module HCRD.FORM.Tests.Setup 

open Form

DotNetEnv.Env.Load() |> ignore

let psqlConnectionString = 
    System.Environment.GetEnvironmentVariable("postgres_connection_string")
let mysqlConnectionString = ""
let mssqlConnectionString = ""
let sqliteConnectionString = 
    System.Environment.GetEnvironmentVariable("sqlite_connection_string")

type Contexts =
    | PSQL = 1
    | MySQL = 2
    | MSSQL = 4
    | SQLite = 8

let psqlState =     PSQL( psqlConnectionString, Contexts.PSQL )
let mysqlState =    MySQL( mysqlConnectionString, Contexts.MySQL )
let mssqlState =    MSSQL( mssqlConnectionString, Contexts.MSSQL )
let sqliteState  =   SQLite( sqliteConnectionString, Contexts.SQLite )

[<Table("Fact", Contexts.PSQL)>]
[<Table("Fact", Contexts.MySQL)>]
[<Table("Fact", Contexts.MSSQL)>]
[<Table("Fact", Contexts.SQLite)>]
type Fact =
    {
        [<Key(Key.PrimaryKey, Contexts.PSQL)>]
        [<Key(Key.PrimaryKey, Contexts.MySQL)>]
        [<Key(Key.PrimaryKey, Contexts.MSSQL)>]
        [<Key(Key.PrimaryKey, Contexts.SQLite)>]
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
        specialChar : string
        [<SQLType("boolean", Contexts.PSQL)>]
        maybeSomething : string 
        sometimesNothing : int option
        biteSize : string
    }

module Fact = 
    let init () = 
        {
            id = System.Guid.NewGuid().ToString()
            name = "Gerry McGuire"
            timeStamp = System.DateTime.Now.ToString()
            specialChar = "Δ"
            maybeSomething = "true"
            sometimesNothing = None
            biteSize =  "!yourmom"
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