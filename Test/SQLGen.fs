module Test.DSL

open Form
open Form.Attributes
open HCRD.FORM.Tests.Setup 
open NUnit.Framework

type Contexts =
    | Default = 0

[<Table("schema1.Table1 t1 JOIN schema2.Table2 t2 ON t2.id = t1.id WHERE t1.privacyflag = 0", Contexts.Default)>]
type StraightFacts =
    {
        [<Column("t1.id", Contexts.Default)>]
        id : int64
        [<Column("t1.col1", Contexts.Default)>]
        col1 : int
        [<Column("t2.col6", Contexts.Default)>]
        col2 : int   
    }
let state = PSQL("", Contexts.Default)
// Orm.selectAll<StraightFacts> "" state
(* 
    This may cause issues with timing. We noticed SetUp, even though it's called setup, 
    is not actually executed first.
*)
[<SetUp>]
// [<NonParallelizable>]
let Setup () =
    ()
    // let createTable = 
    //     "drop table if exists Fact;
    //     create table Fact (
    //         Id text primary key,
    //         sqliteName text null,
    //         TimeStamp text,
    //         SpecialChar text,
    //         MaybeSomething text,
    //         SometimesNothing int null,
    //         BiteSize text
    //     );"
    // match Orm.connect sqliteState with 
    // | Ok con -> 
    //     con.Open()
    //     Orm.Execute createTable sqliteState |> printfn "%A"
    //     con.Close()
    // | Error e -> failwith (e.ToString())

// [<Test>]
// [<NonParallelizable>]
// let basic_querygen () =
//     let query = 
//         [ select<StraightFacts>
//         ]
//     printfn "%A" query.Compile 