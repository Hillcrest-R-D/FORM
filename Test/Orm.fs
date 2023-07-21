module Test.Orm

open Form
open Form.Attributes
open HCRD.FORM.Tests.Setup 
open NUnit.Framework

type FixtureArgs =
    static member Source : obj seq = seq {
        [| sqliteState |] 
        [| psqlState |]
    }
    
[<SetUpFixture>]
type OrmSetup () = 
    [<OneTimeSetUp>]
    member _.Setup () = 
        DotNetEnv.Env.Load() |> printf "Loaded variables %A" 
        printfn "%A\n\n\n\n\n" (System.Environment.GetEnvironmentVariable("sqlite_connection_string"))

[<TestFixtureSource(typeof<FixtureArgs>, "Source")>]
type Orm (_testingState) =
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

    let transaction = 
        None// Orm.beginTransaction testingState
    
    [<OneTimeSetUp>]
    member _.Setup () =
        let createTable = 
            $"DROP TABLE IF EXISTS {tableName};
            CREATE TABLE {tableName} (
                \"indexId\" int not null,
                \"id\" text primary key,
                \"{nameCol}\" text null,
                \"timeStamp\" text,
                \"specialChar\" text,
                \"maybeSomething\" text,
                \"sometimesNothing\" int null,
                \"biteSize\" text
            );"

        Orm.execute testingState createTable None  |> printfn "Create Table Returns: %A"
        
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
        match Orm.insert< Fact > testingState true ( Fact.init() ) None with 
        | Ok _ -> Assert.Pass() 
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.InsertMany () =
        let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
        match Orm.insertMany< Fact > testingState true ( str8Facts ) None with 
        | Ok _ -> Assert.Pass() 
        | Error e -> Assert.Fail(e.ToString())
        
    [<Test>]
    [<NonParallelizable>]
    member _.QueryBuild () =
        printfn "%A" (Orm.queryBase< Fact > testingState)
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
    member _.SelectWhere () =
        printfn "Selecting Where..."
        match Orm.selectWhere< Fact > testingState "\"maybeSomething\" = 'true'" None with 
        | Ok facts ->
            Assert.Pass(sprintf "facts: %A" (facts)) 
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.Update () =
        printfn "Updating..."
        let initial = { Fact.init() with id = testGuid1 }
        let changed = { initial with name = "Evan Towlett"}
        match Orm.update< Fact > testingState changed None with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())

    
    [<Test>]
    [<NonParallelizable>]
    member _.UpdateMany () =
        printfn "Updating many..."
        let initial = Fact.init() 
        let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
        let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
        Orm.updateMany< Fact > testingState [changed;changed2] None 
        |> printf "%A"
        
        Assert.Pass()
    
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
        match Orm.updateWhere< Fact > testingState "\"indexId\" = 1" changed None  with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.Delete () =
        printfn "Deleting..."
        let initial = Fact.init () 
        let changed = { initial with name = "Evan Howlett"}
        match Orm.delete< Fact > testingState changed None with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())
    


    [<Test>]
    [<NonParallelizable>]
    member _.DeleteWhere () = 
        printfn "Deleting Where..."
        match Orm.deleteWhere< Fact > testingState "\"indexId\" = 1" None with 
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
        Orm.deleteMany< Fact > testingState [changed;changed2] None
        |> function 
        | Ok i -> Assert.Pass(sprintf "%A" i )
        | Error e -> Assert.Fail(sprintf "%A" e)



    [<Test>]
    [<NonParallelizable>]
    member _.Reader () =
        printfn "Reading..."
        Orm.consumeReader<Fact> testingState 
        |> fun reader -> Orm.executeWithReader testingState "select * from \"Fact\"" reader None
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

    
    [<OneTimeSetUp>]
    member _.Setup () =
        let createTable = 
            $"DROP TABLE IF EXISTS {tableName};
            CREATE TABLE {tableName} (
                \"indexId\" int not null,
                \"id\" text primary key,
                \"{nameCol}\" text null,
                \"timeStamp\" text,
                \"specialChar\" text,
                \"maybeSomething\" text,
                \"sometimesNothing\" int null,
                \"biteSize\" text
            );"

        
        Orm.execute testingState createTable None  |> printfn "Create Table Returns: %A"


    [<Test>]
    [<NonParallelizable>]
    member _.InsertSelect () =
        let transaction = Orm.beginTransaction testingState
        let theFact = Fact.init()
        let mutable theBackFact = Fact.init()
        printfn "Do we have a transaction? %A" transaction
        testingState
        |> function 
        | SQLite _ -> Orm.execute testingState "PRAGMA journal_mode=WAL;" transaction
        | _ -> Ok 0
        |> Result.bind ( fun _ -> Orm.insert< Fact > testingState true ( theFact ) transaction )
        |> Result.bind ( fun _ -> 
            Orm.selectWhere< Fact > testingState $"id = '{theFact.id}'" transaction 
            |> function 
            | Ok facts when Seq.length facts > 0 -> 
                theBackFact <- Seq.head facts
                Ok facts
            | Error e  -> Error e
            | _ -> Error (exn "No data returned by select, you forgot the facts!")
        )
        |> Result.map ( fun _ -> Orm.commitTransaction transaction )
        |> function 
        | Ok _ -> 
            if theFact = theBackFact 
            then Assert.Pass() 
            else Assert.Fail(sprintf "%A %A" theFact theBackFact) 
        | Error error -> Assert.Fail(error.ToString()) 

    
    
    [<Test>]
    [<NonParallelizable>]
    member _.InsertDeleteSelect () =
        let transaction = Orm.beginTransaction testingState
        let theFact = Fact.init()
        let mutable theBackFact = Fact.init()
        let err = exn "No data returned by select, you forgot the facts!"
        printfn "Do we have a transaction? %A" transaction
        testingState
        |> function 
        | SQLite _ -> Orm.execute testingState "PRAGMA journal_mode=WAL;" transaction
        | _ -> Ok 0
        |> Result.bind ( fun _ -> Orm.insert< Fact > testingState true ( theFact ) transaction )
        |> Result.bind ( fun _ -> Orm.delete< Fact > testingState theFact transaction )
        |> Result.bind ( fun _ -> 
            Orm.selectWhere< Fact > testingState $"id = '{theFact.id}'" transaction 
            |> function 
            | Ok facts when Seq.length facts > 0 -> 
                theBackFact <- Seq.head facts
                Ok facts
            | Error e  -> Error e
            | _ -> Error err
        )
        |> Result.map ( fun _ -> Orm.commitTransaction transaction )
        |> function 
        | Ok _ -> Assert.Fail(sprintf "%A %A" theFact theBackFact) 
        | Error error -> 
            if err = error 
            then Assert.Pass()
            else Assert.Fail(error.ToString()) 

    // [<Test>]
    // [<NonParallelizable>]
    // member _.ReaderWithTransaction () =
    //     printfn "Reading..."
    //     Orm.consumeReader<Fact> testingState 
    //     |> fun reader -> Orm.executeWithReader testingState "select * from \"Fact\"" reader transaction
    //     |> function 
    //     | Ok facts -> Assert.Pass(sprintf "%A" facts)
    //     | Error e -> Assert.Fail(sprintf "%A" e)


    // // [<OneTimeTearDown>]
    // [<Test>]
    // [<NonParallelizable>]
    // member _.TearDown () = 
    //     transaction
    //     |> Option.map ( Orm.commitTransaction )
    //     |> sprintf "Transaction: %A"
    //     |> Assert.Pass
