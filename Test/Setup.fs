module HCRD.FORM.Tests.Setup 

open Form

let psqlConnectionString = ""
let mysqlConnectionString = ""
let mssqlConnectionString = ""
let sqliteConnectionString = "Data Source=./test.db;"

type Contexts =
    | PSQL = 1
    | MYSQL = 2
    | MSSQL = 4
    | SQLITE = 8

let psqlState = PSQL(psqlConnectionString, Contexts.PSQL)
let mysqlState = PSQL(mysqlConnectionString, Contexts.MYSQL)
let mssqlState = MSSQL(psqlConnectionString, Contexts.MSSQL)
let sqliteState = SQLite(psqlConnectionString, Contexts.SQLITE)




[<Table("Facts", Contexts.PSQL)>]
[<Table("Facts", Contexts.MYSQL)>]
[<Table("Facts", Contexts.MSSQL)>]
[<Table("Facts", Contexts.SQLITE)>]
type Facts =
    {
        Id: System.Guid
        Name: string 
        TimeStamp: System.DateTime
        SpecialChar : char
        MaybeSomething : bool
        SometimesNothing : int option
        BiteSize : byte array
    }