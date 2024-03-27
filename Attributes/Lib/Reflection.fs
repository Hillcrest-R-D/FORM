namespace Form.Attributes
open System.Collections.Generic
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Core.LanguagePrimitives
open System
open System.Reflection
open System.Data.Common
module Reflection = 

    type IRelation =
        interface end

    
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    // let _mappings = Dictionary<(Type * OrmState), SqlMapping []>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let _relation = Dictionary<Type, ConstructorInfo>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let _relationArguments = Dictionary<(Type * Type), int>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let _tableNames = Dictionary<Type * OrmState, string>()
    
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let _mappings = Dictionary<(Type * OrmState), SqlMapping []>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let _option = Dictionary<Type, Type option>()

    let inline sqlQuote ( state : OrmState ) str  =
        match state with 
        | MSSQL _ -> $"[{str}]"
        | MySQL _ -> $"`{str}`"
        | PSQL _ 
        | SQLite _ 
        | ODBC _ -> $"\"{str}\""

    let unpackContext =
        function 
        | MSSQL ( _ , ctx) | PSQL ( _ , ctx) | MySQL ( _ , ctx) | ODBC ( _, ctx) | SQLite ( _, ctx) -> ctx
    let inline context< ^T > ( state : OrmState ) = 
        match state with 
        | MSSQL     ( _, c ) -> c 
        | MySQL     ( _, c ) -> c 
        | PSQL      ( _, c ) -> c 
        | SQLite    ( _, c ) -> c 
        | ODBC      ( _, c ) -> c 

    let inline attrFold ( attrs : DbAttribute array ) ( ctx : Enum ) = 
        Array.fold ( fun s ( x : DbAttribute ) ->  
                if snd x.Value = ( ( box( ctx ) :?> DbContext ) |> EnumToValue ) 
                then fst x.Value
                else s
            ) "" attrs

    let inline attrJoinFold ( attrs : OnAttribute array ) ( ctx : Enum ) = 
        Array.fold ( fun s ( x : OnAttribute ) ->  
                if snd x.Value = ( ( box( ctx ) :?> DbContext ) |> EnumToValue ) 
                then (fst x.Value, x.fieldName)
                else s
            ) ("", "") attrs 

    let inline tableName< ^T > ( state : OrmState ) = 
        let reifiedType = typeof< ^T >
        let mutable name = ""
        if _tableNames.TryGetValue( (reifiedType, state), &name ) 
        then name
        else 
            let attrs =
                typedefof< ^T >.GetCustomAttributes( typeof< TableAttribute >, false )
                |> Array.map ( fun x -> x :?> DbAttribute )
            
            let tName = 
                if attrs = Array.empty 
                then typedefof< ^T >.Name
                else attrFold attrs ( context< ^T > state )
                |> fun x -> x.Split( "." )
                |> Array.map ( fun x -> sqlQuote state x )
                |> String.concat "."
            
            _tableNames[(reifiedType, state)] <- tName 
            tName


    let inline mappingHelper< ^T, ^A > state (propertyInfo : PropertyInfo) = 
        propertyInfo.GetCustomAttributes( typeof< ^A >, false ) 
        |> Array.map ( fun y -> y :?> DbAttribute )
        |> fun y -> attrFold y ( context< ^T > state )   

    let inline columnMapping< ^T > ( state : OrmState ) = 
        let reifiedType = typeof< ^T >
        let mutable outMapping = Array.empty
        if _mappings.TryGetValue((reifiedType, state), &outMapping) 
        then outMapping 
        else 
            let mapping = 
                FSharpType.GetRecordFields typedefof< ^T > 
                |> Array.mapi ( fun i x -> 
                    let source =
                        let tmp = mappingHelper< ^T, ByJoinAttribute > state x
                        if tmp = "" then tableName< ^T > state else sqlQuote state tmp
                    let sqlName = 
                        let tmp = mappingHelper< ^T, ColumnAttribute > state x
                        if tmp = "" then x.Name else tmp
                    { 
                        Index = i
                        IsKey = if (mappingHelper< ^T, PrimaryKeyAttribute > state x) = "" then false else true
                        IsIndex = if (mappingHelper< ^T, IdAttribute > state x) = "" then false else true
                        IsRelation = 
                            x.PropertyType.GetInterface(nameof(IRelation)) <> null
                        IsLazilyEvaluated =
                            x.GetCustomAttributes( typeof< LazyEvaluationAttribute >, false ).Length > 0
                        JoinOn = 
                            x.GetCustomAttributes( typeof< OnAttribute >, false ) 
                            |> Array.map ( fun y -> y :?> OnAttribute )
                            |> fun y -> attrJoinFold y ( context< ^T > state )  //attributes< ^T, ColumnAttribute> state
                            |> fun (y : (string * string)) -> if y = ("", "") then None else Some y
                        Source = source
                        QuotedSource = source
                        SqlName = sqlName
                        QuotedSqlName = sqlQuote state sqlName
                        FSharpName = x.Name
                        Type = x.PropertyType 
                        PropertyInfo = x
                    } 
                )
            _mappings[(reifiedType, state)] <- mapping 
            mapping

    let inline table< ^T > ( state : OrmState ) = 
        tableName< ^T > state

    let inline mapping< ^T > ( state : OrmState ) = 
        columnMapping< ^T > state