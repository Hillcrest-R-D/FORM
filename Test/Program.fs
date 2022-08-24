namespace HCRD.FORM.Tests
open Form
open Form.Orm
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
        let test1 = [{ Col1 = 2; Col2 = 3 }; {Col1 = 1; Col2 = 4}]
        Orm.makeInsertMany ( table< Test > mssql )  ( columns< Test > mssql )  test1 mssql 
        |> printfn "%A"
        0




