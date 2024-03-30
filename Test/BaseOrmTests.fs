namespace HCRD.FORM.Tests

module BaseOrmTests =
    open HCRD.FORM.Tests.Utilities
    open Form
    open Setup
    open Expecto

    let connect testingState = 
        constructTest "Connect" "Successfully connected." ( fun _ -> Orm.connect testingState )

    let insert testingState =
        constructTest "Insert" "Fact inserted." ( fun _ -> Orm.insert< Fact > testingState None true ( Fact.init() ) ) 
            
    let insertMany testingState =
        constructTest 
            "InsertMany" 
            "Inserted many facts."
            ( fun _ ->
                let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
                Orm.insertMany< Fact > testingState None true ( str8Facts )
            )

    let insertAlot testingState =
        constructTest 
            "InsertManyMany" 
            "Inserted many facts."
            ( fun _ ->
                let str8Facts = [ for _ in 1..10000 do Fact.init() ]
                Orm.insertMany< Fact > testingState None true ( str8Facts )
            )
                
    // let asyncInsertMany testingState =
    //     constructTest
    //         "InsertMany-Async"
    //         "Inserted many facts asynchronously."
    //         (fun _ -> 
    //             let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
    //             Orm.insertMany< Fact > testingState None true ( str8Facts )
    //         )

    let select testingState =
        constructTest 
            "Select" 
            "Select"
            ( fun _ -> 
                printfn "%A" <| Form.Utilities.queryBase<Fact> testingState
                Orm.selectAll< Fact > testingState None |> Result.toResultSeq ) 
            
    // let asyncSelect testingState =
    //     constructTest
    //         "Select-Async"
    //         "Select-Async"
    //         (fun _ -> Orm.selectAll< Fact > testingState None)  

    let selectLimit testingState =
        constructTest 
            "SelectLimit"
            "SelectLimit"
            (fun _ -> Orm.selectLimit< Fact > testingState None 5 |> Result.toResultSeq)

    let selectBigLimit testingState =
        constructTest 
            "SelectBigLimit"
            "SelectBigLimit"
            (fun _ -> Orm.selectLimit< Fact > testingState None 10000 |> Result.toResultSeq)

    let selectWhere testingState =
        constructTest 
            "SelectWhere"
            "SelectWhere"
            (fun _ -> 
                let results = Orm.selectWhere< Fact > testingState None ( "\"maybeSomething\" = ':1'", [| "true" |]) |> Result.toResultSeq 
                match results with
                | Ok res -> 
                    let factResult = res |> Seq.head 
                    factResult
                    |> fun x -> (Relation.evaluate x.subFact None x) 
                    |> Result.toResultSeq
                    |> function 
                    | Ok sf -> 
                        let subfactResult = sf |> Seq.head 
                        factResult.subFact.Value
                        |> function 
                        | Some subFactValueResultSeqFromFact ->
                            let subFactValueResultFromFact = Seq.head subFactValueResultSeqFromFact
                            if subFactValueResultFromFact = Ok subfactResult then Ok ()
                            else Error (exn $"subfacts not equal")
                        | None -> Error (exn $"subfact obtained from evaluate but no mutation on higher type occured")
                    | Error e -> Error (exn "subfact not evaluated")
                | Error e -> Error e         
            )


    let selectWhereWithIn testingState =
        constructTest 
            "SelectWhereWithIn"
            "SelectWhereWithIn"
            (fun _ -> Orm.selectWhere< Fact > testingState None ( """("id" in (:1) and "maybeSomething" = ':2') or "indexId" in (:3)""", [| [ testGuid1; testGuid2; testGuid3 ]; "false"; [ 1.4; 2.2; 3.5 ] |]) |> Result.toResultSeq )

    let selectWhereWithInFailure testingState =
        test "SelectWhereWithInFailure" {
            Expect.wantError (
                Orm.selectWhere< Fact > testingState None ( """("id" in (:1) and "maybeSomething" = ':2') or "indexId" in (:3)""", [| [ testGuid1; testGuid2; testGuid3 ]; "false"; [ Fact.init(); Fact.init(); Fact.init() ] |]) 
                |> Result.toResultSeq ) "SelectWhereWithInFailure" 
            |> ignore
        }
    let update testingState =
        constructTest 
            "Update"
            "Update"
            (fun _ ->
                let initial = { Fact.init() with id = testGuid1 }
                let changed = { initial with name = "Evan Towlett"}
                Orm.update< Fact > testingState None changed
            )

    let updateMany testingState =
        constructTest 
            "UpdateMany"
            "UpdateMany"
            ( fun _ -> 
                let initial = Fact.init() 
                // let str8Facts = [{ Fact.init() with id = testGuid1}; { Fact.init() with id = testGuid2; sometimesNothing = None }; { Fact.init() with id = testGuid3}; Fact.init()]
                // Orm.insertMany< Fact > testingState None true ( str8Facts )
                // |> printfn "insert %A"
                let changed = { initial with name = "Evan Mowlett"; id = testGuid3 ; subFact= Form.Utilities.Relation<Fact, SubFact>(1,testingState)}
                let changed2 = { initial with name = "Mac Flibby"; id = testGuid2; subFact = Form.Utilities.Relation<Fact, SubFact>(1,testingState)}
                printfn "ids: %A" [testGuid2; testGuid3]
                Orm.updateMany< Fact > testingState None [changed;changed2]  |> printf "%A"

                let evan = Orm.selectWhere<Fact> testingState None ( "id = ':1'", [| testGuid3 |] ) |> Result.toResultSeq
                let mac = Orm.selectWhere<Fact> testingState None ( "id = ':1'", [| testGuid2 |] ) |> Result.toResultSeq
                printfn "evan: %A" evan 
                printfn "mac: %A" mac
                match evan, mac with 
                | Ok e, Ok m -> 
                    let headE = Seq.head e 
                    let headM = Seq.head m 
                    printfn "\n%A = %A\n\n\n%A = %A" headE changed headM changed2
                    if headE = changed && headM = changed2 
                    then Ok ()
                    else Result.Error "Update not applied."
                | Result.Error ex, _ 
                | _, Result.Error ex -> Result.Error ex.Message
                
            )



    let updateWhere testingState =
        constructTest
            "UpdateWhere"
            "UpdateWhere"
            (fun _ -> 
                let initial = Fact.init () 
                let changed = { initial with name = "Evan Howlett"}
                Orm.updateWhere< Fact > testingState None ( "\"indexId\" = :1", [| "1" |] ) changed 
            )

    let delete testingState =
        constructTest 
            "Delete"
            "Delete"
            ( fun _ ->
                let initial = Fact.init () 
                let changed = { initial with name = "Evan Howlett"}
                Orm.delete< Fact > testingState None changed
            )
    let deleteWhere testingState = 
        constructTest
            "DeleteWhere"
            "DeleteWhere"
            (fun _ -> Orm.deleteWhere< Fact > testingState None ( "\"indexId\" <> :1", [| "1" |] ) )
            
    let deleteMany testingState =
        constructTest
            "DeleteMany"
            "DeleteMany"
            (fun _ ->
                let initial = Fact.init() 
                let changed = { initial with name = "Evan Mowlett"; id = testGuid3}
                let changed2 = { initial with name = "Mac Flibby"; id = testGuid2}
                Orm.deleteMany< Fact > testingState None [changed;changed2] 
            )
    let reader testingState =
        constructTest
            "Reader"
            "Reader"
            (fun _ ->
                let query = $"select {Utilities.queryBase<Fact> testingState}"
                Orm.consumeReader<Fact> testingState 
                |> fun reader -> Orm.executeWithReader testingState None query reader |> Result.toResultSeq
            )