namespace HCRD.FORM.Tests


module Main = 
    open Setup
    open Form
    open Form.Attributes
    open Expecto
    open System.IO

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

    let orm testingState = 
        let testGuid1 = System.Guid.NewGuid().ToString()
        let testGuid2 = System.Guid.NewGuid().ToString()
        let testGuid3 = System.Guid.NewGuid().ToString()
        let testGuid4 = System.Guid.NewGuid().ToString()

        let transaction = 
            None// Orm.beginTransaction testingState
    
        
        let setup () =
            constructTest 
                "" 
                ""
                ( fun _ ->
                    let createTable = 
                        $"DROP TABLE IF EXISTS {tableName};
                        DROP TABLE IF EXISTS \"SubFact\";
                        CREATE TABLE {tableName} (
                            \"indexId\" {intType testingState} not null,
                            \"id\" text primary key,
                            \"{nameCol testingState}\" text null,
                            \"timeStamp\" text,
                            \"specialChar\" text,
                            \"maybeSomething\" text,
                            \"sometimesNothing\" {intType testingState} null,
                            \"biteSize\" text
                        );
                        CREATE TABLE \"SubFact\" (
                            \"factId\" {intType testingState} not null,
                            \"subFact\" text not null
                        );
                        "

                    Orm.execute testingState None createTable
                )
        
        let connect () = 
            constructTest "Connect" "Successfully connected." ( fun _ -> Orm.connect testingState )

        let insert () =
            constructTest "Insert" "Fact inserted." ( fun _ -> Orm.insert< Fact > testingState None true ( Fact.init() ) ) 
                
        let insertMany () =
            constructTest 
                "InsertMany" 
                "Inserted many facts."
                ( fun _ ->
                    let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
                    Orm.insertMany< Fact > testingState None true ( str8Facts )
                )
                  
        // let asyncInsertMany () =
        //     constructTest
        //         "InsertMany-Async"
        //         "Inserted many facts asynchronously."
        //         (fun _ -> 
        //             let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
        //             Orm.insertMany< Fact > testingState None true ( str8Facts )
        //         )

        let select () =
            constructTest 
                "Select" 
                "Select"
                ( fun _ -> Orm.selectAll< Fact > testingState None |> Orm.toResultSeq ) 
                
        // let asyncSelect () =
        //     constructTest
        //         "Select-Async"
        //         "Select-Async"
        //         (fun _ -> Orm.selectAll< Fact > testingState None)  
        
        let selectLimit () =
            constructTest 
                "SelectLimit"
                "SelectLimit"
                (fun _ -> Orm.selectLimit< Fact > testingState None 5 |> Orm.toResultSeq)

        let selectWhere () =
            constructTest 
                "SelectWhere"
                "SelectWhere"
                (fun _ -> Orm.selectWhere< Fact > testingState None ( "\"maybeSomething\" = ':1'", [| "true" |]) |> Orm.toResultSeq )

        let selectWhereWithIn () =
            constructTest 
                "SelectWhereWithIn"
                "SelectWhereWithIn"
                (fun _ -> Orm.selectWhere< Fact > testingState None ( """("id" in (:1) and "maybeSomething" = ':2') or "indexId" in (:3)""", [| [ testGuid1; testGuid2; testGuid3 ]; "false"; [ 1.4; 2.2; 3.5 ] |]) |> Orm.toResultSeq )
        
        let selectWhereWithInFailure () =
            test "SelectWhereWithInFailure" {
                Expect.wantError (
                    Orm.selectWhere< Fact > testingState None ( """("id" in (:1) and "maybeSomething" = ':2') or "indexId" in (:3)""", [| [ testGuid1; testGuid2; testGuid3 ]; "false"; [ Fact.init(); Fact.init(); Fact.init() ] |]) 
                    |> Orm.toResultSeq ) "SelectWhereWithInFailure" 
                |> ignore
            }
        let update () =
            constructTest 
                "Update"
                "Update"
                (fun _ ->
                    let initial = { Fact.init() with id = testGuid1 }
                    let changed = { initial with name = "Evan Towlett"}
                    Orm.update< Fact > testingState None changed
                )

        let updateMany () =
            constructTest 
                "UpdateMany"
                "UpdateMany"
                ( fun _ -> 
                let initial = Fact.init() 
                let changed = { initial with name = "Evan Mowlett"; id = testGuid3 ; subFact= None}
                let changed2 = { initial with name = "Mac Flibby"; id = testGuid2; subFact = None}
                Orm.updateMany< Fact > testingState None [changed;changed2]  |> printf "%A"

                let evan = Orm.selectWhere<Fact> testingState None ( "id = ':1'", [| testGuid3 |] ) |> Orm.toResultSeq
                let mac = Orm.selectWhere<Fact> testingState None ( "id = ':1'", [| testGuid2 |] ) |> Orm.toResultSeq

                match evan, mac with 
                | Ok e, Ok m -> 
                    if Seq.head e = changed && Seq.head m = changed2 
                    then Ok ()
                    else Result.Error "Update not applied."
                | Result.Error ex, _ 
                | _, Result.Error ex -> Result.Error ex.Message
                    
                )
            
        
        // 
        // 
        // member _.UpdateManyWithTransaction () =
        //     printfn "Updating many with transaction..."
        //     let initial = Fact.init() 
        //     let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
        //     let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
        //     Orm.updateMany< Fact > testingState [changed;changed2] transaction
        //     |> printf "%A"
            
        //     Assert.Pass()
        
        
        
        let updateWhere () =
            constructTest
                "UpdateWhere"
                "UpdateWhere"
                (fun _ -> 
                    let initial = Fact.init () 
                    let changed = { initial with name = "Evan Howlett"}
                    Orm.updateWhere< Fact > testingState None ( "\"indexId\" = :1", [| "1" |] ) changed 
                )

        let delete () =
            constructTest 
                "Delete"
                "Delete"
                ( fun _ ->
                    let initial = Fact.init () 
                    let changed = { initial with name = "Evan Howlett"}
                    Orm.delete< Fact > testingState None changed
                )
        let deleteWhere () = 
            constructTest
                "DeleteWhere"
                "DeleteWhere"
                (fun _ -> Orm.deleteWhere< Fact > testingState None ( "\"indexId\" = :1", [| "1" |] ) )
                
        let deleteMany () =
            constructTest
                "DeleteMany"
                "DeleteMany"
                (fun _ ->
                    let initial = Fact.init() 
                    let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
                    let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
                    Orm.deleteMany< Fact > testingState None [changed;changed2] 
                )
        let reader () =
            constructTest
                "Reader"
                "Reader"
                (fun _ ->
                    Orm.consumeReader<Fact> testingState 
                    |> fun reader -> Orm.executeWithReader testingState None "select * from \"Fact\"" reader |> Orm.toResultSeq
                )

        // 
        // 
        // let readerWithTransaction () =
        //     printfn "Reading..."
        //     Orm.consumeReader<Fact> testingState 
        //     |> fun reader -> Orm.executeWithReader testingState "select * from \"Fact\"" reader transaction
        //     |> function 
        //     | Ok facts -> Assert.Pass(sprintf "%A" facts)
        //     | Result.Error e -> Assert.Fail(sprintf "%A" e)


        // 
        
        
        let tearDown () = 
            constructTest 
                "Teardown"
                "Teardown"
                ( fun _ -> 
                    transaction
                    |> Option.map ( Orm.commitTransaction  )
                    |> function
                    | Some o -> Result.Error ""
                    | None -> Ok ()
                )

        testSequenced <| testList "Base ORM tests" [
            connect ()
            setup ()
            testSequenced <| testList "Tests" [
                // insert ()
                // insertMany ()
                // // asyncInsertMany ()
                // select ()
                // // asyncSelect ()
                // selectLimit ()
                // selectWhere ()
                // selectWhereWithIn ()
                selectWhereWithInFailure ()
                // update ()
                // updateMany ()
                // updateWhere ()
                // delete ()
                // deleteWhere ()
                // deleteMany ()
                // reader ()
            ]
            tearDown ()
        ]
        
    
    
    let transaction testingState = 
        let tableName = "\"Fact\""
        let testGuid1 = System.Guid.NewGuid().ToString()
        let testGuid2 = System.Guid.NewGuid().ToString()
        let testGuid3 = System.Guid.NewGuid().ToString()
        let testGuid4 = System.Guid.NewGuid().ToString()
            
        let sleep () = System.Threading.Thread.Sleep(500)

        let commit transaction x = Orm.tryCommit transaction |> ignore; x 
        
        let setup () =
            constructTest 
                    "" 
                    ""
                    ( fun _ ->
                        let createTable = 
                            $"DROP TABLE IF EXISTS {tableName};
                            DROP TABLE IF EXISTS \"SubFact\";
                            CREATE TABLE {tableName} (
                                \"indexId\" {intType testingState} not null,
                                \"id\" text primary key,
                                \"{nameCol testingState}\" text null,
                                \"timeStamp\" text,
                                \"specialChar\" text,
                                \"maybeSomething\" text,
                                \"sometimesNothing\" {intType testingState} null,
                                \"biteSize\" text
                            );
                            CREATE TABLE \"SubFact\" (
                                \"factId\" {intType testingState} not null,
                                \"subFact\" text not null
                            );
                            "

                        Orm.execute testingState None createTable 
                    )         
        
        let insertSelect () =
            constructTest
                "InsertSelect"
                "InsertSelect"
                (fun _ ->
                    let transaction = Orm.beginTransaction testingState
                    let theFact = {Fact.init() with subFact = None}
                    let mutable theBackFact = Fact.init()
                    Orm.insert< Fact > testingState transaction true ( theFact ) 
                    |> Result.bind ( fun _ -> 
                        printfn "We have inserted"
                        Orm.selectWhere< Fact > testingState transaction ("id = ':1'", [|theFact.id|])
                        |> Orm.toResultSeq 
                        |> fun x -> printfn "We have the facts: %A" x; x
                        |> function 
                        | Ok facts when Seq.length facts > 0 -> 
                            theBackFact <- Seq.head facts
                            Ok facts
                        | Result.Error e  -> Result.Error e
                        | _ -> Result.Error (exn "No data returned by select, you forgot the facts!")
                    )
                    |> Result.map ( fun _ -> Orm.commitTransaction transaction )
                    |> Result.mapError ( fun _ -> Orm.rollbackTransaction transaction )
                    |> function 
                    | Ok _ -> 
                        if theFact = theBackFact 
                        then Ok ()
                        else Result.Error (sprintf "%A %A %A" testingState theFact theBackFact) 
                    | Result.Error error -> Result.Error (sprintf "%A %A" testingState (error.ToString())) 
                )

        let insertDeleteSelect () =
            constructTest 
                "InsertDeleteSelect"
                "InsertDeleteSelect"
                ( fun _ ->
                    let transaction = Orm.beginTransaction testingState
                    let theFact = Fact.init()
                    let mutable theBackFact = Fact.init()
                    let err = exn "No data returned by select, you forgot the facts!"
                    // Orm.insert< SubFact > testingState true ({factId = theFact.indexId; subFact = "woooo"}) transaction |> ignore
                    Orm.insert< Fact > testingState transaction true ( theFact ) 
                    |> Result.bind ( fun _ -> Orm.delete< Fact > testingState transaction theFact  )
                    |> Result.bind ( fun _ -> 
                        Orm.selectWhere< Fact > testingState transaction ("id = ':1'", [|theFact.id|]) 
                        |> Orm.toResultSeq
                        |> function 
                        | Ok facts when Seq.length facts > 0 -> 
                            theBackFact <- Seq.head facts
                            Ok facts
                        | Result.Error e  -> Result.Error e
                        | _ -> Result.Error err
                    )
                    |> commit transaction
                    |> function 
                    | Ok _ -> Result.Error (sprintf "%A %A" theFact theBackFact) 
                    | Result.Error error -> 
                        if err = error 
                        then Ok ()
                        else Result.Error(error.ToString()) 
                )

        let insertUpdateSelect () =
            constructTest
                "InsertUpdateSelect"
                "InsertUpdateSelect"
                (fun _ ->
                    let transaction = Orm.beginTransaction testingState
                    let theFact = Fact.init()
                    let theNewFact = { theFact with name = "All Facts, All the Time"; subFact = None }
                    let mutable theBackFact = Fact.init() 
                    let err = exn "No data returned by select, you forgot the facts!"
        
                    Orm.insert< Fact > testingState transaction true ( theFact ) 
                    |> Result.bind ( fun _ -> Orm.update< Fact > testingState transaction theNewFact )
                    |> Result.bind ( fun _ -> 
                        Orm.selectWhere< Fact > testingState transaction ("id = ':1'", [|theFact.id|])  
                        |> Orm.toResultSeq
                        |> function 
                        | Ok facts when Seq.length facts > 0 -> 
                            theBackFact <- Seq.head facts
                            Ok facts
                        | Result.Error e  -> Result.Error e
                        | _ -> Result.Error err
                    )
                    |> commit transaction
                    |> function 
                    | Ok facts ->
                        if theNewFact = theBackFact
                        then 
                            Ok(sprintf "You remembered the facts: %A - %A | %A" theFact theBackFact facts) 
                        else 
                            Result.Error(sprintf "Look at all these facts: %A - %A | %A" theFact theBackFact facts)
                    | Result.Error error ->
                        Result.Error(error.ToString()) 
                )
        
        let readerWithTransaction () =
            constructTest
                "Reader-Transaction"
                "Reader with Transaction"
                (fun _ -> 
                    let transaction = Orm.beginTransaction testingState
                    Orm.consumeReader<Fact> testingState 
                    |> fun reader -> Orm.executeWithReader testingState transaction "select * from \"Fact\"" reader 
                    |> Orm.toResultSeq
                    |> commit transaction 
                )

        testSequenced <| testList "Base ORM tests" [
            setup ()
            testSequenced <| testList "Tests" [
                insertSelect ()
                insertDeleteSelect ()
                insertUpdateSelect ()
                readerWithTransaction ()
            ]
        ]


    [<EntryPoint>]
    let main argv  =     
        System.IO.File.ReadAllLines("../.env")
        |> Array.iter( fun line -> 
            let chunks = line.Split("=")
            let variable = chunks[0]
            let value = System.String.Join("=", chunks[1..])
            printfn "%A %A" variable value
            System.Environment.SetEnvironmentVariable(variable, value)
        )
        
        
        let psqlConnectionString  = System.Environment.GetEnvironmentVariable("postgres_connection_string")
        let odbcConnectionString  = System.Environment.GetEnvironmentVariable("odbc_connection_string")
        let mysqlConnectionString  = ""
        let mssqlConnectionString  = ""
        let sqliteConnectionString  = System.Environment.GetEnvironmentVariable("sqlite_connection_string")
        
        let psqlState = PSQL( psqlConnectionString , Contexts.PSQL )
        let mysqlState = MySQL( mysqlConnectionString , Contexts.MySQL )
        let mssqlState = MSSQL( mssqlConnectionString , Contexts.MSSQL )
        let sqliteState = SQLite( sqliteConnectionString , Contexts.SQLite )
        let odbcState = ODBC( odbcConnectionString , Contexts.ODBC )

        let states = 
            [ 
            // odbcState
            // ; psqlState
            sqliteState
            // ; mysqlState
            // ; mssqlstate
            ]

        // use fs = new FileStream(outputPath, FileMode.Create)
        // use writer = new StreamWriter( fs, System.Text.Encoding.UTF8 )

        // writer.AutoFlush <- true

        // System.Console.SetOut(writer)
        // System.Console.SetError(writer)

        states
        |> List.map ( 
            orm  
            >> runTestsWithCLIArgs [] argv 
        )
        |> printfn "%A"

        // states 
        // |> List.map ( 
        //     transaction 
        //     >> runTestsWithCLIArgs [] argv 
        // )
        // |> printfn "%A"
        // let testGuid1 = System.Guid.NewGuid().ToString()
        // let testGuid2 = System.Guid.NewGuid().ToString()
        // let testGuid3 = System.Guid.NewGuid().ToString()
        // Orm.selectWhere< Fact > sqliteState None ( """("id" in (:1) and "maybeSomething" = ':2') or "indexId" in (:3)""", [| [ testGuid1; testGuid2; testGuid3 ]; "false"; [ Fact.init(); Fact.init(); Fact.init() ] |]) |> Orm.toResultSeq
        // |> printfn "Direct: %A"

        0
