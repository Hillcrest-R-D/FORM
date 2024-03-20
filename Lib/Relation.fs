namespace Form

open System
open Form.Attributes
open System.Reflection
open System.Data.Common
open Microsoft.FSharp.Core.LanguagePrimitives
open Form.Orm
open Form.Utilities
(*
    The type argument is that of the type that needs to be looked up.
    Do we need to be able to reference the type that Relation is declared on?
*)
module Relation =

    let inline evaluate<^P, ^C> (relation : Relation<^P, ^C>) (transaction : DbTransaction option) ( instance : ^P ): Result<^C, exn> seq = 
        let columns = 
            relation.parent.GetProperties() 
            |> Seq.filter( fun prop -> 
                if prop.IsDefined( typeof< OnAttribute > ) 
                then 
                    let attr = 
                        prop.GetCustomAttributes(typeof< OnAttribute >) 
                        |> Seq.map ( fun x -> x :?> OnAttribute) 
                        |> Seq.filter ( fun x -> snd x.Value = (relation.context) ) 
                        |> Seq.head
                    snd attr.Value = ( relation.context) && attr.key = relation.keyId //if the key lines up AND the context.
                else false
            )
            |> Seq.map ( fun prop -> {| property = prop;  attribute = prop.GetCustomAttributes(typeof< OnAttribute >) 
                        |> Seq.map ( fun x -> x :?> OnAttribute) 
                        |> Seq.filter ( fun x -> snd x.Value = (relation.context) ) 
                        |> Seq.head |} )
            |> Seq.sortBy ( fun column -> column.attribute.part )
            |> Seq.map ( fun column -> {| child = relation.child.GetProperty(column.attribute.fieldName); parent = column |} )
        
        let where = 
            String.Join( 
                " and "
                , columns |> Seq.mapi ( fun index column -> $"\"{column.child.Name}\" = :{index+1}" ) 
            )

        let parameters = 
            columns 
            |> Seq.map ( fun column -> column.parent.property.GetValue(instance) )
        
        let tmp = (Orm.selectWhere< ^C > relation.state transaction ( where, parameters )) //selectHelper< ^T > state transaction ( fun x -> $"select {x} where {escape (where, parameters)}" ) //
        relation.SetValue <| Some tmp
        tmp

    
    // ///<summary>Returns the current State as a Result&lt;^C seq, exn&gt;, calling an Evaluation if the state is currently none.</summary>
    // ///<param name="transaction"></param>
    // let result transaction instance  : Result< ^C seq, exn > = 
    //     value
    //     |> function 
    //     | None -> this.Evaluate transaction instance
    //     | Some v -> v 
    //     |> Result.toResultSeq 
