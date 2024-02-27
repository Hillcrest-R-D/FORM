namespace Form

module Relation =     
    open Utilities
    open System
    // open Orm
    open Form.Attributes
    let inline lookupId<^C> state =
        columnMapping<^C> state
        |> Seq.filter (fun col -> col.IsKey)
        
    (*
        The type argument is that of the type that needs to be looked up.
        Do we need to be able to reference the type that Relation is declared on?
    *)
    type Relation< ^P, ^C > (keyId, eval, instance, state) as self =
        let mutable value : Result<^C seq, exn> option = None
        let state : OrmState = state
        let evaluationStrategy : EvaluationStrategy = eval
        let keyId : int = keyId
        let instance : ^P = instance

        do
            self.value <- 
                match self.evaluationStategy with
                | Strict -> self.evaluate ()
                | Lazy -> None
            

        member private this.evaluate () = 
            let id = lookupId<^C> state
            let idValueSeq = 
                RelationshipCell.fold ( 
                    fun acc item -> 
                        Seq.append acc <| seq { sqlWrap item } 
                    ) 
                    Seq.empty 
                    inst.id
                |> Seq.rev

            let whereClause = 
                Seq.zip id idValueSeq
                |> Seq.map (fun (keyCol, value) -> $"{keyCol} = {value}") 
                |> String.concat " and " 

            log ( fun _ -> 
                sprintfn "lookupId Id Column Name: %A" id 
                + sprintf "Where Clause: %A" whereClause 
            )
            if Seq.isEmpty id then {inst with value = None} 
            else 
                Orm.selectWhere<^C> state None whereClause
                |> Orm.toResultSeq 
                |> Result.bind (fun vals -> 
                    this.value <- Ok vals 
                    Ok vals
                ) 
                    
        member this.Value =
            match this.value with 
            | None -> this.evaluate ()
            | Some thing -> thing

        member this.Evaluate = this.evaluate 

    // type MyOtherRecord =
    //     {
    //         [<PrimaryKey>]
    //         otherFactId : str 
    //     }
        
    // type MyRecord = 
    //     {  
    //         [<On(typeof<MyOtherRecord>, 2, 1, JoinDirection.Inner, propertyInfo<Other.otherFactId1>, Contexts.PSQL)>]
    //         otherFactId1 : str
    //         [<RelationParams(EvaluationStrategy.Strict, Context.PSQL, state)>]
    //         otherFact : Relation<MyOtherRecord>
    //     }
    