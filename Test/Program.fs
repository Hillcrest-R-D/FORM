namespace HCRD.FORM.Tests


module Main = 
    open Setup
    open Form
    open Form.Attributes
    open Expecto
    open System.IO
    open HCRD.FORM.Tests.Utilities
    open HCRD.FORM.Tests.BaseOrmTests
    open HCRD.FORM.Tests.RelationTests
    

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
                        CREATE TABLE IF NOT EXISTS \"SubFact\" (
                                \"factId\" {intType testingState} not null,
                                \"subFact\" text not null
                            );
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
                        DELETE FROM \"SubFact\";
                        INSERT INTO \"SubFact\" VALUES (1, 'a really good subfact');
                        "
                    
                    Orm.execute testingState None createTable
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

        testSequenced <| testList "FORM tests" [
            connect testingState
            setup ()
            //base orm tests
            testSequenced <| testList "Base Orm Tests" [
                insert testingState
                insertMany testingState
                insertAlot testingState
                // // asyncInsertMany testingState
                select testingState
                // // // asyncSelect testingState
                selectLimit testingState
                selectBigLimit testingState
                selectWhere testingState
                selectWhereWithIn testingState
                selectWhereWithInFailure testingState
                update testingState
                updateMany testingState
                updateWhere testingState
                delete testingState
                deleteWhere testingState
                deleteMany testingState
                reader testingState
            ]
            //relation tests
            testSequenced <| testList "Relation Tests" [
                relationEvaluteAndNestedAreEqual testingState
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
                    let theFact = {Fact.init() with subFact = Unchecked.defaultof<Form.Utilities.Relation<Fact, SubFact>>}
                    let mutable theBackFact = Fact.init()
                    Orm.insert< Fact > testingState transaction true ( theFact ) 
                    |> Result.bind ( fun _ -> 
                        printfn "We have inserted"
                        Orm.selectWhere< Fact > testingState transaction ("id = ':1'", [|theFact.id|])
                        |> Result.toResultSeq 
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
                        |> Result.toResultSeq
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
                    let theNewFact = { theFact with name = "All Facts, All the Time"; subFact = Unchecked.defaultof<Form.Utilities.Relation<Fact, SubFact>> }
                    let mutable theBackFact = Fact.init() 
                    let err = exn "No data returned by select, you forgot the facts!"
        
                    Orm.insert< Fact > testingState transaction true ( theFact ) 
                    |> Result.bind ( fun _ -> Orm.update< Fact > testingState transaction theNewFact )
                    |> Result.bind ( fun _ -> 
                        Orm.selectWhere< Fact > testingState transaction ("id = ':1'", [|theFact.id|])  
                        |> Result.toResultSeq
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
                    |> Result.toResultSeq
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
        

        let states = 
            [ 
            PSQL( psqlConnectionString , Contexts.PSQL ) 
            // MySQL( mysqlConnectionString , Contexts.MySQL )
            // MSSQL( mssqlConnectionString , Contexts.MSSQL )
            // SQLite( sqliteConnectionString , Contexts.SQLite )
            // ODBC( odbcConnectionString , Contexts.ODBC )
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
        // Orm.selectWhere< Fact > sqliteState None ( """("id" in (:1) and "maybeSomething" = ':2') or "indexId" in (:3)""", [| [ testGuid1; testGuid2; testGuid3 ]; "false"; [ Fact.init(); Fact.init(); Fact.init() ] |]) |> Result.toResultSeq
        // |> printfn "Direct: %A"
        // Orm.selectAll<Fact> psqlState None 
        // |> Result.toResultSeq
        // |> function 
        // | Ok v ->
        //     v
        //     |> Seq.map ( fun x -> Relation.evaluate x.subFact None x )
        //     |> printfn "%A"
        // | _ -> printfn "Get fucked."

        0
