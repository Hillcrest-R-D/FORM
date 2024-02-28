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
        let parent = typeof< ^P >
        let child = typeof< ^C >
        member private this.evaluate (transaction : DbTransaction option) : Result< ^C, exn> seq option = 
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
                |> Seq.map ( fun column -> column.parent.property.GetValue(instance) )
            
            value <- Some <| (Orm.selectWhere< ^C > state transaction ( where, parameters ))
            value

        member _.Value = value 
        member this.Evaluate = this.evaluate

    (* For ^T Seq *)
[<Table("Fact", Contexts.PSQL)>]
[<Join(typeof<SubFact>, Left)>]
[<Join(typeof<OtherFact>, Inner)>]
type Fact =
    {
        id: int64
        [<On(typeof<SubFact>, 1, 1, JoinDirection.Left, propertyInfo<SubFact.subFactId1>, Contexts.PSQL)>]
        subFactId1 : int64
        [<On(typeof<SubFact>, 1, 2, JoinDirection.Left, propertyInfo<SubFact.subFactId2>, Contexts.PSQL)>]
        subFactId2 : string
        [<ByJoin(typeof<SubFact>, Contexts.PSQL)>]
        subFact : string option
        [<On(typeof<OtherFact>, 2, 1, JoinDirection.Inner, propertyInfo<Other.otherFactId1>, Contexts.PSQL)>]
        otherFactId1 : DateTime
        (* This could SIGNIFICANTLY complicate the attributes and the join logic *)
        [<ByJoin(typeof<SubFact>, EvaluationStrategy.Lazy, Contexts.PSQL)>]
        otherFact : OtherFact seq option
    }
