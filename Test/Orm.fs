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
        Orm.beginTransaction testingState
    
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

        let pragma = "PRAGMA journal_mode=WAL;"

         
        match Orm.connect testingState with 
        | Ok con -> 
            con.Open()
            match testingState with 
            | SQLite _ -> 
                Orm.execute testingState pragma 
                |> sprintf "Enabling WAL mode for SQLite: %A" 
                |> fun x -> System.IO.File.WriteAllText("pragma.log", x )
            | _ -> ()
            Orm.execute testingState createTable  |> printfn "Create Table Returns: %A"
            con.Close()
        | Error e -> failwith (e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.Conn () =
        printfn "Contest: %A\n\n\n\n\n" (System.Environment.GetEnvironmentVariable("sqlite_connection_string"))
        match Orm.connect testingState with 
        | Ok _ -> Assert.Pass()
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.Insert () =
        match Orm.insert< Fact > testingState true ( Fact.init() ) with 
        | Ok _ -> Assert.Pass() 
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.InsertMany () =
        let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
        match Orm.insertMany< Fact > testingState true ( str8Facts )  with 
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
    member _.SelectWithTransaction () =
        printfn "Selecting All... with transaction"
        match Orm.selectAll< Fact > testingState transaction with 
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
    member _.SelectWhereWithTransaction () =
        printfn "Selecting Where..."
        match Orm.selectWhere< Fact > testingState "\"maybeSomething\" = 'true'" transaction with 
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
    member _.UpdateWithTransaction () =
        printfn "Updating..."
        let initial = { Fact.init() with id = testGuid1 }
        let changed = { initial with name = "Evan Towlett"}
        match Orm.update< Fact > testingState changed transaction with 
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
    
    [<Test>]
    [<NonParallelizable>]
    member _.UpdateManyWithTransaction () =
        printfn "Updating many with transaction..."
        let initial = Fact.init() 
        let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
        let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
        Orm.updateMany< Fact > testingState [changed;changed2] transaction
        |> printf "%A"
        
        Assert.Pass()
    
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
    member _.UpdateWhereWithTransaction () =
        printfn "Updating Where With Transaction..."
        let transaction = 
            Orm.beginTransaction testingState
        let initial = Fact.init () 
        let changed = { initial with name = "Evan Howlett"}
        match Orm.updateWhere< Fact > testingState "\"indexId\" = 1" changed transaction with 
        | Ok inserted ->
            transaction
            |> Option.map Orm.commitTransaction 
            Assert.Pass(sprintf "facts: %A, %A" inserted transaction)
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.Delete () =
        printfn "Updating..."
        let initial = Fact.init () 
        let changed = { initial with name = "Evan Howlett"}
        match Orm.delete< Fact > testingState changed None with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())
    
    [<Test>]
    [<NonParallelizable>]
    member _.DeleteWithTransaction () =
        printfn "Updating..."
        let initial = Fact.init () 
        let changed = { initial with name = "Evan Howlett"}
        match Orm.delete< Fact > testingState changed transaction with 
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
    member _.DeleteWhereWithTransaction () = 
        printfn "Deleting Where..."
        match Orm.deleteWhere< Fact > testingState "\"indexId\" = 1" transaction with 
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
    member _.DeleteManyWithTransaction () =
        printfn "Deleting Many..."
        let initial = Fact.init() 
        let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
        let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
        Orm.deleteMany< Fact > testingState [changed;changed2] transaction
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


    [<Test>]
    [<NonParallelizable>]
    member _.ReaderWithTransaction () =
        printfn "Reading..."
        Orm.consumeReader<Fact> testingState 
        |> fun reader -> Orm.executeWithReader testingState "select * from \"Fact\"" reader transaction
        |> function 
        | Ok facts -> Assert.Pass(sprintf "%A" facts)
        | Error e -> Assert.Fail(sprintf "%A" e)


    // [<OneTimeTearDown>]
    [<Test>]
    [<NonParallelizable>]
    member _.TearDown () = 
        transaction
        |> Option.map ( Orm.commitTransaction )
        |> sprintf "Transaction: %A"
        |> Assert.Pass

// [<TestFixture>]
// type Postgres() =
//     let state = psqlState 
//     [<OneTimeSetUp>]
//     member _.Setup () =
//         let createTable = 
//             $"DROP TABLE IF EXISTS \"Fact\";
//             CREATE TABLE \"Fact\" (
//                 \"id\" character varying (36) primary key,
//                 \"psqlName\" character varying (32) null,
//                 \"timeStamp\" timestamp without time zone,
//                 \"specialChar\" character varying (1),
//                 \"maybeSomething\" boolean,
//                 \"sometimesNothing\" int null,
//                 \"biteSize\" character varying (8)
//             );"
        
//         match Orm.connect state with 
//         | Ok con -> 
//             con.Open()
//             Orm.Execute createTable state |> printfn "Create Table Returns: %A"
//             con.Close()
//         | Error e -> failwith (e.ToString())

//     [<Test>]
//     [<NonParallelizable>]
//     member _.Conn () =
//         match Orm.connect state with 
//         | Ok _ -> Assert.Pass()
//         | Error e -> Assert.Fail(e.ToString())


//     [<Test>]
//     [<NonParallelizable>]
//     member _.Insert () =
//         match Orm.Insert< Fact > ( Fact.init() ) state with 
//         | Ok _ -> Assert.Pass() 
//         | Error e -> Assert.Fail(e.ToString())
        

//     [<Test>]
//     [<NonParallelizable>]
//     member _.InsertMany () =
//         let str8Facts = [Fact.init(); Fact.init(); Fact.init(); Fact.init()]
//         match Orm.InsertAll< Fact > ( str8Facts ) state with 
//         | Ok count -> 
//             printfn "Inserted %A records --------------------------------------" count 
//             Assert.Pass() 
//         | Error e -> Assert.Fail(e.ToString())
        
//     [<Test>]
//     [<NonParallelizable>]
//     member _.QueryBuild () =
//         printfn "%A" (Orm.queryBase< Fact > state)
//         Assert.Pass()


//     [<Test>]
//     [<NonParallelizable>]
//     member _.Select () =
//         printfn "Selecting All..."
//         match Orm.SelectAll< Fact > state with 
//         | Ok facts -> 
//             printfn  "All facts: %A ----------------------------" facts
//             Assert.Pass() 
//         | Error e -> Assert.Fail(e.ToString())

//     [<Test>]
//     [<NonParallelizable>]
//     member _.SelectWhere () =
//         printfn "Selecting Where..."
//         match Orm.SelectWhere< Fact > "\"maybeSomething\" = true" state with 
//         | Ok facts ->
//             printfn "Where facts: %A ----------------------------------" (Seq.head <| facts)
//             Assert.Pass() 
//         | Error e -> Assert.Fail(e.ToString())