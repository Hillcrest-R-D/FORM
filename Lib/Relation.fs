namespace Form

module Relation =     
    open Utilities
    open System
    let inline lookupId<^S> state =
        columnMapping<^S> state
        |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^S > state  ) //! Filter out joins for non-select queries
        |> Array.filter (fun col -> col.IsKey) 
        |> Array.map (fun keyCol -> keyCol.QuotedSqlName)

    type SqlValueDescriptor = 
        {
            _type : Type
            value : obj
        }
    
    let inline sqlWrap (item : SqlValueDescriptor) : string =
        if item._type = typedefof<string> 
        then $"'{item.value}'"
        else $"{item.value}"
    
    type RelationshipCell = 
        | Leaf of SqlValueDescriptor
        | Node of SqlValueDescriptor * RelationshipCell
    module RelationshipCell = 
        let rec fold ( f : 'a -> SqlValueDescriptor -> 'a ) ( acc : 'a ) state =  
            match state with 
            | Leaf l -> f acc l
            | Node ( l, n ) -> f ( fold f acc n ) l
       
    type Relation<^S> =
        {
            id : RelationshipCell 
            // Relation<Fact> {id = Node ( {_type = typeof<int>; value = 0 }, Leaf { _type= typeof<string>; value = "42" } ); None}
            value : ^S option    
        }
        static member inline Value (inst) state =
            let id = lookupId<^S> state
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
                printfn "lookupId Id Column Name: %A" id 
                printfn "Where Clause: %A" whereClause 
            )
            if Seq.isEmpty id then {inst with value = None} 
            else 
                selectWhere<^S> state None whereClause 
                |> function 
                | Ok vals when Seq.length vals > 0 ->
                    Some <| Seq.head vals    
                | _ -> 
                    Option.None 
                |> fun (v : option<'S>) -> { inst with value = v}
