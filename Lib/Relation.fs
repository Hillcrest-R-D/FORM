namespace rec Form

module Relation =     
    open Utilities
    open System
    open Form.Attributes
    open System.Reflection
    open Microsoft.FSharp.Reflection
    open System.Data.Common
        
    (*
        The type argument is that of the type that needs to be looked up.
        Do we need to be able to reference the type that Relation is declared on?
    *)
    

    type Relation< ^P, ^C > (keyId : int, evaluationStrategy : EvaluationStrategy, instance : ^P, context : int, state : OrmState) as self =
        let mutable value : Result<^C, exn> seq option = None
        let mutable instance = instance
        let parent = typeof< ^P >
        let child = typeof< ^C >
        member this.Evaluate (transaction : DbTransaction option) : Result<^C, exn> seq = 
            let columns = 
                parent.GetProperties() 
                |> Seq.filter( fun prop -> 
                    if prop.IsDefined( typeof< OnAttribute > ) 
                    then 
                        let attr = prop.GetCustomAttribute(typeof< OnAttribute >) :?> OnAttribute
                        snd attr.Value = context && attr.key = keyId //if the key lines up AND the context.
                    else false
                )
                |> Seq.map ( fun prop -> {| property = prop;  attribute = prop.GetCustomAttribute(typeof< OnAttribute >) :?> OnAttribute |} )
                |> Seq.sortBy ( fun column -> column.attribute.part )
                |> Seq.map ( fun column -> {| child = child.GetProperty(column.attribute.on); parent = column |} )
            
            let where = 
                String.Join( 
                    " and "
                    , columns |> Seq.mapi ( fun index column -> $"{column.child.Name} = :{index+1}" ) 
                )

            let parameters = 
                columns 
                |> Seq.map ( fun column -> 
                    column.parent.property.GetValue(instance) 
                )
            
            let tmp = (Orm.selectWhere< ^C > state transaction ( where, parameters ))
            value <- Some tmp
            tmp

        ///<summary>Returns the current state of the relation. None if the relation has not been evaluated yet, Some if the evaluation has been called.</summary>
        member _.State : Result<^C, exn > seq option = value
        
        ///<summary>Returns the current State as a Result&lt;^C seq, exn&gt;, calling an Evaluation if the state is currently none.</summary>
        ///<param name="transaction"></param>
        member this.Result transaction : Result< ^C seq, exn > = 
            value
            |> function 
            | None -> this.Evaluate transaction
            | Some v -> v 
            |> Form.Result.toResultSeq 
        
        member _.SetParentInstance i =
            instance <- i

    