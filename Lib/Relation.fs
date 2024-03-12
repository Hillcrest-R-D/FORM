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
type Relation< ^P, ^C > (keyId : int, state : OrmState) =
    inherit BaseRelation<^P,^C>(keyId, state)
    let mutable value : Result<^C, exn> seq option = None
    let parent = typeof< ^P >
    let child = typeof< ^C >
    let context = 
        match state with 
        | MSSQL ( _ , ctx) 
        | PSQL ( _ , ctx) 
        | MySQL ( _ , ctx) 
        | ODBC ( _, ctx) 
        | SQLite ( _, ctx) -> ctx

    member this.Evaluate (transaction : DbTransaction option) ( instance : ^P ): Result<^C, exn> seq = 
        let columns = 
            parent.GetProperties() 
            |> Seq.filter( fun prop -> 
                if prop.IsDefined( typeof< OnAttribute > ) 
                then 
                    let attr = prop.GetCustomAttribute(typeof< OnAttribute >) :?> OnAttribute
                    snd attr.Value = ( context :?> DbContext |> EnumToValue ) && attr.key = keyId //if the key lines up AND the context.
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
            |> Seq.map ( fun column -> column.parent.property.GetValue(instance) )
        
        let tmp = (Orm.selectWhere< ^C > state transaction ( where, parameters )) //selectHelper< ^T > state transaction ( fun x -> $"select {x} where {escape (where, parameters)}" ) //
        value <- Some tmp
        tmp

    ///<summary>Returns the current state of the relation. None if the relation has not been evaluated yet, Some if the evaluation has been called.</summary>
    member _.State : Result<^C, exn > seq option = value
    
    ///<summary>Returns the current State as a Result&lt;^C seq, exn&gt;, calling an Evaluation if the state is currently none.</summary>
    ///<param name="transaction"></param>
    member this.Result transaction instance  : Result< ^C seq, exn > = 
        value
        |> function 
        | None -> this.Evaluate transaction instance
        | Some v -> v 
        |> Result.toResultSeq 
