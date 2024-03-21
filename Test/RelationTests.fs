namespace HCRD.FORM.Tests

module RelationTests =
    open HCRD.FORM.Tests.Utilities
    open Form
    open Setup
    open Expecto
    
    let relationEvaluteAndNestedAreEqual testingState = 
        constructTest 
            "RelationEvaluteAndNestedAreEqual" 
            "" 
            ( fun _ -> 
                Orm.selectWhere<Fact> testingState None ( "\"maybeSomething\" = ':1'", [|"true"|])
                |> Result.toResultSeq
                |> function 
                | Ok res ->
                    let fact = Seq.head res  
                    Relation.evaluate fact.subFact None fact 
                    |> Result.toResultSeq 
                    |> function 
                    | Ok subRes ->
                        let subfact = Seq.head subRes
                        
                        fact.subFact.Value
                        |> Option.get
                        |> Result.toResultSeq
                        |> function 
                        | Ok nestedSubFacts ->
                            Seq.head nestedSubFacts
                            |> fun x -> if x = subfact then Ok () else Error (exn $"subfact from fact {x} and subfact from evaluate {subfact} not equal.")
                        | Error e -> Error e 
                    | Error e -> Error e 
                | Error e -> Error e
            )
