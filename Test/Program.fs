namespace HCRD.FORM.Tests
open Form
open Form.Orm
open Form.Attributes
open Form.Utilities
open Setup
module Main = 
    type Contexts =
        | Test1 = 0

    let mssql = MSSQL("", Contexts.Test1)
    [<Table("Temp.Test", Contexts.Test1)>]
        type Test =
            { Col1 : int 
              Col2 : int 
            }

    [<EntryPoint>]
    let main _ = 
        DotNetEnv.Env.Load "../" |> ignore
        let testId = 1
        let testIdSeq = seq{1}
        // printfn "int to seq: %A\n int to seq to seq: %A" (Seq.concat [[testId]]) (Seq.red [[testIdSeq]]) 

        let psqlConnectionString = 
            System.Environment.GetEnvironmentVariable("postgres_connection_string")
        let psqlState =     PSQL( psqlConnectionString, Contexts.PSQL )
        if typedefof<int64> <> typedefof<seq<obj>> then printfn "match"

        // let rel : Orm.Relation<int64, Fact>  = { id = 1; value = None}
        // let test = (Orm.Relation<int64,Fact>.Value rel psqlState).value
        columnMapping<Fact> <| sqliteState()
        |> printfn "%A"

        queryBase<Fact> <| sqliteState()
        |> printfn "%A"

        0




