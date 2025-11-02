namespace HCRD.FORM.Tests


module Main =
    open Setup
    open Form
    open Form.Attributes
    open Expecto
    open System.IO

    let outputPath = "./console.log"

    let constructSeqTest name message f =
        test name {
            Expect.wantOk
                (f ()
                 |> Result.map (fun t ->
                     Seq.toList t |> ignore
                     ()
                 ))
                message
        }

    let constructScalarTest name message f =
        test name { Expect.wantOk (f () |> Result.map (fun _ -> ())) message }

    let constructFailureTest name message f =
        test name { Expect.wantError (f () |> Result.mapError (fun _ -> ())) message }

    let tableName = "\"Fact\""

    let nameCol =
        function
        | SQLite _ -> "sqliteName"
        | PSQL _ -> "psqlName"
        | ODBC _ -> "psqlName"
        | _ -> "idk"

    let intType =
        function
        | SQLite _ -> "integer"
        | _ -> "bigint"

    let datetimeType =
        function
        | PSQL _ -> "timestamp"
        | SQLite _ -> "datetime"
        | ODBC _ -> "timestamp"
        | _ -> "datetime"

    let datetimeOffsetType =
        function
        | PSQL _ -> "timestamp without time zone"
        | SQLite _ -> "datetime"
        | ODBC _ -> "timestamp"
        | _ -> "datetime"

    let orm testingState =
        let testGuid1 = System.Guid.NewGuid().ToString ()
        let testGuid2 = System.Guid.NewGuid().ToString ()
        let testGuid3 = System.Guid.NewGuid().ToString ()
        let testGuid4 = System.Guid.NewGuid().ToString ()

        let transaction = None // Orm.beginTransaction testingState
        let prefix = 
            testingState
            |> function
            | PSQL _ -> "PSQL"
            | SQLite _ -> "SQLite"
            | ODBC _ -> "ODBC"
            | MySQL _ -> "MySQL"
            | MSSQL _ -> "MSSQL"

        let setup () =
            constructScalarTest
                ""
                ""
                (fun _ ->
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
                                \"biteSize\" text,
                                \"commonCol\" text,
                                \"aDateTime\" {datetimeType testingState} null
                            );
                            CREATE TABLE \"SubFact\" (
                                \"factId\" {intType testingState} not null,
                                \"subFact\" text not null,
                                \"commonCol\" text
                            );
                            "

                    Orm.execute testingState None createTable
                )

        let connect () =
            constructScalarTest "Connect" "Successfully connected." (fun _ -> Orm.connect testingState)

        let connectionClosesAfterConsumption () =
            test "Connection Closes After Seq Consumption" {
                let mutable connectabetical : System.Data.Common.DbConnection = null

                let s =
                    Form.Utilities.withTransaction
                        testingState
                        (fun _ -> Utilities.Sequence <| seq { Ok () })
                        (fun conn ->
                            seq {
                                connectabetical <- conn
                                yield Ok ()
                                conn.Close ()
                            }
                            |> Utilities.Sequence
                        )
                        None
                    |> Utilities.liftSequenceResult

                Expect.isNull connectabetical "Sentinal connection was set before sequence was evaluated."
                let l = Seq.toList s
                Expect.equal connectabetical.State System.Data.ConnectionState.Closed "Connection was not closed after sequence evaluation."
            }

        let insert () =
            constructScalarTest "Insert" "Fact inserted." (fun _ -> Orm.insert<Fact> testingState None true (Fact.init ()))

        let insertMany () =
            constructScalarTest
                "InsertMany"
                "Inserted many facts."
                (fun _ ->
                    let str8Facts =
                        [
                            { Fact.init () with id = testGuid1 }
                            { Fact.init () with
                                id = testGuid2
                                sometimesNothing = None
                            }
                            { Fact.init () with id = testGuid3 }
                            Fact.init ()
                        ]

                    Orm.insertMany<Fact> testingState None true (str8Facts) |> Orm.toResultSeq
                )

        // let asyncInsertMany () =
        //     constructTest
        //         "InsertMany-Async"
        //         "Inserted many facts asynchronously."
        //         (fun _ ->
        //             let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
        //             Orm.insertMany< Fact > testingState None true ( str8Facts )
        //         )

        let queryBase () =
            test "queryBase generates the proper SQL" {
                let mutable expected =
                    $""" "Fact"."indexId", "Fact"."id", "Fact"."{nameCol testingState}", "Fact"."timeStamp", "Fact"."specialChar", "Fact"."maybeSomething", "Fact"."sometimesNothing", "Fact"."biteSize", "SubFact"."subFact", "Fact"."commonCol", "Fact"."aDateTime" from "Fact" left join "SubFact" on "SubFact"."factId" = "Fact"."indexId" """

                //this is horribly unstable, but it will work for now.
                match testingState with
                | PSQL _ ->
                    expected <- expected.Replace ("\"Fact\"", "\"public\".\"Fact\"")
                    expected <- expected.Replace ("\"SubFact\"", "\"public\".\"SubFact\"")
                | _ -> ()

                Expect.equal (Utilities.queryBase<Fact> testingState) (expected.Trim ()) "queryBase not generating the proper SQL"
            }

        let select () =
            constructSeqTest "Select" "Select" (fun _ -> Orm.selectAll<Fact> testingState None |> Orm.toResultSeq)

        // let asyncSelect () =
        //     constructTest
        //         "Select-Async"
        //         "Select-Async"
        //         (fun _ -> Orm.selectAll< Fact > testingState None)

        let selectLimit () =
            constructScalarTest "SelectLimit" "SelectLimit" (fun _ -> Orm.selectLimit<Fact> testingState None 5 |> Orm.toResultSeq)

        let selectWhere () =
            constructScalarTest
                "SelectWhere"
                "SelectWhere"
                (fun _ ->
                    Orm.selectWhere<Fact> testingState None ("\"maybeSomething\" = ':1'", [| "true" |])
                    |> Orm.toResultSeq
                )

        let selectWhereWithIn () =
            constructScalarTest
                "SelectWhereWithIn"
                "SelectWhereWithIn"
                (fun _ ->
                    Orm.selectWhere<Fact>
                        testingState
                        None
                        ("""("id" in (:1) and "maybeSomething" = ':2') or "indexId" in (:3)""",
                        [| [ testGuid1 ; testGuid2 ; testGuid3 ] ; "false" ; [ 1.4 ; 2.2 ; 3.5 ] |])
                    |> Orm.toResultSeq
                )

        let selectWhereWithInFailure () =
            test "SelectWhereWithInFailure" {
                Expect.wantError
                    (Orm.selectWhere<Fact>
                        testingState
                        None
                        ("""("id" in (:1) and "maybeSomething" = ':2') or "indexId" in (:3)""",
                         [|
                            [ testGuid1 ; testGuid2 ; testGuid3 ]
                            "false"
                            [ Fact.init () ; Fact.init () ; Fact.init () ]
                         |])
                     |> Orm.toResultSeq)
                    "SelectWhereWithInFailure"
                |> ignore
            }

        let selectFirstWhere () =
            test "SelectFirstWhere Returns One" {
                Expect.isOk
                    (Orm.selectFirstWhere<Fact>
                        testingState
                        None
                        (""" "id" = ':1' """, [testGuid1])
                    )
                    "SelectFirstWhere did not return the single expected result"
                |> ignore
            }

        let selectFirstWhereFailure () =
            test "SelectFirstWhere Returns None (Fails)" {
                Expect.isError
                    (Orm.selectFirstWhere<Fact>
                        testingState
                        None
                        (""" "id" = ':1' """, [System.Guid.NewGuid()])
                    )
                    "SelectFirstWhere returned a result when none was expected"
                |> ignore
            }
        
        let toResultFuncs () =
            test "Seq,List,Array helpers" {
                Expect.isOk
                    (seq { Ok 1 }
                    |> Orm.toResultSeq)
                    "toResultSeq fails"
                Expect.isOk
                    (seq { Ok 1 }
                    |> Orm.toResultList)
                    "toResultList fails"
                Expect.isOk
                    (seq { Ok 1 }
                    |> Orm.toResultArray)
                    "toResultArray fails"
            }

        let update () =
            constructScalarTest
                "Update"
                "Update"
                (fun _ ->
                    let initial = { Fact.init () with id = testGuid1 }
                    let changed = { initial with name = "Evan Towlett" }
                    Orm.update<Fact> testingState None changed
                )

        let updateMany () =
            test $"{prefix} UpdateMany" {
                let initial = Fact.init ()
                
                //most sql distributions seem to have a lower precision than .net, we truncate here so we don't get an erroneous failure. 
                let dateTime = System.DateTime.Parse(initial.aDateTime.ToString("yyyy/MM/dd HH:mm:ss")) 

                let changed =
                    { initial with
                        name = "Evan Mowlett"
                        id = testGuid3
                        aSubFact = None
                        aDateTime = dateTime
                    }

                let changed2 =
                    { initial with
                        name = "Mac Flibby"
                        id = testGuid2
                        aSubFact = None
                        aDateTime = dateTime
                    }

                // printfn "ids: %A" [ testGuid2 ; testGuid3 ]
                Orm.updateMany<Fact> testingState None [ changed ; changed2 ] |> printf "%A"

                let evan =
                    Orm.selectWhere<Fact> testingState None ("id = ':1'", [| testGuid3 |])
                    |> Orm.toResultSeq
                    |> Result.map Seq.head

                let mac =
                    Orm.selectWhere<Fact> testingState None ("id = ':1'", [| testGuid2 |])
                    |> Orm.toResultSeq
                    |> Result.map Seq.head


                Expect.equal mac (Ok changed2 ) "update not persisted"
                Expect.equal evan (Ok changed ) "update not persisted"

            }



        let updateWhere () =
            constructScalarTest
                "UpdateWhere"
                "UpdateWhere"
                (fun _ ->
                    let initial = Fact.init ()
                    let changed = { initial with name = "Evan Howlett" }
                    Orm.updateWhere<Fact> testingState None ("\"indexId\" = :1", [| "1" |]) changed
                )

        let delete () =
            constructScalarTest
                "Delete"
                "Delete"
                (fun _ ->
                    let initial = Fact.init ()
                    let changed = { initial with name = "Evan Howlett" }
                    Orm.delete<Fact> testingState None changed
                )

        let deleteWhere () =
            constructScalarTest
                "DeleteWhere"
                "DeleteWhere"
                (fun _ ->
                    Orm.deleteWhere<Fact> testingState None ("\"indexId\" = :1", [| "1" |])
                )

        let deleteMany () =
            constructScalarTest
                "DeleteMany"
                "DeleteMany"
                (fun _ ->
                    let initial = Fact.init ()

                    let changed =
                        { initial with
                            name = "Evan Mowlett"
                            id = testGuid3
                        }

                    let changed2 =
                        { initial with
                            name = "Mac Flibby"
                            id = testGuid2
                        }

                    Orm.deleteMany<Fact> testingState None [ changed ; changed2 ] |> Orm.toResultSeq
                )

        let reader () =
            constructScalarTest
                "Reader"
                "Reader"
                (fun _ ->
                    let qb = Utilities.queryBase<Fact> testingState 
                    
                    Orm.consumeReader<Fact> testingState
                    |> fun reader ->
                        Orm.executeWithReader testingState None $"""select {qb}""" reader
                    |> Seq.head
                )

        let readerByJoinFailure () =
            constructFailureTest
                "ReaderByJoinFailure"
                "ReaderByJoinFailure"
                (fun _ ->
                    Orm.consumeReader<Fact> testingState
                    |> fun reader ->
                        Orm.executeWithReader testingState None "select * from \"Fact\"" reader
                        |> Orm.toResultSeq
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
            constructScalarTest
                "Teardown"
                "Teardown"
                (fun _ ->
                    transaction
                    |> Option.map (Orm.commitTransaction)
                    |> function
                        | Some o -> Result.Error ""
                        | None -> Ok ()
                )

        testSequenced
        <| testList
            "Base ORM tests"
            [
                connect ()
                connectionClosesAfterConsumption ()
                setup ()
                testSequenced
                <| testList
                    "Tests"
                    [
                        insert ()
                        insertMany ()
                        // // asyncInsertMany ()
                        queryBase ()
                        select ()
                        // asyncSelect ()
                        selectLimit ()
                        selectWhere ()
                        selectWhereWithIn ()
                        selectWhereWithInFailure ()
                        selectFirstWhere ()
                        toResultFuncs ()
                        update ()
                        updateMany ()
                        updateWhere ()
                        reader ()
                        delete ()
                        deleteWhere ()
                        deleteMany ()
                    ]
                tearDown ()
            ]



    let transaction testingState =
        let tableName = "\"Fact\""
        let testGuid1 = System.Guid.NewGuid().ToString ()
        let testGuid2 = System.Guid.NewGuid().ToString ()
        let testGuid3 = System.Guid.NewGuid().ToString ()
        let testGuid4 = System.Guid.NewGuid().ToString ()

        let sleep () = System.Threading.Thread.Sleep (500)

        let commit transaction x =
            Orm.tryCommit transaction |> ignore
            x

        let setup () =
            constructScalarTest
                ""
                ""
                (fun _ ->
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
                                \"biteSize\" text,
                                \"commonCol\" text,
                                \"aDateTime\" {datetimeType testingState} null
                            );
                            CREATE TABLE \"SubFact\" (
                                \"factId\" {intType testingState} not null,
                                \"subFact\" text not null,
                                \"commonCol\" text
                            );
                            "

                    Orm.execute testingState None createTable
                )

        let insertSelect () =
            constructScalarTest
                "InsertSelect"
                "InsertSelect"
                (fun _ ->
                    let transaction = Orm.beginTransaction testingState
                    let theFact = { Fact.init () with aSubFact = None }
                    let mutable theBackFact = Fact.init ()

                    Orm.insert<Fact> testingState transaction true (theFact)
                    |> Result.bind (fun _ ->
                        // printfn "We have inserted"

                        Orm.selectWhere<Fact> testingState transaction ("id = ':1'", [| theFact.id |])
                        |> Orm.toResultSeq
                        |> fun x ->
                            // printfn "We have the facts: %A" x
                            x
                        |> function
                            | Ok facts when Seq.length facts > 0 ->
                                theBackFact <- Seq.head facts
                                Ok facts
                            | Result.Error e -> Result.Error e
                            | _ -> Result.Error (exn "No data returned by select, you forgot the facts!")
                    )
                    |> Result.map (fun _ -> Orm.commitTransaction transaction)
                    |> Result.mapError (fun _ -> Orm.rollbackTransaction transaction)
                    |> function
                        | Ok _ ->
                            if theFact = theBackFact then
                                Ok ()
                            else
                                Result.Error (sprintf "%A %A %A" testingState theFact theBackFact)
                        | Result.Error error -> Result.Error (sprintf "%A %A" testingState (error.ToString ()))
                )

        let insertDeleteSelect () =
            constructScalarTest
                "InsertDeleteSelect"
                "InsertDeleteSelect"
                (fun _ ->
                    let transaction = Orm.beginTransaction testingState
                    let theFact = Fact.init ()
                    let mutable theBackFact = Fact.init ()
                    let err = exn "No data returned by select, you forgot the facts!"
                    // Orm.insert< SubFact > testingState true ({factId = theFact.indexId; subFact = "woooo"}) transaction |> ignore
                    Orm.insert<Fact> testingState transaction true (theFact)
                    |> Result.bind (fun _ -> Orm.delete<Fact> testingState transaction theFact)
                    |> Result.bind (fun _ ->
                        Orm.selectWhere<Fact> testingState transaction ("id = ':1'", [| theFact.id |])
                        |> Orm.toResultSeq
                        |> function
                            | Ok facts when Seq.length facts > 0 ->
                                theBackFact <- Seq.head facts
                                Ok facts
                            | Result.Error e -> Result.Error e
                            | _ -> Result.Error err
                    )
                    |> commit transaction
                    |> function
                        | Ok _ -> Result.Error (sprintf "%A %A" theFact theBackFact)
                        | Result.Error error ->
                            if err = error then
                                Ok ()
                            else
                                Result.Error (error.ToString ())
                )

        let insertMany () =
            constructScalarTest
                "InsertMany"
                "Inserted many facts."
                (fun _ ->
                    let transaction = Orm.beginTransaction testingState
                    let str8Facts =
                        [
                            { Fact.init () with id = testGuid1 }
                            { Fact.init () with
                                id = testGuid2
                                sometimesNothing = None
                            }
                            { Fact.init () with id = testGuid3 }
                            Fact.init ()
                        ]

                    let i = Orm.insertMany<Fact> testingState transaction true (str8Facts) |> Orm.toResultSeq
                    transaction |> Orm.commitTransaction |> ignore
                    i
                )
        let updateMany () =
            test $"{testingState} UpdateMany" {
                let transaction = Orm.beginTransaction testingState
                let initial = Fact.init ()
                
                //most sql distributions seem to have a lower precision than .net, we truncate here so we don't get an erroneous failure. 
                let dateTime = System.DateTime.Parse(initial.aDateTime.ToString("yyyy/MM/dd HH:mm:ss")) 

                let changed =
                    { initial with
                        name = "Evan Mowlett"
                        id = testGuid3
                        aSubFact = None
                        aDateTime = dateTime
                    }

                let changed2 =
                    { initial with
                        name = "Mac Flibby"
                        id = testGuid2
                        aSubFact = None
                        aDateTime = dateTime
                    }

                // printfn "ids: %A" [ testGuid2 ; testGuid3 ]
                Orm.updateMany<Fact> testingState transaction [ changed ; changed2 ] |> printf "\n\n\n\n\ntransactional update many: %A\n\n\n\n\n"

                let evan =
                    Orm.selectWhere<Fact> testingState transaction ("id = ':1'", [| testGuid3 |])
                    |> Orm.toResultSeq
                    |> Result.map Seq.head

                let mac =
                    Orm.selectWhere<Fact> testingState transaction ("id = ':1'", [| testGuid2 |])
                    |> Orm.toResultSeq
                    |> Result.map Seq.head

                transaction |> Orm.commitTransaction |> ignore
                Expect.equal mac (Ok changed2 ) "update not persisted"
                Expect.equal evan (Ok changed ) "update not persisted"

            }
        let insertUpdateSelect () =
            constructSeqTest
                "InsertUpdateSelect"
                "InsertUpdateSelect"
                (fun _ ->
                    let transaction = Orm.beginTransaction testingState
                    let theFact = Fact.init ()

                    let theNewFact =
                        { theFact with
                            name = "All Facts, All the Time"
                            aSubFact = None
                        }

                    let mutable theBackFact = Fact.init ()
                    let err = exn "No data returned by select, you forgot the facts!"

                    Orm.insert<Fact> testingState transaction true (theFact)
                    |> Result.bind (fun _ -> Orm.update<Fact> testingState transaction theNewFact)
                    |> Result.bind (fun _ ->
                        Orm.selectWhere<Fact> testingState transaction ("id = ':1'", [| theFact.id |])
                        |> Orm.toResultSeq
                        |> function
                            | Ok facts when Seq.length facts > 0 ->
                                theBackFact <- Seq.head facts
                                Ok facts
                            | Result.Error e -> Result.Error e
                            | _ -> Result.Error err
                    )
                    |> commit transaction
                    |> function
                        | Ok facts ->
                            if theNewFact = theBackFact then
                                Ok (sprintf "You remembered the facts: %A - %A | %A" theFact theBackFact facts)
                            else
                                Result.Error (sprintf "Look at all these facts: %A - %A | %A" theFact theBackFact facts)
                        | Result.Error error -> Result.Error (error.ToString ())
                )

        let readerWithTransaction () =
            constructScalarTest
                "Reader-Transaction"
                "Reader with Transaction"
                (fun _ ->
                    let qb = Utilities.queryBase<Fact> testingState 
                    let transaction = Orm.beginTransaction testingState

                    Orm.consumeReader<Fact> testingState
                    |> fun reader -> Orm.executeWithReader testingState transaction $"select {qb}" reader
                    |> Orm.toResultSeq
                    |> commit transaction
                )
        
        let deleteManyWithTransaction () =
            constructScalarTest
                "DeleteMany"
                "DeleteMany"
                (fun _ ->
                    let transaction = Orm.beginTransaction testingState
                    let initial = Fact.init ()

                    let changed =
                        { initial with
                            name = "Evan Mowlett"
                            id = testGuid3
                        }

                    let changed2 =
                        { initial with
                            name = "Mac Flibby"
                            id = testGuid2
                        }

                    let d = Orm.deleteMany<Fact> testingState transaction [ changed ; changed2 ] |> Orm.toResultSeq
                    commit transaction |> ignore
                    d
                )

        testSequenced
        <| testList
            "Base ORM tests"
            [
                setup ()
                testSequenced
                <| testList
                    "Tests"
                    [
                        insertSelect ()
                        insertDeleteSelect ()
                        insertMany ()
                        updateMany ()
                        insertUpdateSelect ()
                        readerWithTransaction ()
                        deleteManyWithTransaction ()
                    ]
            ]


    [<EntryPoint>]
    let main argv =
        System.IO.File.ReadAllLines ("../.env")
        |> Array.iter (fun line ->
            let chunks = line.Split ("=")
            let variable = chunks[0]
            let value = System.String.Join ("=", chunks[1..])
            // printfn "%A %A" variable value
            System.Environment.SetEnvironmentVariable (variable, value)
        )


        let psqlConnectionString =
            System.Environment.GetEnvironmentVariable ("postgres_connection_string")

        let odbcConnectionString =
            System.Environment.GetEnvironmentVariable ("odbc_connection_string")

        let mysqlConnectionString = ""
        let mssqlConnectionString = ""

        let sqliteConnectionString =
            System.Environment.GetEnvironmentVariable ("sqlite_connection_string")

        let psqlState = PSQL (psqlConnectionString, Contexts.PSQL)
        let mysqlState = MySQL (mysqlConnectionString, Contexts.MySQL)
        let mssqlState = MSSQL (mssqlConnectionString, Contexts.MSSQL)
        let sqliteState = SQLite (sqliteConnectionString, Contexts.SQLite)
        let odbcState = ODBC (odbcConnectionString, Contexts.ODBC)

        let states =
            [
                // odbcState
                // psqlState
                sqliteState
            // ; mysqlState
            // ; mssqlstate
            ]

        let expectoArgs = argv |> Array.skip 1 
        if Array.length argv = 0 
        then 
            states |> List.map (orm >> runTestsWithCLIArgs [] expectoArgs) 
        else 
            let first = argv[0].ToLowerInvariant()
            match first with 
            | "-a" -> 
                states |> List.map (orm >> runTestsWithCLIArgs [] expectoArgs) 
                |> ignore
                states
                |> List.map (
                    transaction
                    >> runTestsWithCLIArgs [] expectoArgs
                )
            | "-t" -> 
                states
                |> List.map (
                    transaction
                    >> runTestsWithCLIArgs [] expectoArgs
                )
            | "-i" -> 
                states |> List.map (orm >> runTestsWithCLIArgs [] expectoArgs) 
        |> ignore
        
        0
