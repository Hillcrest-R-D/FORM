namespace Form 

open System
open System.Data
open System.Reflection
open FSharp.Reflection
open Npgsql
open NpgsqlTypes
open Microsoft.Data.SqlClient
open Microsoft.Data.Sqlite
open MySqlConnector
open System.Data.Common
open Microsoft.FSharp.Core.LanguagePrimitives
open Form.Attributes
open FSharp.Reflection.FSharpReflectionExtensions
open System.Collections.Generic

module Orm = 
    ///<Description>Stores the flavor And context used for a particular connection.</Description>
    let inline connect ( state : OrmState ) : Result< DbConnection, exn > = 
        try 
            match state with 
            | MSSQL     ( str, _ ) -> new SqlConnection( str ) :> DbConnection
            | MySQL     ( str, _ ) -> new MySqlConnection( str ) :> DbConnection
            | PSQL      ( str, _ ) -> new NpgsqlConnection( str ) :> DbConnection
            | SQLite    ( str, _ ) -> new SqliteConnection( str ) :> DbConnection
            |> Ok
        with 
        | exn -> Error exn

    let inline log f = 
#if DEBUG 
        f()
#endif  
        ()

    let inline beginTransaction ( state : OrmState ) =
        match connect state with 
        | Ok connection ->
            try 
                if connection.State = ConnectionState.Closed 
                then connection.Open()
                else ()
                Some ( connection.BeginTransaction() )
            with 
            | exn -> 
                log ( fun _ -> printfn "Exception when beginning transaction: %A" exn )
                None
        | Error e -> 
            log ( fun _ -> printfn "Error when beginning transaction: %A" e )
            None
    

 
    let commitTransaction = 
        Option.map ( fun ( transaction : DbTransaction ) -> transaction.Commit() )
    let rollbackTransaction = 
        Option.map ( fun ( transaction : DbTransaction ) -> transaction.Rollback() )

    let tryCommit (transaction : DbTransaction option) = // option<Transaction> -> Result<unit, exn>
            try 
                commitTransaction transaction |> Ok 
            with  
            | exn -> 
                rollbackTransaction transaction 
#if DEBUG                 
                |> printfn "%A"
#endif
                exn |> Error
            
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
    let mutable _tableNames = Dictionary<Type * OrmState, string>()
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

    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _mappings = Dictionary<(Type * OrmState), SqlMapping []>();

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

    /// **Do not use.** This is internal to Form and cannot be hidden due to inlining. 
    /// We make no promises your code won't break in the future if you use this.
    let mutable _toOptions = Dictionary<Type, obj[] -> obj>()
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
        

    // let mutable _toOptions = Dictionary<Type, UnionCaseInfo array>()
    // let inline toOption ( type_: Type ) ( value: obj ) =
    //     let mutable info = Array.empty
    //     if _toOptions.TryGetValue( type_, &info )
    //     then ()
    //     else 
    //         info <- FSharpType.GetUnionCases( typedefof<Option<_>>.MakeGenericType( [|type_|] ) )
    //         _toOptions[type_] <- info
    //     let tag, variable = if DBNull.Value.Equals( value ) then 0, [||] else 1, [|value|]
    //     FSharpValue.MakeUnion( info[tag], variable )

    let mutable _options = Dictionary<Type, Type option>()
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

    let toDbType ( typeCode : TypeCode ) = 
        match typeCode with 
            | TypeCode.Boolean -> DbType.Boolean
            | TypeCode.Byte -> DbType.Byte
            | TypeCode.Char -> DbType.StringFixedLength    // ???
            | TypeCode.DateTime -> DbType.DateTime// Used for Date, DateTime and DateTime2 DbTypes DbType.DateTime
            | TypeCode.Decimal -> DbType.Decimal
            | TypeCode.Double -> DbType.Double
            | TypeCode.Int16 -> DbType.Int16
            | TypeCode.Int32 -> DbType.Int32
            | TypeCode.Int64 -> DbType.Int64
            | TypeCode.SByte -> DbType.SByte
            | TypeCode.Single -> DbType.Single
            | TypeCode.String -> DbType.String
            | TypeCode.UInt16 -> DbType.UInt16
            | TypeCode.UInt32 -> DbType.UInt32
            | TypeCode.UInt64 -> DbType.UInt64
            | _ -> DbType.Object 
            
    let inline exceptionHandler f =
        try 
            Ok <| f( )
        with 
        | exn -> Error exn

    let mutable _constructors = Dictionary< Type, obj[] -> obj>()

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
    
    let inline getParamChar state = 
        match state with
        | MSSQL _ -> "@"
        | MySQL _ -> "$"
        | PSQL _ -> "@"
        | SQLite _ -> "@"   
        
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
        | SQLite _ -> new SqliteCommand ( query, connection :?> SqliteConnection )

    let inline withTransaction state transactionFunction noneFunction = 
        function 
        | Some ( transaction : DbTransaction ) ->
            transactionFunction transaction
            |> Ok 
        | None -> 
            connect state 
            |> Result.map ( noneFunction )

    let inline execute ( state : OrmState ) ( transaction : DbTransaction option ) sql =
        transaction 
        |> withTransaction 
            state
            ( fun transaction -> 
                use cmd = makeCommand state sql ( transaction.Connection )
                cmd.Transaction <- transaction  
                cmd.ExecuteNonQuery( )
            )
            ( fun connection -> 
                connection.Open()
                use cmd = makeCommand state sql connection  
                let result = cmd.ExecuteNonQuery( )
                connection.Close() 
                result
            )
    
    ///<Description>
    /// Takes a function of IDataReader -> Result< 't seq, exn> (see FORMs consumeReader function as example) to 
    /// transfer the results of executing the specified sql against the specified database given by state into an 
    /// arbitrary type 't, defined by you in the readerFunction.
    /// </Description>
    let inline generateReader state sql  =
        match connect state with
        | Ok conn -> 
            try 
                conn.Open( )
                use cmd = makeCommand state sql conn 
                cmd.ExecuteReader( CommandBehavior.CloseConnection )
                |> Ok
            with 
            | exn -> Error exn
        | Error e -> Error e

    let inline executeWithReader ( state : OrmState ) ( transaction : DbTransaction option ) sql ( readerFunction : IDataReader -> 't ) = //Result<'t, exn>
        transaction
        |> withTransaction 
            state
            ( fun transaction -> 
                seq {
                    use cmd = makeCommand state sql <| transaction.Connection 
                    cmd.Transaction <- transaction
                    use reader = cmd.ExecuteReader( )
                    yield! readerFunction reader
                } 
            )
            ( fun connection -> 
                seq {
                    connection.Open( )
                    use cmd = makeCommand state sql connection 
                    use reader = cmd.ExecuteReader( CommandBehavior.CloseConnection )
                    yield! readerFunction reader
                    connection.Close()
                }
            )

            
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

    let inline unwrapOption ( tmp : DbParameter ) ( opt : obj ) ( ) = 
        match opt with 
        | :? Option<Boolean> as t -> tmp.Value <- t |> Option.get
        | :? Option<Byte> as t -> tmp.Value <- t |> Option.get
        | :? Option<Char> as t -> tmp.Value <- t |> Option.get
        | :? Option<DateTime> as t -> tmp.Value <- t |> Option.get
        | :? Option<Decimal> as t -> tmp.Value <- t |> Option.get
        | :? Option<Double> as t -> tmp.Value <- t |> Option.get
        | :? Option<Int16> as t -> tmp.Value <- t |> Option.get
        | :? Option<Int32> as t -> tmp.Value <- t |> Option.get
        | :? Option<Int64> as t -> tmp.Value <- t |> Option.get
        | :? Option<Int128> as t -> tmp.Value <- t |> Option.get
        | :? Option<SByte> as t -> tmp.Value <- t |> Option.get
        | :? Option<Single> as t -> tmp.Value <- t |> Option.get
        | :? Option<String> as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt16> as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt32> as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt64> as t -> tmp.Value <- t |> Option.get
        | :? Option<UInt128> as t -> tmp.Value <- t |> Option.get
        | _ -> ()

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
            // let head = Array.head maps
            
            Array.map (fun map -> 
                let secCol = map.JoinOn |> Option.get |> snd 
                $"{qoute source}.{qoute secCol} = {map.QuotedSource}.{map.QuotedSqlName}"
            ) maps
            |> String.concat " and "
            |> fun onString -> $"left join {qoute source} on {onString}"
        )
        |> String.concat "\n"
        // |> Array.map ( fun (primary, (secSource, secTable)) -> $"{secSource}.{secTable} = {primary}" ) 
        //?=> [idMap1, idMap2, infoSecondaryMap] => [ [idMap1, infoSecondaryMap], [idMap2] ]
        //=> ["UserSecrets.userId = User.id", "UserInfo.userId = User.id"]

        // left join UserInfo on UserInfo.userId = User.cola and UserInfo.ident = User.infoSecondary
        // left join UserSecrets on UserSecrets.userId = User.id
        // ...
    
    let inline queryBase< ^T > ( state : OrmState ) = 
        let cols = columns< ^T > state 
        let joins = joins<^T> state 
        // let sources = 
        ( String.concat ", " cols ) + " from " + table< ^T > state
        + " " + joins
    let inline private select< ^T > ( state : OrmState ) (transaction : DbTransaction option) query = 
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
    
    let inline selectLimit< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) lim = 
        selectHelper< ^T > state transaction ( fun x -> 
            match state with 
            | MSSQL _ -> $"select top {lim} {x}" 
            | _ -> $"select {x} limit {lim}" 
        ) 

    let inline selectWhere< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) where   = 
        selectHelper< ^T > state transaction ( fun x -> $"select {x} where {where}" ) 
        
    let inline selectAll< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) = 
        selectHelper< ^T > state transaction ( fun x -> $"select {x}" ) 

        
    let inline makeParameter ( state : OrmState ) : DbParameter =
        match state with
        | MSSQL _ -> SqlParameter( )
        | MySQL _ -> MySqlParameter( )
        | PSQL _ -> NpgsqlParameter( )
        | SQLite _ -> SqliteParameter( )
    
    
    let inline insert< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) insertKeys ( instance : ^T ) =
        let query = insertBase< ^T > state insertKeys 
        let paramChar = getParamChar state 
        transaction 
        |> withTransaction 
            state 
            (
                fun transaction ->
                    use command = parameterizeCommand state query (transaction.Connection) instance //makeCommand query conn state
                    log (fun _ -> 
                        printfn "Param count: %A" command.Parameters.Count
                        for i in [0..command.Parameters.Count-1] do 
                            printfn "Param %d - %A: %A" i command.Parameters[i].ParameterName command.Parameters[i].Value
                    )  
                    command.Transaction <- transaction
                    command.ExecuteNonQuery ( ) 
            )
            (
                fun connection ->
                    connection.Open( )
                    let query = insertBase< ^T > state insertKeys 
                    let paramChar = getParamChar state 
                    use command = parameterizeCommand state query connection instance //makeCommand query connection state
                    log (fun _ -> 
                        printfn "Param count: %A" command.Parameters.Count
                        for i in [0..command.Parameters.Count-1] do 
                            printfn "Param %d - %A: %A" i command.Parameters[i].ParameterName command.Parameters[i].Value
                    )   
                    let result = command.ExecuteNonQuery ( )
                    connection.Close( )
                    result
            )
            
    let inline insertMany< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) insertKeys ( instances : ^T seq ) =
        let query = insertBase< ^T > state insertKeys 
        transaction
        |> withTransaction 
            state 
            ( fun transaction ->  
                parameterizeSeqAndExecuteCommand state query transaction instances //makeCommand query connection state
            )
            ( fun connection -> 
                connection.Open()
                use transaction = connection.BeginTransaction()
                parameterizeSeqAndExecuteCommand state query transaction instances //makeCommand query connection state
                |> fun x -> transaction.Commit();connection.Close(); x
            )

    (*
    uPDATE a
        ...
        FROM my_table a
        ...    
    *)
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
        
    let inline update< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( instance: ^T ) = 
        let table = table< ^T > state 
        let paramChar = getParamChar state
        
        ensureId< ^T > state 
        |> Result.bind (fun sqlMapping ->
            sqlMapping
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> sprintf "%s.%s = %s%s" table x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun idConditional -> updateHelper< ^T > state transaction ( sprintf " where %s" idConditional ) instance 
        )
        
    let inline updateMany< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( instances: ^T seq )  = 
        // Array.map ( fun instance -> update<^T> state transaction instance ) instances 
        let table = table<^T> state
        let paramChar = getParamChar state
        
        ensureId< ^T > state 
        |> Result.bind (fun sqlMapping ->
            sqlMapping
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> sprintf "%s.%s = %s%s" table x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun idConditional -> updateManyHelper< ^T > state transaction ( sprintf " where %s" idConditional ) instances 
        )





    let inline updateWhere< ^T > ( state : OrmState ) transaction ( where : string ) ( instance: ^T )  = 
        updateHelper< ^T > state transaction ( sprintf " where %s" where ) instance 
        
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
        
    let inline delete< ^T > state ( transaction : DbTransaction option )  instance = 
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            sqlMapping
            |> Array.filter ( fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> sprintf "%s.%s = %s%s" tableName x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun where -> deleteHelper< ^T > state transaction where instance  
        )

    let inline deleteMany< ^T > state ( transaction : DbTransaction option ) instances  =
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            sqlMapping
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> sprintf "%s.%s = %s%s" tableName x.QuotedSqlName paramChar x.FSharpName)
            |> String.concat " and " // id1 = @id1 AND id2 = @id2
            |> fun where -> deleteManyHelper< ^T > state transaction where instances 
        )        
        
    /// <Warning> Running this function is equivalent to DELETE 
    /// FROM table WHERE whereClause </Warning>
    let inline deleteWhere< ^T > state ( transaction : DbTransaction option ) whereClause = 
        let query =  (deleteBase< ^T > state) + whereClause
        transaction 
        |> withTransaction
            state
            ( fun transaction -> 
                use cmd = makeCommand state query ( transaction.Connection ) 
                cmd.Transaction <- transaction
                cmd.ExecuteNonQuery ( )
            )
            ( fun connection -> 
                connection.Open( )
                use cmd = makeCommand state query connection 
                cmd.ExecuteNonQuery ( )
                |> fun res -> connection.Close(); res
            )

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
