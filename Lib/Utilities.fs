namespace Form

module Result = 
    // {Ok a; Ok b; Ok c} -> Ok {a; b; c}
    // {Ok a; Ok b; Ok c; Error e} -> Error e
    
    ///<summary>A utility function which takes a query result and returns a result of <c>Ok seq&lt;'a&gt;</c> or <c>Error e</c>, where <c>'a</c> would be the static type parameter <c>^T</c> fed to a previously called query function (e.g. selectAll, selectWhere, etc)</summary>
    ///<param name="results"></param>
    ///<description></description>
    let inline toResultSeq (results : seq<Result<'a,'b>>) = 
        Seq.fold 
            ( fun accumulator item -> 
                match accumulator, item with 
                | Ok state, Ok i -> Ok ( seq { yield! state; yield i } )
                | Error e, _  
                | _, Error e -> Error e 
            ) 
            ( Ok Seq.empty )
            results

    
    ///<summary>A utility function which takes a query result and returns a sequence of the unwrapped Ok results.</summary>
    ///<param name="results"></param>
    ///<description></description>
    let inline toSeq (results : seq<Result<'a,'b>>) = 
        results
        |> Seq.takeWhile ( Result.isOk ) 
        |> Seq.map ( Result.defaultValue Unchecked.defaultof<'a> ) 

    
    ///<summary>A utility function which takes a query result and returns a tuple whose first element is the unwrapped Ok results and second element is the unwrapped Error results</summary>
    ///<param name="results"></param>
    ///<description></description>
    let inline toSeqs (results : seq<Result<'a,'b>>) : (seq<'a> * seq<'b>) = 
        Seq.fold 
            ( fun (okAcc, errAcc) item -> 
                match item with 
                | Ok i -> ( seq { yield! okAcc; yield i } , errAcc)
                | Error e -> ( okAcc , seq { yield! errAcc; yield e}) 
            ) 
            ( Seq.empty, Seq.empty )
            results
    let bindError f state = 
        match state with 
        | Error e -> f e
        | _ -> state

module Utilities = 
    open Form.Attributes
    open System.Collections.Generic
    open Microsoft.FSharp.Reflection
    open Microsoft.FSharp.Core.LanguagePrimitives
    open NpgsqlTypes
    open System
    open System.Data
    open System.Data.SQLite
    open Npgsql
    open MySqlConnector
    open System.Data.SqlClient
    open System.Reflection
    open System.Data.Common
    open Logging
    open System.Data.Odbc    
    open System.Text.RegularExpressions
    
    type Behavior = 
        | Update
        | Insert 
        | Delete

    type IRelation =
        interface end

    type Relation< ^P, ^C > (keyId : int, state : OrmState) =
        let mutable value : Result<^C, exn> seq option = None
        member _.parent = typeof< ^P >
        member _.child = typeof< ^C >

        member _.keyId = keyId 
        member _.state = state
        member _.context = 
            match state with 
            | MSSQL ( _ , ctx) 
            | PSQL ( _ , ctx) 
            | MySQL ( _ , ctx) 
            | ODBC ( _, ctx) 
            | SQLite ( _, ctx) -> ctx :?> DbContext |> EnumToValue

        interface IRelation
        member _.SetValue v = value <- v
        member _.Value = value

        override _.GetHashCode() =
            hash (value, keyId, state)
        
        override this.Equals(b) =
            match b with 
            | :? Relation< ^P, ^C > as r -> this.GetHashCode() = r.GetHashCode()
            | _ -> false
            
        // static member op_Equality ( L : Relation< ^P, ^C >, R : Relation< ^P, ^C > ) =
        //     printfn "Using custom-defined equality"
        //     L.Value = R.Value
            


    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _tableNames = Dictionary<Type * OrmState, string>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _constructors = Dictionary< Type, obj[] -> obj>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _mappings = Dictionary<(Type * OrmState), SqlMapping []>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _toOptions = Dictionary<Type, obj[] -> obj>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _options = Dictionary<Type, Type option>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _keyArray = Dictionary<Type, obj -> obj[]>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    // let mutable _mappings = Dictionary<(Type * OrmState), SqlMapping []>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _relations = Dictionary<Type, ConstructorInfo>()
    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _relationArguments = Dictionary<(Type * Type), int>()
    
    let unpackContext =
            function 
            | MSSQL ( _ , ctx) | PSQL ( _ , ctx) | MySQL ( _ , ctx) | ODBC ( _, ctx) | SQLite ( _, ctx) -> ctx

    let inline connect ( state : OrmState ) : Result< DbConnection, exn > = 
        try 
            let connection = 
                match state with 
                | MSSQL     ( str, _ ) -> new SqlConnection( str ) :> DbConnection
                | MySQL     ( str, _ ) -> new MySqlConnection( str ) :> DbConnection
                | PSQL      ( str, _ ) -> new NpgsqlConnection( str ) :> DbConnection
                | SQLite    ( str, _ ) -> new SQLiteConnection( str ) :> DbConnection
                | ODBC      ( str, _ ) -> new OdbcConnection( str ) :> DbConnection
            connection.Open()
            Ok connection
        with 
        | exn -> Error exn

    let inline sqlQuote ( state : OrmState ) str  =
        match state with 
        | MSSQL _ -> $"[{str}]"
        | MySQL _ -> $"`{str}`"
        | PSQL _ 
        | SQLite _ 
        | ODBC _ -> $"\"{str}\""

    let pattern = fun t -> Regex.Replace(t, @"'", @"''" )
        // function 
        // | t when t :?> string -> Regex.Replace(t, @"'", @"''" )
        // | t when t :?> seq -> t
    // [| ("customerType = %s", "retail"); ( "and (hasSaleWithinPastYear = %s", "true" ); ( "or boughtTiresAYearAgo = %s)", "true" ) |]

    // "customerType = :1 and (hasSaleWithinPastYear = :2 or boughtTiresAYearAgo = :2)" [| "retail"; "true" |]
    let inline escape( where : string * obj seq )= 
        let format, values = where  
        let mutable i = 0
        values  
        |> Seq.fold 
            (fun accumulator item -> 
                i <- i+1

                let sanitizedInput = 
                    match item with 
                    | :? seq<string> as t -> 
                        System.String.Join( ", ", Seq.map ( fun innerItem -> $"'{pattern innerItem}'" ) t )
                    | :? string as t -> pattern <| t.ToString()
                    | :? System.Collections.IEnumerable as t -> //seq of non string type (for numeric sequences, and any others that will behave in an interpolated string. May need to adjust to get desirable behavior generically)
                        System.String.Join( ", ", Seq.map (fun innerItem -> $"{innerItem}") [for i in t do yield i] ) 
                    | _ -> pattern <| item.ToString()
                
                Regex.Replace(accumulator, $":{i}", sanitizedInput)
            )
            format 
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

    let inline columns< ^T > ( state : OrmState ) = 
        mapping< ^T > state
        |> Array.map ( fun x -> 
            if Seq.contains typeof<IRelation> ( x.Type.GetInterfaces() )
            then $"null as {x.QuotedSqlName}" 
            else $"{x.QuotedSource}.{x.QuotedSqlName}" 
        )
       
    let inline fields< ^T >  ( state : OrmState ) = 
        mapping< ^T > state
        |> Array.map ( fun x -> x.FSharpName )

    let inline toOption ( type_: Type ) ( value: obj ) : obj =
        let constructor = 
            if _toOptions.ContainsKey( type_ )
            then _toOptions[type_]
            else
                let info = FSharpType.GetUnionCases( typedefof<Option<_>>.MakeGenericType( [|type_|] ) )
                _toOptions[type_] <- FSharpValue.PreComputeUnionConstructor(info[1])
                _toOptions[type_]
                

        if DBNull.Value.Equals( value ) 
        then None
        else constructor [|value|]

    
    let inline optionType ( type_ : Type )  =
        let mutable opt = None 
        if _options.TryGetValue( type_, &opt )
        then opt
        else 
            let tmp = 
                if type_.IsGenericType && type_.GetGenericTypeDefinition( ) = typedefof<Option<_>>
                then Some ( type_.GetGenericArguments( ) |> Array.head ) // optionType Option<User> -> User  
                else None
            _options[type_] <- tmp
            tmp

    ///<summary>Gets the constructor for the child type in a Parent-Child relationship, computes it if it hasn't been memoized yet.</summary>
    ///<param name="parent"></param>
    ///<param name="child"></param>
    ///<returns>The record constructor for the given child type in the context of the Parent-Child relation.</returns>
    // let inline relationType ( parent : Type ) ( child : Type ) : obj[] -> obj =
    //     let mutable childDictionary = Dictionary<Type, obj[]->obj>()
    //     //check to see if the parent exists
    //     //if it does assign the reference to childDictionary
    //     if not <| _relations.TryGetValue( parent, &childDictionary )
    //     then 
    //         //if it doesn't, assign a new dictionary with the current 
    //         //child and its constructor as the first key-value pair
    //         let tmp = FSharpValue.PreComputeRecordConstructor(child)
    //         childDictionary[child] <- tmp
    //         _relations[parent] <- childDictionary
        
    //     let mutable constructor = fun _ -> Object()
    //     //check to see if the child exists in the parent entry of the relation dict
    //     //if it does assign the reference to constructor and return the constructor
    //     if childDictionary.TryGetValue( child, &constructor )
    //     then constructor
    //     else 
    //         //if it doesn't, add the child (its record constructor)
    //         let tmp = FSharpValue.PreComputeRecordConstructor(child)
    //         childDictionary[child] <- tmp
    //         tmp

    let inline makeParameter ( state : OrmState ) : DbParameter =
        match state with
        | MSSQL     _ -> SqlParameter( )
        | MySQL     _ -> MySqlParameter( )
        | PSQL      _ -> NpgsqlParameter( )
        | SQLite    _ -> SQLiteParameter( )
        | ODBC      _ -> OdbcParameter( )
        
    let toDbType ( typeCode : TypeCode ) = 
        match typeCode with 
            | TypeCode.Byte     -> DbType.Byte
            | TypeCode.Char     -> DbType.StringFixedLength    // ???
            | TypeCode.Int16    -> DbType.Int16
            | TypeCode.Int32    -> DbType.Int32
            | TypeCode.Int64    -> DbType.Int64
            | TypeCode.SByte    -> DbType.SByte
            | TypeCode.Double   -> DbType.Double
            | TypeCode.Single   -> DbType.Single
            | TypeCode.String   -> DbType.String
            | TypeCode.UInt16   -> DbType.UInt16
            | TypeCode.UInt32   -> DbType.UInt32
            | TypeCode.UInt64   -> DbType.UInt64
            | TypeCode.Boolean  -> DbType.Boolean
            | TypeCode.Decimal  -> DbType.Decimal
            | TypeCode.DateTime -> DbType.DateTime // Used for Date, DateTime and DateTime2 DbTypes DbType.DateTime
            | _ -> DbType.Object 
    
    let inline unwrapOption ( tmp : DbParameter ) ( opt : obj ) ( ) = 
        match opt with 
        | :? Option<Byte>       as t -> tmp.Value <- t |> Option.get
        | :? Option<Char>       as t -> tmp.Value <- t |> Option.get
        | :? Option<SByte>      as t -> tmp.Value <- t |> Option.get //Int8
        | :? Option<Int16>      as t -> tmp.Value <- t |> Option.get
        | :? Option<Int32>      as t -> tmp.Value <- t |> Option.get
        | :? Option<Int64>      as t -> tmp.Value <- t |> Option.get
        #if NET7_0_OR_GREATER
        | :? Option<Int128>     as t -> tmp.Value <- t |> Option.get
        #endif
        | :? Option<Double>     as t -> tmp.Value <- t |> Option.get
        | :? Option<Single>     as t -> tmp.Value <- t |> Option.get
        | :? Option<String>     as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt16>     as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt32>     as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt64>     as t -> tmp.Value <- t |> Option.get
        #if NET7_0_OR_GREATER
        | :? Option<UInt128>    as t -> tmp.Value <- t |> Option.get
        #endif
        | :? Option<Boolean>    as t -> tmp.Value <- t |> Option.get
        | :? Option<Decimal>    as t -> tmp.Value <- t |> Option.get
        | :? Option<DateTime>   as t -> tmp.Value <- t |> Option.get
        | _ -> ()
    
    let inline getParamChar state = 
        match state with
        | ODBC _ -> "?"
        | _ -> "@"

    // let inline relationshipArguments< ^T, ^S > state = 


    let inline keyArray<^T> state keyId = 
        let ctx = box (unpackContext state) :?> DbContext |> EnumToValue
        let _type = typeof<^T>
        if _keyArray.ContainsKey( _type )
        then _keyArray[ _type ]
        else
            let props = 
                _type.GetType().GetProperties() 
                |> Seq.filter( fun prop -> 
                    if prop.IsDefined( typeof< OnAttribute > ) 
                    then 
                        let attr = 
                            prop.GetCustomAttributes(typeof< OnAttribute >) 
                            |> Seq.map ( fun x -> x :?> OnAttribute) 
                            |> Seq.filter ( fun x -> snd x.Value = ctx )
                            |> Seq.head
                        snd attr.Value = ctx && attr.key = keyId //if the key lines up AND the context.
                    else false
                )
                |> Seq.map ( fun prop -> {| 
                property = prop
                attribute = 
                    prop.GetCustomAttributes(typeof< OnAttribute >) 
                    |> Seq.map ( fun x -> x :?> OnAttribute) 
                    |> Seq.filter ( fun x -> snd x.Value = ctx ) 
                    |> Seq.head 
                |} )
                |> Seq.sortBy ( fun column -> column.attribute.part )
                |> Seq.toArray
                |> Array.map (fun prop -> prop.property)
                
            let getValuesFromProps = 
                fun ( instance : obj ) -> 
                    Array.map ( fun (prop : PropertyInfo) -> prop.GetValue(instance)) props 
            _keyArray[_type] <- getValuesFromProps 
            getValuesFromProps
            

        (*
            select *
            from subfact 
            where 
                concat(key1,key2,...) in ('12..', '4269..', ) and 

            select *
            from subfact
            where
                key1 = someVal and key2 = someotherval
            union
            select *
            from subfact
            where
                key1 = someVal2 and key2 = someotherval2
            ...
        *)

    ///<Description> Takes a reader of type IDataReader and a state of type OrmState -> consumes the reader and returns a sequence of type ^T.</Description>
    let inline consumeReader< ^T > ( state : OrmState ) ( reader : IDataReader ) = 
        let reifiedType = typeof< ^T >
        let constructor = 
            let mutable tmp = fun _ -> obj()
            if _constructors.TryGetValue(reifiedType, &tmp)
            then ()
            else 
                tmp <- FSharpValue.PreComputeRecordConstructor(reifiedType)
                _constructors[reifiedType] <- tmp
            tmp       
        let columns = columnMapping<^T> state 
        let mutable options = 
            [| for fld in columns do  
                match optionType fld.Type with //handle option type, i.e. option<T> if record field is optional, else T
                | Some _type -> toOption _type 
                | None -> id
            |]
            
        
        
        let mutable relations = 
            let context = unpackContext state
            [| for fld in columns do  
                if fld.IsRelation
                then
                    let typeParameters = fld.Type.GenericTypeArguments
                    let reifiedType = typedefof<Relation<_,_>>.MakeGenericType( typeParameters ) 
                    let mutable constructor : ConstructorInfo = null
                    if _relations.TryGetValue(reifiedType, &constructor)
                    then ()
                    else
                        constructor <- reifiedType.GetConstructor([|typeof<int>; typeof<OrmState>|])
                        _relations[reifiedType] <- constructor
                    let relation = constructor.Invoke( [| box 1; box state |])
                    fun _ -> relation 
                else id
            |]

        
        //We're going to need to add logic here to instantiate relation types.
        seq { 
            try 
                while reader.Read( ) do
                    
                    constructor 
                        [| for i in 0..reader.FieldCount-1 do 
                            reader.GetValue( reader.GetOrdinal( columns[i].SqlName ) )
                            |> options[i] 
                            |> relations[i]
                        |] 
                    :?> ^T
                    |> Ok
                    
            with exn -> 
                Error exn                
        }  

    let inline insertBase< ^T > ( state : OrmState ) insertKeys =
        let paramChar = getParamChar state
        let tableName = ( table< ^T > state ) 
        let cols = 
            mapping< ^T > state
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries 
            |> Array.filter (fun col -> not col.IsKey || insertKeys )
        let placeHolders = 
            cols
            |> Array.map ( fun col ->  
                match state with 
                | ODBC _ -> paramChar
                | _ -> sprintf "%s%s" paramChar col.FSharpName
            )
            |> String.concat ", "
        let columnNames = 
            cols
            |> Array.map ( fun x -> x.QuotedSqlName )
            |> String.concat ", "  
        
        sprintf "insert into %s ( %s ) values ( %s )" tableName columnNames placeHolders
    
    let inline makeCommand ( state : OrmState ) ( query : string ) ( connection : DbConnection ) : DbCommand = 
        // log ( sprintf "Query being generated:\n\n%s\n\n" <| query )
        match state with 
        | MSSQL _ ->    new SqlCommand ( query, connection :?> SqlConnection )
        | MySQL _ ->    new MySqlCommand ( query, connection :?> MySqlConnection )
        | PSQL _ ->     new NpgsqlCommand ( query, connection :?> NpgsqlConnection )
        | SQLite _ ->   new SQLiteCommand ( query, connection :?> SQLiteConnection )
        | ODBC _ ->     new OdbcCommand ( query, connection :?> OdbcConnection )

    let inline withTransaction state transactionFunction (noneFunction : DbConnection -> Result<'a, exn> seq) transaction : Result<'a, exn> seq =
        match transaction with 
        | Some ( transaction : DbTransaction ) -> transactionFunction transaction
        | None -> 
            seq {
                match connect state with 
                | Ok conn -> 
                    yield! noneFunction conn 
                | Error exn -> yield Error exn
            }

    let rec genericTypeName full ( _type : Type ) = 
        if not _type.IsGenericType 
        then _type.Name
        else 
            let typeName = 
                let mutable tmp = _type.GetGenericTypeDefinition().Name 
                tmp <- tmp.Substring(0, tmp.IndexOf('`'))
                tmp
            if not full 
            then typeName 
            else 
                let args = 
                    _type.GetGenericArguments()
                    |> Array.map (genericTypeName full)
                    |> String.concat ","
                
                sprintf "%s<%s>" typeName args

    let inline parameterizeCommand< ^T > state query (transaction : DbTransaction) includeKeys behavior ( instance : ^T ) =
        let cmd = makeCommand state query transaction.Connection
        cmd.Transaction <- transaction
        let paramChar = getParamChar state
        let allColumns = 
            mapping< ^T > state
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = (tableName< ^T > state)  ) //! Filter out joins for non-select queries
        
        match behavior with 
        | Insert -> allColumns |> Array.filter (fun col ->  not col.IsKey || includeKeys )
        | Update -> allColumns |> Array.filter (fun col ->  not col.IsKey || includeKeys ) |> fun x -> Array.append x ( Array.filter (fun col -> col.IsKey ) allColumns )
        | Delete -> allColumns |> Array.filter (fun col ->  col.IsKey )
        |> Array.iteri ( fun i mappedInstance -> 
            // log (sprintf "binding value %s(%A) to position %i - " mappedInstance.FSharpName (mappedInstance.PropertyInfo.GetValue( instance )) i )
            let param =
                let mutable tmp = cmd.CreateParameter( )    
                let mappedValue = mappedInstance.PropertyInfo.GetValue( instance )
                match state with 
                | ODBC _ -> ()
                | _ -> tmp.ParameterName <- sprintf "%s%s" paramChar mappedInstance.FSharpName
                if
                    mappedValue = null 
                then
                    tmp.IsNullable <- true
                    tmp.Value <- DBNull.Value
                else
                    if 
                        genericTypeName false mappedInstance.Type = "FSharpOption"
                    then
                        tmp.IsNullable <- true
                        unwrapOption tmp (mappedValue) ()
                    else
                        tmp.Value <- mappedValue // Some 1
                tmp

            cmd.Parameters.Add ( param ) |> ignore
        )
        
        cmd

    let inline parameterizeSeqAndExecuteCommand< ^T > state query (cmd : DbCommand) includeKeys behavior ( instances : ^T seq ) =
         
        
        let mapp = 
            let tmp = 
                mapping< ^T > state
                |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries 
            
            match behavior with 
            | Insert -> tmp |> Array.filter (fun col ->  not col.IsKey || includeKeys )
            | Update -> tmp |> Array.filter (fun col ->  not col.IsKey || includeKeys ) |> fun x -> Array.append x ( Array.filter (fun col -> col.IsKey ) tmp )
            | Delete -> tmp |> Array.filter (fun col ->  col.IsKey )

        let paramChar = getParamChar state        
        let mutable cmdParams = 
            mapp 
            |> Array.map (
                fun (mappedInstance : SqlMapping ) ->
                    let mutable tmp = cmd.CreateParameter( ) 
                    match state with 
                    | ODBC _ -> ()
                    | _ -> tmp.ParameterName <- sprintf "%s%s" paramChar mappedInstance.FSharpName
                    cmd.Parameters.Add ( tmp ) |> ignore
                    tmp
                )
            
        instances 
        |> Seq.mapi ( fun index instance ->
            mapp 
            |> Array.iteri ( fun jindex mappedInstance ->   
                let thing = mappedInstance.PropertyInfo.GetValue( instance )                  
                if
                    thing = null 
                then
                    cmdParams[jindex].IsNullable <- true
                    cmdParams[jindex].Value <- DBNull.Value
                else 
                    if genericTypeName false mappedInstance.Type = "FSharpOption"
                    then
                        cmdParams[jindex].IsNullable <- true
                        unwrapOption cmdParams[jindex] (thing) ()
                        
                    else
                        cmdParams[jindex].Value <- thing // Some 1
            )
            
            // log ( 
            //         sprintf "Param count: %A" cmd.Parameters.Count :: 
            //         [ for i in [0..cmd.Parameters.Count-1] do 
            //             yield sprintf "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value 
            //         ]
            //         |> String.concat "\n"
            //     )  
            try cmd.ExecuteNonQuery() |> Ok
            with exn -> Error exn
        )
        |> Seq.fold ( fun accumulator item -> 
            match accumulator, item with 
            | Ok a, Ok i -> Ok ( a + i )
            | Error e, _ 
            | _, Error e -> Error e
        ) ( Ok 0 )

    let inline joins< ^T > (state : OrmState) = 
        let qoute = sqlQuote state
        mapping< ^T > state
        |> Array.filter (fun sqlMap -> Option.isSome sqlMap.JoinOn)
        |> Array.groupBy (fun x -> x.JoinOn |> Option.get |> fst)
        |> Array.map (fun (source, maps)  -> 
            Array.map (fun map -> 
                let secCol = map.JoinOn |> Option.get |> snd 
                $"{qoute source}.{qoute secCol} = {map.QuotedSource}.{map.QuotedSqlName}"
            ) maps
            |> String.concat " and "
            |> fun onString -> $"left join {qoute source} on {onString}"
        )
        |> String.concat "\n"
    
    let inline queryBase< ^T > ( state : OrmState ) = 
        let cols = columns< ^T > state 
        let joins = joins<^T> state 
        ( String.concat ", " cols ) + " from " + table< ^T > state
        + " " + joins

    let inline selectHelper< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) f = 
        let query = queryBase< ^T > state |> f

        transaction
        |> withTransaction  
            state 
            ( fun (transaction : DbTransaction) -> 
                seq {
                    use cmd = makeCommand state query ( transaction.Connection ) 
                    cmd.Transaction <- transaction 
                    try 
                        use reader = cmd.ExecuteReader( ) 
                        yield! consumeReader< ^T > state reader  
                    with exn -> Error exn
                } 
            )
            ( fun ( connection : DbConnection ) -> 
                seq {
                    use cmd = makeCommand state query connection  
                    try 
                        use reader = cmd.ExecuteReader( )
                        yield! consumeReader< ^T > state reader
                    with exn -> yield Error exn
                }
            )

    let inline updateBase< ^T > ( state : OrmState )  = 
        let paramChar = getParamChar state
        let cols = 
            mapping< ^T > state
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Array.filter (fun col -> not col.IsKey) //Can't update keys
        // log ( sprintf "columns to update: %A" cols )
        let queryParams = 
            cols 
            |> Array.map (fun col -> 
                match state with 
                | ODBC _ -> paramChar
                | _ -> sprintf "%s%s" paramChar col.FSharpName ) // @col1, @col2, @col3
            

        let table = table< ^T > state 
        let set = 
            Array.zip cols queryParams
            |> Array.map ( fun x -> sprintf "%s = %s" (fst x).QuotedSqlName (snd x) ) 
            |> String.concat ", "

        "update " + table + " set " + set 

    let inline ensureId< ^T > ( state: OrmState ) = 
        mapping< ^T > state 
        |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
        |> Array.filter ( fun x -> x.IsKey )
        |> fun x -> if Array.length x = 0 then "Record must have at least one ID attribute specified..." |> exn |> Error else Ok x
    
    let inline updateHelper<^T> ( state : OrmState ) ( transaction : DbTransaction option ) ( whereClause : string ) ( instance : ^T ) = 
        let query = ( updateBase< ^T > state ) + (whereClause) 
        transaction
        |> withTransaction 
            state 
            ( fun transaction ->  
                use command = parameterizeCommand< ^T > state query transaction false Update instance 
                command.Transaction <- transaction
                seq { 
                    try 
                        command.ExecuteNonQuery ( ) |> Ok 
                    with exn -> 
                        // log ( sprintf "%A" exn )
                        Error exn
                } 
            )
            ( fun connection -> 
                let transaction = connection.BeginTransaction()
                let command = parameterizeCommand< ^T > state query transaction false Update instance 
                try  
                    seq {
                        yield! seq {command.ExecuteNonQuery( ) |> Ok}
                    }
                    |> Seq.map (fun x -> x)
                    |> fun x -> transaction.Commit();  x
                with exn -> 
                    transaction.Rollback()
                    seq { Error exn }
            )
        |> Seq.head

    let inline updateManyHelper<^T> ( state : OrmState ) ( transaction : DbTransaction option ) ( whereClause : string ) ( instances : ^T seq ) = 
        let query = ( updateBase< ^T > state ) + (whereClause) 
        transaction
        |> withTransaction 
            state 
            ( fun transaction -> 
                let cmd = makeCommand state query transaction.Connection
                seq { parameterizeSeqAndExecuteCommand< ^T > state query ( cmd ) false Update instances }
            )
            ( fun connection -> 
                let transaction = connection.BeginTransaction()
                let cmd = makeCommand state query connection
                try  
                    seq {
                        yield! seq {parameterizeSeqAndExecuteCommand< ^T > state query cmd false Update instances }
                    }
                    |> Seq.map (fun x -> x)
                    |> fun x -> transaction.Commit();  x
                with exn -> 
                    transaction.Rollback()
                    seq { Error exn }
            )
        |> Seq.head

    let inline deleteBase< ^T > state =
        table< ^T > state 
        |> sprintf "delete from %s where "

    let inline deleteHelper< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( whereClause : string ) ( instance : ^T ) =
        let query = deleteBase< ^T > state + (whereClause) 
        transaction
        |> withTransaction 
            state 
            ( fun transaction ->  
                use command = parameterizeCommand< ^T > state query transaction false Delete instance 
                command.Transaction <- transaction
                seq { 
                    try command.ExecuteNonQuery ( ) |> Ok 
                    with exn -> 
                        // log ( sprintf "%A" exn )
                        Error exn
                } 
            )
            ( fun connection -> 
                let transaction = connection.BeginTransaction()
                let command = parameterizeCommand< ^T > state query transaction false Delete instance 
                try  
                    seq {
                        yield! seq {command.ExecuteNonQuery( ) |> Ok}
                    }
                    |> Seq.map (fun x -> x)
                    |> fun x -> transaction.Commit();  x
                with exn -> 
                    transaction.Rollback()
                    seq { Error exn }
            )
        |> Seq.head

    
    let inline deleteManyHelper< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( whereClause : string ) ( instances : ^T seq ) =
        let query = deleteBase< ^T > state + (whereClause) 
        transaction 
        |> withTransaction 
            state 
            ( fun transaction -> 
                let cmd = makeCommand state query transaction.Connection
                seq { parameterizeSeqAndExecuteCommand< ^T > state query ( cmd ) false Delete instances }
            )
            ( fun connection -> 
                let transaction = connection.BeginTransaction()
                let cmd = makeCommand state query connection
                try 
                    seq {
                        yield parameterizeSeqAndExecuteCommand< ^T > state query cmd false Delete instances
                    }
                    |> Seq.map (fun x -> x)
                    |> fun x -> transaction.Commit();  x
                with exn -> 
                    transaction.Rollback()
                    seq { Error exn }
            )
        |> Seq.head
