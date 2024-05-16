namespace Form.Attributes.Test
open Form.Attributes

type IRelation = interface end

module Utilities =
    type Contexts =
        | Default = 1
        | Secondary = 2
        | Tertiary = 4
    let dummbyState = PSQL("", Contexts.Default)

    let inline sqlQuote ( state : OrmState ) str  =
        match state with 
        | MSSQL _ -> $"[{str}]"
        | MySQL _ -> $"`{str}`"
        | PSQL _ 
        | SQLite _ 
        | ODBC _ -> $"\"{str}\""
        
    let inline context< ^T > ( state : OrmState ) = 
        match state with 
        | MSSQL     ( _, c ) -> c 
        | MySQL     ( _, c ) -> c 
        | PSQL      ( _, c ) -> c 
        | SQLite    ( _, c ) -> c 
        | ODBC      ( _, c ) -> c 

    let inline attrFold ( attrs : DbAttribute array ) ( ctx : System.Enum ) = 
        Array.fold ( fun s ( x : DbAttribute ) ->  
                if snd x.Value = ( ctx ) 
                then fst x.Value
                else s
            ) "" attrs

    let inline attrJoinFold ( attrs : OnAttribute array ) ( ctx : System.Enum ) = 
        Array.fold ( fun s ( x : OnAttribute ) ->  
                if snd x.Value = ( ctx ) 
                then (fst x.Value, x.fieldName)
                else s
            ) ("", "") attrs 
    
    let inline tableName< ^T > ( state : OrmState ) = 
        let reifiedType = typeof< ^T >
        let attrs =
            typedefof< ^T >.GetCustomAttributes( typeof< TableAttribute >, false )
            |> Array.map ( fun x -> x :?> DbAttribute )
        
        if attrs = Array.empty 
        then typedefof< ^T >.Name
        else attrFold attrs ( context< ^T > state )
        |> fun x -> x.Split( "." )
        |> Array.map ( fun x -> sqlQuote state x )
        |> String.concat "."
        

    
    let inline mappingHelper< ^T, ^A when ^A :> DbAttribute> state (propertyInfo : System.Reflection.PropertyInfo) = 
        printfn "Trying MH..."
        propertyInfo.GetCustomAttributes( typeof< ^A >, false ) 
        |> Array.map ( fun y -> y :?> DbAttribute )
        |> fun y -> attrFold y ( context< ^T > state )   
    
    let compareContextsAsEnums (ctx1 : System.Enum) (ctx2 : obj) =
        printfn "%A = %A => %A" ctx1 ctx2 (ctx1 = ((ctx2 :?> DbContext :> System.Enum)))
        printfn "%A = %A => %A" (ctx1.GetType()) (ctx2.GetType()) ((ctx1.GetType()) = (ctx2.GetType()))
        printfn "%A" (ctx2.GetType())
        ctx1 = ((ctx2 :?> DbContext :> System.Enum))
        && (ctx1.GetType()) = (ctx2.GetType())

    let checkContextAgainstState (state : OrmState) (ctx : obj) =
        compareContextsAsEnums (context state) ctx 

    let inline getAttribute<^T,^A when ^A :> DbAttribute> state (propertyInfo : System.Reflection.PropertyInfo) =
        let filter = checkContextAgainstState state
        propertyInfo.GetCustomAttributes( typeof< ^A >, false )
        |> Array.map ( fun attr -> attr :?> 'A )
        // |> Array.filter ( fun attr -> filter attr.Context )
        |> Array.tryHead

    let inline columnMapping< ^T > ( state : OrmState ) = 
        let reifiedType = typeof< ^T >
        let mutable outMapping = Array.empty

        Microsoft.FSharp.Reflection.FSharpType.GetRecordFields typedefof< ^T > 
        |> Array.mapi ( fun i x -> 
            let primaryKey : option<PrimaryKeyMember> = 
                printfn "PrimaryKeyAttribute"
                getAttribute<^T, PrimaryKeyAttribute> state x 
                |> Option.map( fun attr ->
                    printfn "%A" attr.ContextType
                    { 
                        name = fst attr.Value
                        order = attr.Order
                    } : PrimaryKeyMember
                )
            let source =
                printfn "ByjoinAttribute"
                let tmp = mappingHelper< ^T, ByJoinAttribute > state x
                if tmp = "" then tableName< ^T > state else sqlQuote state tmp
            let sqlName = 
                printfn "ColumnAttribute"
                let tmp = mappingHelper< ^T, ColumnAttribute > state x
                if tmp = "" then x.Name else tmp
            
                

            let joinery = 
                x.GetCustomAttributes( typeof< OnAttribute >, false ) 
                |> Array.map ( fun y -> y :?> OnAttribute )
                |> fun y -> attrJoinFold y ( context< ^T > state )  //attributes< ^T, ColumnAttribute> state
                |> fun (y : (string * string)) -> if y = ("", "") then None else Some y
            let isKey = (mappingHelper< ^T, PrimaryKeyAttribute > state x) <> ""
            { 
                Index = i
                PrimaryKey = primaryKey
                ForeignKey = None
                SqlIndex = None
                IsRelation = 
                    x.PropertyType.GetInterface(nameof(IRelation)) <> null
                IsLazilyEvaluated =
                    x.GetCustomAttributes( typeof< LazyEvaluationAttribute >, false ).Length > 0
                JoinOn = None
                Source = source
                QuotedSource = source
                SqlName = sqlName
                QuotedSqlName = sqlQuote state sqlName
                FSharpName = x.Name
                Type = x.PropertyType 
                PropertyInfo = x
            } 
        )