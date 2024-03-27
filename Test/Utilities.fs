namespace HCRD.FORM.Tests

module Utilities = 
    open Form.Attributes
    open Expecto
    let outputPath = "./console.log"
    let constructTest name message f =
        test name {
            Expect.wantOk ( f () |> Result.map ( fun _ -> () )) message 
        }

    let constructFailureTest name message f =
        test name {
            Expect.wantError ( f () |> Result.mapError ( fun _ -> () )) message 
        }

    let tableName = "\"Fact\""
    let nameCol = function 
        | SQLite _ -> "sqliteName"
        | PSQL _ -> "psqlName"
        | ODBC _ -> "psqlName"
        | _ -> "idk"
    let intType = function 
        | SQLite _ -> "integer"
        | _ -> "bigint"
    
    let testGuid1 = System.Guid.NewGuid().ToString()
    let testGuid2 = System.Guid.NewGuid().ToString()
    let testGuid3 = System.Guid.NewGuid().ToString()
    let testGuid4 = System.Guid.NewGuid().ToString()