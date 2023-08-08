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
            DROP TABLE IF EXISTS \"SubFact\";
            CREATE TABLE {tableName} (
                \"indexId\" int not null,
                \"id\" text primary key,
                \"{nameCol}\" text null,
                \"timeStamp\" text,
                \"specialChar\" text,
                \"maybeSomething\" text,
                \"sometimesNothing\" int null,
                \"biteSize\" text
            );
            CREATE TABLE \"SubFact\" (
                \"factId\" int not null,
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

    
    let sleep () = System.Threading.Thread.Sleep(500)

    let commit transaction x = Orm.tryCommit transaction |> ignore; x 

    [<OneTimeSetUp>]
    member _.Setup () =
        let createTable = 
            $"DROP TABLE IF EXISTS {tableName};
            DROP TABLE IF EXISTS \"SubFact\";
            CREATE TABLE {tableName} (
                \"indexId\" int not null,
                \"id\" text primary key,
                \"{nameCol}\" text null,
                \"timeStamp\" text,
                \"specialChar\" text,
                \"maybeSomething\" text,
                \"sometimesNothing\" int null,
                \"biteSize\" text
            );
            CREATE TABLE \"SubFact\" (
                \"factId\" int not null,
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
            Orm.selectWhere< Fact > testingState transaction $"id = '{theFact.id}'" 
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
            else Assert.Fail(sprintf "%A %A" theFact theBackFact) 
        | Error error -> Assert.Fail(error.ToString()) 

    
    
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
