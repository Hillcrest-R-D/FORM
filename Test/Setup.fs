module HCRD.FORM.Tests.Setup 

open Form
open Form.Orm 
open Form.Utilities
open Form.Attributes
open dotenv.net


type Contexts =
    | PSQL = 1
    | MySQL = 2
    | MSSQL = 4
    | SQLite = 8
    | ODBC = 16

[<Table("SubFact", Contexts.PSQL)>]
[<Table("SubFact", Contexts.MySQL)>]
[<Table("SubFact", Contexts.MSSQL)>]
[<Table("SubFact", Contexts.SQLite)>]
[<Table("SubFact", Contexts.ODBC)>]
type SubFact = 
    {
        factId : int64 
        subFact : string
    }

[<Table("Fact", Contexts.PSQL)>]
[<Table("Fact", Contexts.MySQL)>]
[<Table("Fact", Contexts.MSSQL)>]
[<Table("Fact", Contexts.SQLite)>]
[<Table("Fact", Contexts.ODBC)>]
type Fact =
    {
        [<Id(Contexts.PSQL)>]
        [<Id(Contexts.SQLite)>]
        [<Id(Contexts.MySQL)>]
        [<Id(Contexts.MSSQL)>]
        [<Id(Contexts.ODBC)>]
        [<On(typeof<SubFact>, 1, 1, "factId", JoinDirection.Left, Contexts.PSQL)>]
        [<On(typeof<SubFact>, 1, 1, "factId", JoinDirection.Left, Contexts.SQLite)>]
        [<On(typeof<SubFact>, 1, 1, "factId", JoinDirection.Left, Contexts.ODBC)>]
        indexId: int64
        [<PrimaryKey("pk",Contexts.PSQL)>]
        [<PrimaryKey("pk",Contexts.MySQL)>]
        [<PrimaryKey("pk",Contexts.MSSQL)>]
        [<PrimaryKey("pk",Contexts.SQLite)>]
        [<PrimaryKey("pk",Contexts.ODBC)>]
        id: string
        [<Column("psqlName", Contexts.PSQL)>]
        [<Column("mysqlName", Contexts.MySQL)>]
        [<Column("mssqlName", Contexts.MSSQL)>]
        [<Column("sqliteName", Contexts.SQLite)>]
        [<Column("psqlName", Contexts.ODBC)>]
        [<SQLType("varchar(16)", Contexts.PSQL)>]
        [<SQLType("varchar(16)", Contexts.MySQL)>]
        [<SQLType("varchar(16)", Contexts.MSSQL)>]
        [<SQLType("varchar(16)", Contexts.ODBC)>]
        // [<SQLType("varchar(16)", Contexts.SQLite)>] !!! Won't work, sqlite doesn't have varchar
        name: string 
        [<Constraint("DEFAULT GETDATE()", Contexts.MSSQL)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP", Contexts.PSQL)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP()", Contexts.MySQL)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP", Contexts.SQLite)>]
        [<Constraint("DEFAULT CURRENT_TIMESTAMP", Contexts.ODBC)>]
        timeStamp: string
        [<Unique("group1", Contexts.PSQL)>]
        [<Unique("group1", Contexts.ODBC)>]
        specialChar : string
        [<SQLType("boolean", Contexts.PSQL)>]
        [<SQLType("boolean", Contexts.ODBC)>]
        maybeSomething : string 
        [<Unique("group1", Contexts.PSQL)>]
        [<Unique("group1", Contexts.ODBC)>]
        sometimesNothing : int64 option
        [<Unique("group2", Contexts.PSQL)>]
        [<Unique("group2", Contexts.ODBC)>]
        biteSize : string
        [<ByJoin(typeof<SubFact>, Contexts.SQLite)>]
        [<ByJoin(typeof<SubFact>, Contexts.PSQL)>]
        [<ByJoin(typeof<SubFact>, Contexts.ODBC)>]
        [<Arguments(EvaluationStrategy.Lazy, 1, Contexts.SQLite)>]
        [<Arguments(EvaluationStrategy.Lazy, 1, Contexts.PSQL)>]
        [<Arguments(EvaluationStrategy.Lazy, 1, Contexts.ODBC)>]
        subFact : Form.Utilities.Relation<Fact, SubFact>
    }

    //lookup = { id =  Orm.Node (  {_type = typeof<int>; value = 1 }, Orm.Leaf  { _type= typeof<string>; value = indexId }); value = None}
    // member Relationship (lookup) = 
    //     ^A 

    // static member Relation (id, indexId) =
    //     { id =  Orm.Node (  {_type = typeof<int>; value = id }, Orm.Leaf  { _type= typeof<string>; value = indexId }); value = None}

module Fact = 
    let init () = 
        {
            indexId = 1L
            id = System.Guid.NewGuid().ToString()
            name = "Gerry McGuire"
            timeStamp = System.DateTime.Now.ToString()
            specialChar = "Δ"
            maybeSomething = "true"
            sometimesNothing = Some 1L
            biteSize =  "!aBite"
            subFact = Unchecked.defaultof<Form.Utilities.Relation<Fact, SubFact>>
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