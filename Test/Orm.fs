module Test.Orm

open Form
open HCRD.FORM.Tests.Setup 
open NUnit.Framework

let connector f =
    match Orm.connect sqliteState with 
    | Ok conn -> 
        conn.Open() 
        f conn
        conn.Close() 
        Assert.Pass()
    | Error e -> Assert.Fail(e.ToString())

(* 
    This may cause issues with timing. We noticed SetUp, even though it's called setup, 
    is not actually executed first.
*)
[<SetUp>]
// [<NonParallelizable>]
let Setup () =
    let createTable = 
        "drop table if exists Fact;
        create table Fact (
            Id text primary key,
            sqliteName text null,
            TimeStamp text,
            SpecialChar text,
            MaybeSomething text,
            SometimesNothing int null,
            BiteSize text
        );"

    match Orm.connect sqliteState with 
    | Ok con -> 
        con.Open()
        Orm.Execute createTable sqliteState |> printfn "%A"
        con.Close()
    | Error e -> failwith (e.ToString())

let inline passIfTrue cond = 
    if cond then
        Assert.Pass()
    else 
        Assert.Fail()

[<NonParallelizable>]
[<Test>]
let connTest () =
    match Orm.connect sqliteState with 
    | Ok _ -> Assert.Pass()
    | Error e -> Assert.Fail(e.ToString())


[<Test>]
[<NonParallelizable>]
let insertTest () =
    match Orm.Insert< Fact > ( Fact.init() ) sqliteState with 
    | Ok _ -> Assert.Pass() 
    | Error e -> Assert.Fail(e.ToString())

[<Test>]
[<NonParallelizable>]
let insertManyTest () =
    let str8Facts = [Fact.init(); Fact.init(); Fact.init(); Fact.init()]
    match Orm.InsertAll< Fact > ( str8Facts ) sqliteState with 
    | Ok _ -> Assert.Pass() 
    | Error e -> Assert.Fail(e.ToString())
    
[<Test>]
[<NonParallelizable>]
let queryBuildTest () =
    printfn "%A" (Orm.queryBase< Fact > sqliteState)
    Assert.Pass()


[<Test>]
[<NonParallelizable>]
let selectTest () =
    printfn "Selecting All..."
    match Orm.SelectAll< Fact > sqliteState with 
    | Ok facts -> 
        printf "facts: %A" facts
        Assert.Pass(sprintf "facts: %A" facts) 
    | Error e -> Assert.Fail(e.ToString())



// [<TearDown>] 
// let TearDown () = 
//     match Orm.connect sqliteState with 
//     | Ok con -> 
//         con.Open() 
//         Orm.execute "drop table Fact;" sqliteState |> printfn "%A"
//         con.Close()
//     | Error e -> failwith <| e.ToString()
