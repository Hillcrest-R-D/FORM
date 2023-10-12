namespace Form

module Utilities = 
    open Form.Attributes
    open System.Collections.Generic
    open FSharp.Reflection.FSharpReflectionExtensions
    open Microsoft.FSharp.Reflection
    open System.Collections.Generic
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

    let inline connect ( state : OrmState ) : Result< DbConnection, exn > = 
        try 
            match state with 
            | MSSQL     ( str, _ ) -> new SqlConnection( str ) :> DbConnection
            | MySQL     ( str, _ ) -> new MySqlConnection( str ) :> DbConnection
            | PSQL      ( str, _ ) -> new NpgsqlConnection( str ) :> DbConnection
            | SQLite    ( str, _ ) -> new SQLiteConnection( str ) :> DbConnection
            |> Ok
        with 
        | exn -> Error exn

    let inline log f = 
    #if DEBUG 
            f()
    #endif  
            ()

    let inline sqlQuote ( state : OrmState ) str  =
        match state with 
        | MSSQL _ -> $"[{str}]"
        | MySQL _ -> $"`{str}`"
        | PSQL _ | SQLite _ -> $"\"{str}\""

    let inline context< ^T > ( state : OrmState ) = 
        match state with 
        | MSSQL     ( _, c ) -> c 
        | MySQL     ( _, c ) -> c 
        | PSQL      ( _, c ) -> c 
        | SQLite    ( _, c ) -> c 
    let inline attrFold ( attrs : DbAttribute array ) ( ctx : Enum ) = 
        Array.fold ( fun s ( x : DbAttribute ) ->  
                if snd x.Value = ( ( box( ctx ) :?> DbContext ) |> EnumToValue ) 
                then fst x.Value
                else s
            ) "" attrs

    let inline attrJoinFold ( attrs : OnAttribute array ) ( ctx : Enum ) = 
        Array.fold ( fun s ( x : OnAttribute ) ->  
                if snd x.Value = ( ( box( ctx ) :?> DbContext ) |> EnumToValue ) 
                then (fst x.Value, x.key.Name)
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
                if attrs = Array.empty then
                    typedefof< ^T >.Name
                else 
                    attrFold attrs ( context< ^T > state )
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
        |> Array.map ( fun x -> $"{x.QuotedSource}.{x.QuotedSqlName}" )
       
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
        
    let inline makeParameter ( state : OrmState ) : DbParameter =
        match state with
        | MSSQL _ -> SqlParameter( )
        | MySQL _ -> MySqlParameter( )
        | PSQL _ -> NpgsqlParameter( )
        | SQLite _ -> SQLiteParameter( )
        
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
        | :? Option<Int16>      as t -> tmp.Value <- t |> Option.get
        | :? Option<Int64>      as t -> tmp.Value <- t |> Option.get
        | :? Option<Int32>      as t -> tmp.Value <- t |> Option.get
        | :? Option<SByte>      as t -> tmp.Value <- t |> Option.get
        | :? Option<Double>     as t -> tmp.Value <- t |> Option.get
        | :? Option<Int128>     as t -> tmp.Value <- t |> Option.get
        | :? Option<Single>     as t -> tmp.Value <- t |> Option.get
        | :? Option<String>     as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt16>     as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt32>     as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt64>     as t -> tmp.Value <- t |> Option.get
        | :? Option<Boolean>    as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt128>    as t -> tmp.Value <- t |> Option.get
        | :? Option<Decimal>    as t -> tmp.Value <- t |> Option.get
        | :? Option<DateTime>   as t -> tmp.Value <- t |> Option.get
        | _ -> ()

    let inline exceptionHandler f =
        try 
            Ok <| f( )
        with 
        | exn -> Error exn
    
    let inline getParamChar state = 
        match state with
        | MSSQL _ -> "@"
        | MySQL _ -> "$"
        | PSQL _ -> "@"
        | SQLite _ -> "@"   


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
        let mutable options = 
            [| for fld in ( columnMapping< ^T > state  ) do  
                match optionType fld.Type with //handle option type, i.e. option<T> if record field is optional, else T
                | Some _type -> toOption _type 
                | None -> id
            |]
        seq { 
            while reader.Read( ) do
                constructor 
                    [| for i in 0..reader.FieldCount-1 do 
                        options[i] <| reader.GetValue( i ) 
                    |] 
                :?> ^T // dang ol' class factory man
        }  

    let inline insertBase< ^T > ( state : OrmState ) insertKeys =
        let paramChar = getParamChar state
        let tableName = ( table< ^T > state ) 
        let cols = 
            mapping< ^T > state
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries 
            |> Array.filter (fun col -> insertKeys || not col.IsKey )
        let placeHolders = 
            cols
            |> Array.mapi ( fun i x -> sprintf "%s%s" paramChar x.FSharpName )
            |> String.concat ", "
        let columnNames = 
            cols
            |> Array.map ( fun x -> x.QuotedSqlName )
            |> String.concat ", "  
        
        sprintf "insert into %s ( %s ) values ( %s )" tableName columnNames placeHolders
    
    let inline makeCommand ( state : OrmState ) ( query : string ) ( connection : DbConnection ) : DbCommand = 
        log (fun _ -> printfn "Query being generated:\n\n%s\n\n\n" query )
        match state with 
        | MSSQL _ -> new SqlCommand ( query, connection :?> SqlConnection )
        | MySQL _ -> new MySqlCommand ( query, connection :?> MySqlConnection )
        | PSQL _ -> new NpgsqlCommand ( query, connection :?> NpgsqlConnection )
        | SQLite _ -> new SQLiteCommand ( query, connection :?> SQLiteConnection )

    let inline withTransaction state transactionFunction noneFunction = 
        function 
        | Some ( transaction : DbTransaction ) ->
            try 
                transactionFunction transaction
                |> Ok 
            with 
            | exn -> Error exn
        | None -> 
            connect state 
            |> Result.map ( noneFunction )

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

    let inline parameterizeCommand< ^T > state query conn ( instance : ^T ) =
        let cmd = makeCommand state query conn 
        
        let paramChar = getParamChar state
        
        mapping< ^T > state
        |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = (tableName< ^T > state)  ) //! Filter out joins for non-select queries
        |> Array.iter ( fun mappedInstance -> 
            let param =  
                let mutable tmp = cmd.CreateParameter( )    
                let mappedValue = mappedInstance.PropertyInfo.GetValue( instance )
                tmp.ParameterName <- sprintf "%s%s" paramChar mappedInstance.FSharpName
                if
                    mappedValue = null 
                then
                    tmp.IsNullable <- true
                    tmp.Value <- DBNull.Value
                else
                    if genericTypeName false mappedInstance.Type = "FSharpOption"
                    then
                        tmp.IsNullable <- true
                        unwrapOption tmp (mappedValue) ()
                        
                    else
                        tmp.Value <- 
                            mappedValue // Some 1
                tmp

            cmd.Parameters.Add ( param ) |> ignore
        )
        
        cmd

    let inline parameterizeSeqAndExecuteCommand< ^T > state query (transaction : DbTransaction) ( instances : ^T seq ) =
        let cmd = makeCommand state query transaction.Connection
        let mapp = 
            mapping< ^T > state
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries 
        cmd.Transaction <- transaction 
        let paramChar = getParamChar state        
        let mutable cmdParams = 
            mapp 
            |> Array.map (
                fun (mappedInstance : SqlMapping ) ->
                    let mutable tmp = cmd.CreateParameter( ) 
                    tmp.ParameterName <- sprintf "%s%s" paramChar mappedInstance.FSharpName
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
            cmd.ExecuteNonQuery()
        )
        |> Seq.fold (+) 0

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

    let inline select< ^T > ( state : OrmState ) (transaction : DbTransaction option) query = 
        transaction
        |> withTransaction  
            state 
            ( fun (transaction : DbTransaction) -> 
                seq {
                    use cmd = makeCommand state query ( transaction.Connection ) 
                    cmd.Transaction <- transaction 
                    use reader = cmd.ExecuteReader( ) 
                    yield! consumeReader< ^T > state reader  
                }
            )
            ( fun ( connection : DbConnection ) -> 
                seq {
                    connection.Open()
                    use cmd = makeCommand state query connection 
                    use reader = cmd.ExecuteReader( CommandBehavior.CloseConnection ) 
                    yield! consumeReader< ^T > state reader  
                    connection.Close()
                }
                
            )

    let inline selectHelper< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) f = 
        queryBase< ^T > state
        |> f
        |> select< ^T > state transaction


    let inline updateBase< ^T > ( state : OrmState ) = 
        let pchar = getParamChar state
        let cols = 
            mapping< ^T > state
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Array.filter (fun col -> not col.IsKey) //Can't update keys
        log ( fun _ -> printfn "columns to update: %A" cols )
        let queryParams = 
            cols 
            |> Array.map (fun col -> pchar + col.FSharpName ) // @col1, @col2, @col3
            

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
        let query = ( updateBase< ^T > state ) + whereClause 
        transaction
        |> withTransaction 
            state 
            ( fun transaction ->  
                use command = parameterizeCommand< ^T > state query ( transaction.Connection ) instance 
                command.Transaction <- transaction
                command.ExecuteNonQuery ( )
            )
            ( fun connection -> 
                connection.Open( )
                use command = parameterizeCommand< ^T > state query connection instance 
                let result = command.ExecuteNonQuery ( )
                connection.Close( )
                result 
            )

    let inline updateManyHelper<^T> ( state : OrmState ) ( transaction : DbTransaction option ) ( whereClause : string ) ( instances : ^T seq ) = 
        let query = ( updateBase< ^T > state ) + whereClause 
        transaction
        |> withTransaction 
            state 
            ( fun transaction -> 
                parameterizeSeqAndExecuteCommand< ^T > state query ( transaction ) instances  
            )
            ( fun connection -> 
                connection.Open( )
                let transaction = connection.BeginTransaction() 
                parameterizeSeqAndExecuteCommand< ^T > state query transaction instances 
                |> fun x -> transaction.Commit();connection.Close(); x 
            )
        

    let inline deleteBase< ^T > state =
        table< ^T > state 
        |> sprintf "delete from %s where "

    let inline deleteHelper< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( whereClause : string ) ( instance : ^T ) =
        let query = deleteBase< ^T > state + whereClause 
        transaction
        |> withTransaction 
            state 
            ( fun transaction -> 
                use command = parameterizeCommand< ^T > state query ( transaction.Connection ) instance 
                command.Transaction <- transaction
                command.ExecuteNonQuery ( )        
            )
            ( fun connection -> 
                connection.Open( )
                use cmd = parameterizeCommand< ^T > state query connection instance 
                let result = cmd.ExecuteNonQuery ( )
                connection.Close()
                result
            )
    
    let inline deleteManyHelper< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( whereClause : string ) ( instances : ^T seq ) =
        let query = deleteBase< ^T > state + whereClause 
        transaction 
        |> withTransaction 
            state 
            ( fun transaction -> 
                parameterizeSeqAndExecuteCommand< ^T > state query ( transaction ) instances  
            )
            ( fun connection -> 
                connection.Open( )
                let transaction = connection.BeginTransaction() 
                parameterizeSeqAndExecuteCommand< ^T > state query transaction instances 
                |> fun x -> transaction.Commit();connection.Close(); x 
            )