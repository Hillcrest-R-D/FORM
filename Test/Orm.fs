module Test.Orm

open Form
open Form.Attributes
open Form.Utilities
open NUnit.Framework

System.IO.File.ReadAllLines("../../../../.env")
|> Array.iter( fun line -> 
    let chunks = line.Split("=")
    let variable = chunks[0]
    let value = System.String.Join("=", chunks[1..])
    printfn "%A %A" variable value
    System.Environment.SetEnvironmentVariable(variable, value)
)

let psqlConnectionString  = System.Environment.GetEnvironmentVariable("postgres_connection_string")
let odbcConnectionString  = System.Environment.GetEnvironmentVariable("odbc_connection_string")
let mysqlConnectionString  = ""
let mssqlConnectionString  = ""
let sqliteConnectionString  = System.Environment.GetEnvironmentVariable("sqlite_connection_string")

printfn "Postgres: %A" (psqlConnectionString )
printfn "ODBC: %A" (odbcConnectionString )
printfn "SQLite: %A" (sqliteConnectionString )



type Contexts =
    | PSQL = 1
    | MySQL = 2
    | MSSQL = 4
    | SQLite = 8
    | ODBC = 16

let psqlState  =     PSQL( psqlConnectionString , Contexts.PSQL )
let mysqlState  =    MySQL( mysqlConnectionString , Contexts.MySQL )
let mssqlState  =    MSSQL( mssqlConnectionString , Contexts.MSSQL )
let sqliteState  =   SQLite( sqliteConnectionString , Contexts.SQLite )
let odbcState  = ODBC(  odbcConnectionString , Contexts.ODBC )

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
        [<On(typeof<SubFact>, "factId", JoinDirection.Left, Contexts.PSQL)>]
        [<On(typeof<SubFact>, "factId", JoinDirection.Left, Contexts.SQLite)>]
        [<On(typeof<SubFact>, "factId", JoinDirection.Left, Contexts.ODBC)>]
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
        [<Column("sqliteName", Contexts.ODBC)>]
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
        subFact : string option
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
            subFact = Some "sooper dooper secret fact"
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



type FixtureArgs =
    static member Source : obj seq =
        seq {
            [| sqliteState |] 
            [| psqlState |]
            // [| odbcState |]
        }
        
[<SetUpFixture>]
type OrmSetup () = 
    [<OneTimeSetUp>]
    member _.Setup () = 
        printfn "sqlite - %A" (System.Environment.GetEnvironmentVariable("sqlite_connection_string"))
        printfn "postgres - %A" (System.Environment.GetEnvironmentVariable("postgres_connection_string"))
        printfn "odbc - %A" (System.Environment.GetEnvironmentVariable("odbc_connection_string"))

[<TestFixtureSource(typeof<FixtureArgs>, "Source")>]
type Orm (_testingState) =
    let testingState = _testingState
    let tableName = "\"Fact\""
    let testGuid1 = System.Guid.NewGuid().ToString()
    let testGuid2 = System.Guid.NewGuid().ToString()
    let testGuid3 = System.Guid.NewGuid().ToString()
    let testGuid4 = System.Guid.NewGuid().ToString()

    let nameCol = 
        match testingState with
        | SQLite _ -> "sqliteName"
        | PSQL _ -> "psqlName"
        | _ -> "sqliteName"

    let intType = 
        match testingState with 
        | SQLite _ -> "integer"
        | PSQL _ -> "bigint"
        | _ -> "bigint"
    let transaction = 
        None// Orm.beginTransaction testingState
    
    [<OneTimeSetUp>]
    member _.Setup () =
        let createTable = 
            $"DROP TABLE IF EXISTS {tableName};
            DROP TABLE IF EXISTS \"SubFact\";
            CREATE TABLE {tableName} (
                \"indexId\" {intType} not null,
                \"id\" text primary key,
                \"{nameCol}\" text null,
                \"timeStamp\" text,
                \"specialChar\" text,
                \"maybeSomething\" text,
                \"sometimesNothing\" {intType} null,
                \"biteSize\" text
            );
            CREATE TABLE \"SubFact\" (
                \"factId\" {intType} not null,
                \"subFact\" text not null
            );
            "

        Orm.execute testingState None createTable |> printfn "Create Table Returns: %A"
        
    [<Test>]
    [<NonParallelizable>]
    member _.Connect () =
        printfn "Contest: %A\n\n\n\n\n" (System.Environment.GetEnvironmentVariable("sqlite_connection_string"))
        match Orm.connect testingState with 
        | Ok _ -> Assert.Pass()
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.Insert () =
        match Orm.insert< Fact > testingState None true ( Fact.init() )  with 
        | Ok _ -> Assert.Pass() 
        | Error e -> Assert.Fail(e.ToString())
    
    [<Test>]
    [<NonParallelizable>]
    member _.InsertMany () =
        let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
        match Orm.insertMany< Fact > testingState None true ( str8Facts ) with 
        | Ok _ -> Assert.Pass() 
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.AsyncInsertMany () =
        async {
            let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
            match Orm.insertMany< Fact > testingState None true ( str8Facts ) with 
            | Ok _ -> 
                System.Threading.Thread.Sleep(10000)
                Assert.Pass() 
            | Error e -> Assert.Fail(e.ToString())
        } 
        |> Async.RunSynchronously


    [<Test>]
    [<NonParallelizable>]
    member _.QueryBuild () =
        printfn "%A" (queryBase< Fact > testingState)
        Assert.Pass()


    [<Test>]
    [<NonParallelizable>]
    member _.Select () =
        printfn "Selecting All..."
        match Orm.selectAll< Fact > testingState None with 
        | Ok facts -> 
            Seq.iter ( printfn  "%A") facts
            Assert.Pass(sprintf "facts: %A" facts) 
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.AsyncSelect () =
        printfn "Asynchronously Selecting All..."
        async {
            match Orm.selectAll< Fact > testingState None with 
            | Ok facts -> 
                // let newFacts = Seq.toList facts 
                Seq.iter ( printfn  "%A") facts 
                match testingState with 
                | SQLite _ -> () 
                | _ -> System.Threading.Thread.Sleep(10000)
                Assert.Pass(sprintf "facts: %A" facts) 
            | Error e -> Assert.Fail(e.ToString())
        }
        |> Async.RunSynchronously

    [<Test>]
    [<NonParallelizable>]
    member _.SelectLimit () =
        printfn "Selecting All..."
        match Orm.selectLimit< Fact > testingState None 5  with 
        | Ok facts -> 
            Seq.iter ( printfn  "%A") facts
            Assert.Pass(sprintf "facts: %A" facts) 
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.SelectWhere () =
        printfn "Selecting Where..."
        match Orm.selectWhere< Fact > testingState None "\"maybeSomething\" = 'true'" with 
        | Ok facts ->
            Assert.Pass(sprintf "facts: %A" (facts)) 
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.Update () =
        printfn "Updating..."
        let initial = { Fact.init() with id = testGuid1 }
        let changed = { initial with name = "Evan Towlett"}
        match Orm.update< Fact > testingState None changed with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())

    
    [<Test>]
    [<NonParallelizable>]
    member _.UpdateMany () =
        printfn "Updating many..."
        let initial = Fact.init() 
        let changed = { initial with name = "Evan Mowlett"; id = testGuid3 ; subFact= None}
        let changed2 = { initial with name = "Mac Flibby"; id = testGuid2; subFact = None}
        Orm.updateMany< Fact > testingState None [changed;changed2]  |> printf "%A"

        let evan = Orm.selectWhere<Fact> testingState None $"id = '{testGuid3}'" 
        let mac = Orm.selectWhere<Fact> testingState None $"id = '{testGuid2}'" 

        match evan, mac with 
        | Ok e, Ok m -> 
            if Seq.head e = changed && Seq.head m = changed2 
            then Assert.Pass()
            else Assert.Fail("Failed comparison.")
        | _, _ -> 
            Assert.Fail("Couldn't verify update happened")
        
        
    
    // [<Test>]
    // [<NonParallelizable>]
    // member _.UpdateManyWithTransaction () =
    //     printfn "Updating many with transaction..."
    //     let initial = Fact.init() 
    //     let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
    //     let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
    //     Orm.updateMany< Fact > testingState [changed;changed2] transaction
    //     |> printf "%A"
        
    //     Assert.Pass()
    
    [<Test>]
    [<NonParallelizable>]
    member _.UpdateWhere () =
        printfn "Updating..."
        let initial = Fact.init () 
        let changed = { initial with name = "Evan Howlett"}
        match Orm.updateWhere< Fact > testingState None "\"indexId\" = 1" changed   with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.Delete () =
        printfn "Deleting..."
        let initial = Fact.init () 
        let changed = { initial with name = "Evan Howlett"}
        match Orm.delete< Fact > testingState None changed with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())
    


    [<Test>]
    [<NonParallelizable>]
    member _.DeleteWhere () = 
        printfn "Deleting Where..."
        match Orm.deleteWhere< Fact > testingState None "\"indexId\" = 1" with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.DeleteMany () =
        printfn "Deleting Many..."
        let initial = Fact.init() 
        let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
        let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
        Orm.deleteMany< Fact > testingState None [changed;changed2] 
        |> function 
        | Ok i -> Assert.Pass(sprintf "%A" i )
        | Error e -> Assert.Fail(sprintf "%A" e)



    [<Test>]
    [<NonParallelizable>]
    member _.Reader () =
        printfn "Reading..."
        Orm.consumeReader<Fact> testingState 
        |> fun reader -> Orm.executeWithReader testingState None "select * from \"Fact\"" reader 
        |> function 
        | Ok facts -> Assert.Pass(sprintf "%A" facts)
        | Error e -> Assert.Fail(sprintf "%A" e)

    

    // [<Test>]
    // [<NonParallelizable>]
    // member _.ReaderWithTransaction () =
    //     printfn "Reading..."
    //     Orm.consumeReader<Fact> testingState 
    //     |> fun reader -> Orm.executeWithReader testingState "select * from \"Fact\"" reader transaction
    //     |> function 
    //     | Ok facts -> Assert.Pass(sprintf "%A" facts)
    //     | Error e -> Assert.Fail(sprintf "%A" e)


    // [<OneTimeTearDown>]
    [<Test>]
    [<NonParallelizable>]
    member _.TearDown () = 
        transaction
        |> Option.map ( Orm.commitTransaction )
        |> sprintf "Transaction: %A"
        |> Assert.Pass
 
[<TestFixtureSource(typeof<FixtureArgs>, "Source")>]
type OrmTransaction ( _testingState ) = 
    let testingState = _testingState ()
    let tableName = "\"Fact\""
    let testGuid1 = System.Guid.NewGuid().ToString()
    let testGuid2 = System.Guid.NewGuid().ToString()
    let testGuid3 = System.Guid.NewGuid().ToString()
    let testGuid4 = System.Guid.NewGuid().ToString()

    let nameCol = 
        match testingState with
        | SQLite _ -> "sqliteName"
        | PSQL _ -> "psqlName"
        | _ -> "sqliteName"

    let intType = 
        match testingState with 
        | SQLite _ -> "integer"
        | PSQL _ -> "bigint"
        | _ -> "bigint"
        
    let sleep () = System.Threading.Thread.Sleep(500)

    let commit transaction x = Orm.tryCommit transaction |> ignore; x 

    [<OneTimeSetUp>]
    member _.Setup () =
        let createTable = 
            $"DROP TABLE IF EXISTS {tableName};
            DROP TABLE IF EXISTS \"SubFact\";
            CREATE TABLE {tableName} (
                \"indexId\" {intType} not null,
                \"id\" text primary key,
                \"{nameCol}\" text null,
                \"timeStamp\" text,
                \"specialChar\" text,
                \"maybeSomething\" text,
                \"sometimesNothing\" {intType} null,
                \"biteSize\" text
            );
            CREATE TABLE \"SubFact\" (
                \"factId\" {intType} not null,
                \"subFact\" text not null
            );"

        
        Orm.execute testingState None createTable |> printfn "Create Table Returns: %A"


    [<Test>]
    [<NonParallelizable>]
    member _.InsertSelect () =
        // sleep ()
        let transaction = Orm.beginTransaction testingState
        let theFact = {Fact.init() with subFact = None}
        let mutable theBackFact = Fact.init()
        printfn "Do we have a transaction? %A" transaction
        // Orm.insert< SubFact > testingState true ({factId = theFact.indexId; subFact = "woooo"}) transaction |> ignore
        Orm.insert< Fact > testingState transaction true ( theFact ) 
        |> Result.bind ( fun _ -> 
            printfn "We have inserted"
            Orm.selectWhere< Fact > testingState transaction $"id = '{theFact.id}'" 
            |> fun x -> printfn "We have the facts: %A" x; x
            |> function 
            | Ok facts when Seq.length facts > 0 -> 
                theBackFact <- Seq.head facts
                Ok facts
            | Error e  -> Error e
            | _ -> Error (exn "No data returned by select, you forgot the facts!")
        )
        |> Result.map ( fun _ -> Orm.commitTransaction transaction )
        |> Result.mapError ( fun _ -> Orm.rollbackTransaction transaction )
        |> function 
        | Ok _ -> 
            if theFact = theBackFact 
            then Assert.Pass() 
            else Assert.Fail(sprintf "%A %A %A" testingState theFact theBackFact) 
        | Error error -> Assert.Fail(sprintf "%A %A" testingState (error.ToString())) 

    
    
    [<Test>]
    [<NonParallelizable>]
    member _.InsertDeleteSelect () =
        // sleep ()
        let transaction = Orm.beginTransaction testingState
        let theFact = Fact.init()
        let mutable theBackFact = Fact.init()
        let err = exn "No data returned by select, you forgot the facts!"
        printfn "Do we have a transaction? %A" transaction
        // Orm.insert< SubFact > testingState true ({factId = theFact.indexId; subFact = "woooo"}) transaction |> ignore
        Orm.insert< Fact > testingState transaction true ( theFact ) 
        |> Result.bind ( fun _ -> Orm.delete< Fact > testingState transaction theFact  )
        |> Result.bind ( fun _ -> 
            Orm.selectWhere< Fact > testingState transaction $"id = '{theFact.id}'" 
            |> function 
            | Ok facts when Seq.length facts > 0 -> 
                theBackFact <- Seq.head facts
                Ok facts
            | Error e  -> Error e
            | _ -> Error err
        )
        |> commit transaction
        |> function 
        | Ok _ -> Assert.Fail(sprintf "%A %A" theFact theBackFact) 
        | Error error -> 
            if err = error 
            then Assert.Pass()
            else Assert.Fail(error.ToString()) 


    [<Test>]
    [<NonParallelizable>]
    member _.InsertUpdateSelect () =
        // sleep ()
        let transaction = Orm.beginTransaction testingState
        let theFact = Fact.init()
        let theNewFact = { theFact with name = "All Facts, All the Time"; subFact = None }
        let mutable theBackFact = Fact.init() 
        let err = exn "No data returned by select, you forgot the facts!"
        printfn "Do we have a transaction? %A" transaction

        Orm.insert< Fact > testingState transaction true ( theFact ) 
        |> Result.bind ( fun _ -> Orm.update< Fact > testingState transaction theNewFact )
        |> Result.bind ( fun _ -> 
            Orm.selectWhere< Fact > testingState transaction $"id = '{theFact.id}'"  
            |> function 
            | Ok facts when Seq.length facts > 0 -> 
                theBackFact <- Seq.head facts
                Ok facts
            | Error e  -> Error e
            | _ -> Error err
        )
        |> commit transaction
        |> function 
        | Ok facts ->
            if theNewFact = theBackFact
            then 
                Assert.Pass(sprintf "You remembered the facts: %A - %A | %A" theFact theBackFact facts) 
            else 
                Assert.Fail(sprintf "Look at all these facts: %A - %A | %A" theFact theBackFact facts)
        | Error error ->
            Assert.Fail(error.ToString()) 
    
    [<Test>]
    [<NonParallelizable>]
    member _.ReaderWithTransaction () =
        printfn "Reading..."
        let transaction = Orm.beginTransaction testingState
        Orm.consumeReader<Fact> testingState 
        |> fun reader -> Orm.executeWithReader testingState transaction "select * from \"Fact\"" reader 
        |> commit transaction 
        |> function 
        | Ok facts -> Assert.Pass(sprintf "%A" facts)
        | Error e -> Assert.Fail(sprintf "%A" e)


    // // [<OneTimeTearDown>]
    // [<Test>]
    // [<NonParallelizable>]
    // member _.TearDown () = 
    //     transaction
    //     |> Option.map ( Orm.commitTransaction )
    //     |> sprintf "Transaction: %A"
    //     |> Assert.Pass
