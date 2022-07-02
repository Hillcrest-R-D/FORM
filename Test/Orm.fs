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

[<SetUp>]
let Setup () =
    let createTable = 
        "drop table if exists Fact;
        create table Fact (
            Id text primary key,
            sqliteName text not null,
            TimeStamp text,
            SpecialChar text,
            MaybeSomething text,
            SometimesNothing int null,
            BiteSize text
        );"

    match Orm.connect sqliteState with 
    | Ok con -> 
        con.Open()
        Orm.execute createTable sqliteState |> printfn "%A"
        con.Close()
    | Error e -> failwith (e.ToString())

let inline passIfTrue cond = 
    if cond then
        Assert.Pass()
    else 
        Assert.Fail()

[<Test>]
let insertTest () =
    match Orm.insert< Fact > ( Fact.init() ) sqliteState with 
    | Ok _ -> Assert.Pass() 
    | Error e -> Assert.Fail(e.ToString())

[<Test>]
let queryBuildTest () =
    printfn "%A" (Orm.queryBase< Fact > sqliteState)
    Assert.Pass()


[<Test>]
let selectTest () =
    match Orm.selectAll< Fact > sqliteState with 
    | Ok facts -> Assert.Pass(sprintf "%A" facts) 
    | Error e -> Assert.Fail(e.ToString())

// [<TearDown>] 
// let TearDown () = 
//     match Orm.connect sqliteState with 
//     | Ok con -> 
//         con.Open() 
//         Orm.execute "drop table Fact;" sqliteState |> printfn "%A"
//         con.Close()
//     | Error e -> failwith <| e.ToString()
