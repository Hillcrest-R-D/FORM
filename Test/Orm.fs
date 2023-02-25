module Test.Orm

open Form
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

[<TestFixtureSource(typeof<FixtureArgs>, "Source")>]
type Orm (_testingState) =
    let testingState = _testingState
    let tableName = "\"Fact\""

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
            Orm.Execute createTable testingState |> printfn "Create Table Returns: %A"
            con.Close()
        | Error e -> failwith (e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.ConnTest () =
        match Orm.connect testingState with 
        | Ok _ -> Assert.Pass()
        | Error e -> Assert.Fail(e.ToString())


    [<Test>]
    [<NonParallelizable>]
    member _.InsertTest () =
        match Orm.Insert< Fact > ( Fact.init() ) testingState with 
        | Ok _ -> Assert.Pass() 
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.InsertManyTest () =
        let str8Facts = [Fact.init(); Fact.init(); Fact.init(); Fact.init()]
        match Orm.InsertAll< Fact > ( str8Facts ) testingState with 
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
        match Orm.SelectAll< Fact > testingState with 
        | Ok facts -> 
            Seq.iter ( printfn  "%A") facts
            Assert.Pass(sprintf "facts: %A" facts) 
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.SelectWhereTest () =
        printfn "Selecting All..."
        match Orm.SelectWhere< Fact > "\"maybeSomething\" = 'true'" testingState with 
        | Ok facts ->
            Assert.Pass(sprintf "facts: %A" (Seq.head <| facts)) 
        | Error e -> Assert.Fail(e.ToString())


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