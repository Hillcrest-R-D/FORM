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
        
        match Orm.connect testingState with 
        | Ok con -> 
            con.Open()
            Orm.execute testingState createTable  |> printfn "Create Table Returns: %A"
            con.Close()
        | Error e -> failwith (e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.ConnTest () =
        printfn "Contest: %A\n\n\n\n\n" (System.Environment.GetEnvironmentVariable("sqlite_connection_string"))
        match Orm.connect testingState with 
        | Ok _ -> Assert.Pass()
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.InsertTest () =
        match Orm.insert< Fact > testingState true ( Fact.init() ) with 
        | Ok _ -> Assert.Pass() 
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.InsertManyTest () =
        let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
        match Orm.insertMany< Fact > testingState true ( str8Facts )  with 
        | Ok _ -> Assert.Pass() 
        | Error e -> Assert.Fail(e.ToString())
        
    [<Test>]
    [<NonParallelizable>]
    member _.QueryBuildTest () =
        printfn "%A" (Orm.queryBase< Fact > testingState)
        Assert.Pass()


    [<Test>]
    [<NonParallelizable>]
    member _.SelectTest () =
        printfn "Selecting All..."
        match Orm.selectAll< Fact > testingState with 
        | Ok facts -> 
            Seq.iter ( printfn  "%A") facts
            Assert.Pass(sprintf "facts: %A" facts) 
        | Error e -> Assert.Fail(e.ToString())

    // [<Test>]
    // [<NonParallelizable>]
    // member _.RelationTest () =
    //     printfn "Selecting All..."
    //     let rel : Orm.Relation<Fact>  = Fact.Relation (1, testGuid1)
    //     // rel.Value testingState -> {rel with value = Some|None} 
    //     let test = (Orm.Relation<Fact>.Value rel testingState).value
    //     printfn "Relation test: %A" test
    //     match test with 
    //     | Some ent -> Assert.Pass( sprintf "Entity succesfully obtained via Relation: %A" ent)
    //     | None -> Assert.Fail()

    [<Test>]
    [<NonParallelizable>]
    member _.SelectWhereTest () =
        printfn "Selecting Where..."
        match Orm.selectWhere< Fact > testingState "\"maybeSomething\" = 'true'"  with 
        | Ok facts ->
            Assert.Pass(sprintf "facts: %A" (facts)) 
        | Error e -> Assert.Fail(e.ToString())
    
    [<Test>]
    [<NonParallelizable>]
    member _.UpdateTest () =
        printfn "Updating..."
        let initial = { Fact.init() with id = testGuid1 }
        let changed = { initial with name = "Evan Towlett"}
        match Orm.update< Fact > testingState changed with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())
    
    [<Test>]
    [<NonParallelizable>]
    member _.UpdateManyTest () =
        printfn "Updating..."
        let initial = Fact.init() 
        let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
        let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
        Orm.updateMany< Fact > testingState [changed;changed2] 
        |> printf "%A"
        
        Assert.Pass()

        // match  with 
        // | Ok inserted ->
        //     Assert.Pass(sprintf "facts: %A" inserted)
        // | Error e -> Assert.Fail(e.ToString())
    
    [<Test>]
    [<NonParallelizable>]
    member _.UpdateWhereTest () =
        printfn "Updating..."
        let initial = Fact.init () 
        let changed = { initial with name = "Evan Howlett"}
        match Orm.updateWhere< Fact > testingState "\"indexId\" = 1" changed  with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.DeleteTest () =
        printfn "Updating..."
        let initial = Fact.init () 
        let changed = { initial with name = "Evan Howlett"}
        match Orm.delete< Fact > testingState changed with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.DeleteWhereTest () = 
        printfn "Updating..."
        match Orm.deleteWhere< Fact > testingState "\"indexId\" = 1"  with 
        | Ok inserted ->
            Assert.Pass(sprintf "facts: %A" inserted)
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.DeleteManyTest () =
        printfn "Updating..."
        let initial = Fact.init() 
        let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
        let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
        Orm.deleteMany< Fact > testingState [changed;changed2] 
        |> printf "%A"
        
        Assert.Pass()







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
//     member _.ConnTest () =
//         match Orm.connect state with 
//         | Ok _ -> Assert.Pass()
//         | Error e -> Assert.Fail(e.ToString())


//     [<Test>]
//     [<NonParallelizable>]
//     member _.InsertTest () =
//         match Orm.Insert< Fact > ( Fact.init() ) state with 
//         | Ok _ -> Assert.Pass() 
//         | Error e -> Assert.Fail(e.ToString())
        

//     [<Test>]
//     [<NonParallelizable>]
//     member _.InsertManyTest () =
//         let str8Facts = [Fact.init(); Fact.init(); Fact.init(); Fact.init()]
//         match Orm.InsertAll< Fact > ( str8Facts ) state with 
//         | Ok count -> 
//             printfn "Inserted %A records --------------------------------------" count 
//             Assert.Pass() 
//         | Error e -> Assert.Fail(e.ToString())
        
//     [<Test>]
//     [<NonParallelizable>]
//     member _.QueryBuildTest () =
//         printfn "%A" (Orm.queryBase< Fact > state)
//         Assert.Pass()


//     [<Test>]
//     [<NonParallelizable>]
//     member _.SelectTest () =
//         printfn "Selecting All..."
//         match Orm.SelectAll< Fact > state with 
//         | Ok facts -> 
//             printfn  "All facts: %A ----------------------------" facts
//             Assert.Pass() 
//         | Error e -> Assert.Fail(e.ToString())

//     [<Test>]
//     [<NonParallelizable>]
//     member _.SelectWhereTest () =
//         printfn "Selecting Where..."
//         match Orm.SelectWhere< Fact > "\"maybeSomething\" = true" state with 
//         | Ok facts ->
//             printfn "Where facts: %A ----------------------------------" (Seq.head <| facts)
//             Assert.Pass() 
//         | Error e -> Assert.Fail(e.ToString())