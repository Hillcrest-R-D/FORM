namespace Form

module Relation =     
    open Utilities
    open System
    let inline lookupId<^S> state =
        columnMapping<^S> state
        |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^S > state  ) //! Filter out joins for non-select queries
        |> Array.filter (fun col -> col.IsKey) 
        |> Array.map (fun keyCol -> keyCol.QuotedSqlName)

    // type SqlValueDescriptor = 
    //     {
    //         _type : Type
    //         value : obj
    //     }
    
    // let inline sqlWrap (item : SqlValueDescriptor) : string =
    //     if item._type = typedefof<string> 
    //     then $"'{item.value}'"
    //     else $"{item.value}"
    
    // type RelationshipCell = 
    //     | Leaf of SqlValueDescriptor
    //     | Node of SqlValueDescriptor * RelationshipCell
        
    // module RelationshipCell = 
    //     let rec fold ( f : 'a -> SqlValueDescriptor -> 'a ) ( acc : 'a ) state =  
    //         match state with 
    //         | Leaf l -> f acc l
    //         | Node ( l, n ) -> f ( fold f acc n ) l
       
    type Relation< ^T > =
        {
            private mutable value : Result<^T seq, exn> option
            private state : OrmState
            private evaluationStrategy : EvaluationStrategy
        }

        private member this.evaluate () = 
            let id = lookupId<^T> state
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
                selectWhere<^T> state None whereClause
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

    module Relation = 
        let evaluate relationState = 
            let innerType = getInnerType<relationState>
            Orm.selectWhere<innerType> relationState


    let myFact = Orm.selectAll<Fact> state None |> Seq.head
    
    myFact.otherFact.Value
    |> function
    | Ok a-> printfn "%A" a
    
    let myTempFacts = Relation.evaluate fact.otherFact

    /// 
    Relation.evaluate fact.otherFact

    Relation.evaluate fact.otherFact
    => {fact with otherFact = evaluatedOtherFact}
    {
        id = 1
        otherFactId1 = "blue"
        otherFact = 
            Relation 
                [{
                    
                }]
    }
    // type JoinType =
    //     | Direct
    //     | Relation

    // [<Table("auth.User", Contexts.Something)>]
    // type User = 
    //     {
    //         id : int
    //         username : string
    //         [<Join(Direct)>]
    //         tenant : Tenant 
    //         [<Eager>]
    //         biography : Relation<Biography>
    //         artPortfolioId : Relation<ArtPortfolio>
    //     }

    // { user with biography = Relation.Value user.biography ormState }
    // user.biography.otherwiseHiddenOrmState => exception
    // match user.biography.Value with 
    // | Some bio
    // | None