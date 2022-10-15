module HCRD.FORM.Tests.Setup 

open Form

let psqlConnectionString = ""
let mysqlConnectionString = ""
let mssqlConnectionString = ""
let sqliteConnectionString = "Data Source=./test.db;"

type Contexts =
    | PSQL = 1
    | MySQL = 2
    | MSSQL = 4
    | SQLite = 8

let psqlState =     PSQL( psqlConnectionString, Contexts.PSQL )
let mysqlState =    MySQL( mysqlConnectionString, Contexts.MySQL )
let mssqlState =    MSSQL( mssqlConnectionString, Contexts.MSSQL )
let sqliteState =   SQLite( sqliteConnectionString, Contexts.SQLite )

[<Table("Fact", Contexts.PSQL)>]
[<Table("Fact", Contexts.MySQL)>]
[<Table("Fact", Contexts.MSSQL)>]
[<Table("Fact", Contexts.SQLite)>]
type Fact =
    {
        [<Key(Key.Primary, Contexts.PSQL)>]
        [<Key(Key.Primary, Contexts.MySQL)>]
        [<Key(Key.Primary, Contexts.MSSQL)>]
        [<Key(Key.Primary, Contexts.SQLite)>]
        Id: string
        [<Column("psqlName", Contexts.PSQL)>]
        [<Column("mysqlName", Contexts.MySQL)>]
        [<Column("mssqlName", Contexts.MSSQL)>]
        [<Column("sqliteName", Contexts.SQLite)>]
        [<SQLType("varchar(16)", Contexts.PSQL)>]
        [<SQLType("varchar(16)", Contexts.MySQL)>]
        [<SQLType("varchar(16)", Contexts.MSSQL)>]
        // [<SQLType("varchar(16)", Contexts.SQLite)>] !!! Won't work, sqlite doesn't have varchar
        Name: string 
        [<Constraint("DEFAULT GETDATE()", Contexts.MSSQL)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP", Contexts.PSQL)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP()", Contexts.MySQL)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP", Contexts.SQLite)>]
        TimeStamp: string    
        SpecialChar : string
        MaybeSomething : string
        SometimesNothing : int option
        BiteSize : string
    }

module Fact = 
    let init () = 
        {
            Id = System.Guid.NewGuid().ToString()
            Name = "Gerry McGuire"
            TimeStamp = System.DateTime.Now.ToString()
            SpecialChar = "Δ"
            MaybeSomething = "true"
            SometimesNothing = None
            BiteSize =  "!yourmom"
        }

    
