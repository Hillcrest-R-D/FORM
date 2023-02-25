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
        DotNetEnv.Env.Load() |> sprintf "Loaded variables %A" |> logger.Log

[<TestFixtureSource(typeof<FixtureArgs>, "Source")>]
type Orm (_testingState) =
    let testingState = _testingState
    let tableName = "\"Fact\""

    let nameCol = 
        match testingState with
        | SQLite _ -> "sqliteName"
        | PSQL _ -> "psqlName"
        | _ -> "sqliteName"
    
    [<SetUp>]
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
            Assert.Pass(sprintf "facts: %A" facts) 
        | Error e -> Assert.Fail(e.ToString())

    [<Test>]
    [<NonParallelizable>]
    member _.SelectWhereTest () =
        printfn "Selecting All..."
        match Orm.SelectWhere< Fact > "1=0" testingState with 
        | Ok facts -> 
            Assert.Pass(sprintf "facts: %A" facts) 
        | Error e -> Assert.Fail(e.ToString())
    // [<TearDown>] 
    // member _.TearDown () = 
    //     match Orm.connect testingState with 
    //     | Ok con -> 
    //         con.Open() 
    //         Orm.Execute $"drop table {tableName};" testingState |> printfn "%A"
    //         con.Close()
    //     | Error e -> failwith <| e.ToString()