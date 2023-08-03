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

    let inline tableName< ^T > ( state : OrmState ) = 
        let attrs =
            typedefof< ^T >.GetCustomAttributes( typeof< TableAttribute >, false )
            |> Array.map ( fun x -> x :?> DbAttribute )
        
        let name = 
            if attrs = Array.empty then
                typedefof< ^T >.Name
            else 
                attrFold attrs ( context< ^T > state )
        
        name.Split( "." )
        |> Array.map ( fun x -> sqlQuote state x )
        |> String.concat "."

    let inline columnMapping< ^T > ( state : OrmState ) = 
        FSharpType.GetRecordFields typedefof< ^T > 
        |> Array.mapi ( fun i x -> 
            let sqlName =  
                x.GetCustomAttributes( typeof< ColumnAttribute >, false ) 
                |> Array.map ( fun y -> y :?> DbAttribute )
                |> fun y -> attrFold y ( context< ^T > state )  //attributes< ^T, ColumnAttribute> state
                |> fun y -> if y = "" then x.Name else y 
            let isKey =
                x.GetCustomAttributes( typeof< PrimaryKeyAttribute >, false ) 
                |> Array.map ( fun y -> y :?> DbAttribute )
                |> fun y -> attrFold y ( context< ^T > state )  //attributes< ^T, ColumnAttribute> state
                |> fun y -> if y = "" then false else true 
            let isIndex =
                x.GetCustomAttributes( typeof< IdAttribute >, false ) 
                |> Array.map ( fun y -> y :?> DbAttribute )
                |> fun y -> attrFold y ( context< ^T > state )  //attributes< ^T, ColumnAttribute> state
                |> fun y -> if y = "" then false else true 
            let on = 
                x.GetCustomAttributes( typeof< OnAttribute >, false ) 
                |> Array.map ( fun y -> y :?> OnAttribute )
                |> fun y -> attrJoinFold y ( context< ^T > state )  //attributes< ^T, ColumnAttribute> state
                |> fun (y : (string * string)) -> if y = ("", "") then None else Some y
            let source =
                x.GetCustomAttributes( typeof< ByJoinAttribute >, false ) 
                |> Array.map ( fun y -> y :?> DbAttribute )
                |> fun y -> attrFold y ( context< ^T > state )  //attributes< ^T, ColumnAttribute> state
                |> fun y -> if y = "" then tableName< ^T > state else sqlQuote state y
            let fsharpName = x.Name
            let quotedName = sqlQuote state sqlName 
            { 
                Index = i
                IsKey = isKey
                IsIndex = isIndex
                JoinOn = on 
                Source = source
                QuotedSource = source
                SqlName = sqlName
                QuotedSqlName = quotedName
                FSharpName = fsharpName
                Type = x.PropertyType 
                PropertyInfo = x
            } 
        )
        
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
    
    let inline toOption ( type_: Type ) ( value: obj ) =
        let tag, variable = if DBNull.Value.Equals( value ) then 0, [||] else 1, [|value|]
        let optionType = typedefof<Option<_>>.MakeGenericType( [|type_|] )
        let case = FSharpType.GetUnionCases( optionType ) |> Seq.find ( fun info -> info.Tag = tag )
        FSharpValue.MakeUnion( case, variable )

    let inline optionType ( type_ : Type )  =
        if type_.IsGenericType && type_.GetGenericTypeDefinition( ) = typedefof<Option<_>>
        then Some ( type_.GetGenericArguments( ) |> Array.head ) // optionType Option<User> -> User  
        else None

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

    ///<Description> Takes a reader of type IDataReader and a state of type OrmState -> consumes the reader and returns a sequence of type ^T.</Description>
    let inline consumeReader< ^T > ( state : OrmState ) ( reader : IDataReader ) = 
        // Heavily inspired by http://www.fssnip.net/gE -- thanks igeta!
        let rty = typeof< ^T >
        let makeEntity vals = FSharpValue.MakeRecord( rty, vals ) :?>  ^T
        let fields = 
            seq { for fld in ( columnMapping< ^T > state |> Seq.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) ) -> fld.SqlName, fld } // sqlSource User.name -> "UserInfo"  //! Filter out joins for non-select queries
            |> dict 
        seq { 
            while reader.Read( ) do
                yield 
                    seq { 0..reader.FieldCount-1 }
                    |> Seq.map ( fun i -> reader.GetName( i ), reader.GetValue( i ) ) //get SQL column name and value => (name, value)
                    |> Seq.sortBy ( fun ( name, _ ) ->  fields[name].Index ) //constructor is positional, need to add values in correct order
                    |> Seq.map ( fun ( name, value ) ->   
                        match optionType fields[name].Type with //handle option type, i.e. option<T> if record field is optional, else T
                        | Some ``type`` -> toOption ``type`` value
                        | None -> value
                    ) // => seq { Some 1; "field 2 value"; None; etc}
                    |> Seq.toArray // record constructor requires an array
                    |> makeEntity // dang ol' class factory man
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
            |> Seq.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries 
            |> Seq.filter (fun col -> insertKeys || not col.IsKey )
        let placeHolders = 
            cols
            |> Seq.mapi ( fun i x -> sprintf "%s%s" paramChar x.FSharpName )
            |> String.concat ", "
        let columnNames = 
            cols
            |> Seq.map ( fun x -> x.QuotedSqlName )
            |> String.concat ", "  
        
        sprintf "insert into %s ( %s ) values ( %s )" tableName columnNames placeHolders

    //Insert Into table1 Values
    // ( $1, $2, $3 ),
    // ( $4, $5, $6 ),
    let inline insertManyBase< ^T > ( state : OrmState ) insertKeys ( instances : ^T seq )  =
        let paramChar   = getParamChar     state
        let tableName   = table< ^T >       state 
        let cols = 
            mapping< ^T > state 
            |> Seq.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName  ) //! Filter out joins for non-select queries
            |> Seq.filter (fun col -> insertKeys || not col.IsKey )
        let columns     = columns< ^T >     state
        let placeHolders = 
            instances 
            |> Seq.mapi ( fun index e ->
                cols
                |> Seq.mapi ( fun innerIndex x -> 
                    sprintf "%s%s%i" paramChar x.FSharpName index
                )
                |> String.concat ", "
            )
            |> String.concat " ), ( "
        let columnNames = 
            cols 
            |> Seq.map ( fun x -> x.QuotedSqlName )
            |> String.concat ", " 
        //placeHolders e.g. = "@cola1,@colb1),(@cola2,@colb2),(@cola3,@colb3"
        sprintf "insert into %s( %s ) values ( %s );"  tableName columnNames placeHolders
    
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

    let inline execute ( state : OrmState ) sql =
        withTransaction 
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
        
        // function 
        // | Some ( tran : DbTransaction ) ->
        //     withTransaction db 
        //         use cmd = makeCommand state sql ( tran.Connection )
        //         let result = cmd.ExecuteNonQuery( tran )
        //     Ok result
        // | None ->  
        //     match connect state with 
        //     | Ok conn -> 
        //         conn.Open( )
                
        //         conn.Close( )
        //         Ok result
        //     | Error e -> Error e
    
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
                cmd.ExecuteReader( )
                |> Ok
            with 
            | exn -> Error exn
        | Error e -> Error e

    let inline executeWithReader( state : OrmState )  sql ( readerFunction : IDataReader -> 't ) = //Result<'t, exn>
        withTransaction 
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
                    use reader = cmd.ExecuteReader ( )
                    yield! readerFunction reader
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
        
        mapping< ^T > state
        |> Seq.filter (fun mappedInstance -> mappedInstance.QuotedSource = (tableName< ^T > state)  ) //! Filter out joins for non-select queries
        |> Seq.iter ( fun mappedInstance -> 
            let paramChar = getParamChar state
            let formattedParam = 
                sprintf "%s%s" paramChar mappedInstance.FSharpName
            let param = 
                let mutable tmp = cmd.CreateParameter( )

                tmp.ParameterName <- formattedParam 
                if
                    mappedInstance.PropertyInfo.GetValue( instance ) = null 
                then
                    tmp.IsNullable <- true
                    tmp.Value <- DBNull.Value
                else 
                    let _type = mappedInstance.Type
                        
                    if genericTypeName false _type = "FSharpOption"
                    then
                        tmp.IsNullable <- true
                        unwrapOption tmp (mappedInstance.PropertyInfo.GetValue( instance )) ()
                        
                    else
                        tmp.Value <- 
                            mappedInstance.PropertyInfo.GetValue( instance ) // Some 1
                tmp

            cmd.Parameters.Add ( param ) |> ignore
        )
        
        cmd

    let inline parameterizeSeqCommand< ^T > state query conn ( instances : ^T seq ) =
        let cmd = makeCommand state query conn
        
        instances 
        |> Seq.iteri ( fun index instance ->
        
            mapping< ^T > state
            |> Seq.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Seq.iter ( fun mappedInstance -> 
                let paramChar = getParamChar state
                let formattedParam = 
                    sprintf "%s%s%i" paramChar mappedInstance.FSharpName index //``the instance formerly known as mappedInstance``
                let param = 
                    let mutable tmp = cmd.CreateParameter( ) 
                    
                    tmp.ParameterName <- formattedParam 
                    if
                        mappedInstance.PropertyInfo.GetValue( instance ) = null 
                    then
                        tmp.IsNullable <- true
                        tmp.Value <- DBNull.Value
                    else 
                        let _type = mappedInstance.Type
                        if genericTypeName false _type = "FSharpOption"
                        then
                            tmp.IsNullable <- true
                            unwrapOption tmp (mappedInstance.PropertyInfo.GetValue( instance )) ()
                            
                        else
                            tmp.Value <- 
                                mappedInstance.PropertyInfo.GetValue( instance ) // Some 1
                    tmp

                cmd.Parameters.Add ( param ) |> ignore
            )
        )
        
        cmd
    
    // type User = 
    //     {
    //         [<On(typeof<UserInfo>, "userId", JoinDirection.Left, DbContext.Default)>]
    //         [<On(typeof<UserSecrets>, "userId", JoinDirection.Left, DbContext.Default)>]
    //         id : string 
    //         [<On(typeof<UserInfo>, "ident", JoinDirection.Left, DbContext.Default)>]
    //         infoSecondary : int 
    //         [<ByJoin(typeof<UserInfo>, DbContext.Default)>]
    //         name : string 
    //         [<ByJoin(typeof<UserSecrets>, DbContext.Default)>]
    //         password : string 
    //     }

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
            |> fun onString -> $"join {qoute source} on {onString}"
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
    let inline private select< ^T > ( state : OrmState ) query = 
        withTransaction  
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
                    use reader = cmd.ExecuteReader( ) 
                    yield! consumeReader< ^T > state reader  
                }
                
            )

    let inline selectHelper< ^T > ( state : OrmState ) f = 
        queryBase< ^T > state
        |> f
        |> select< ^T > state 
    
    let inline selectLimit< ^T > ( state : OrmState ) lim = 
        selectHelper< ^T > state ( fun x -> $"select top {lim} {x}" ) 

    let inline selectWhere< ^T > ( state : OrmState ) where   = 
        selectHelper< ^T > state ( fun x -> $"select {x} where {where}" ) 
        
    let inline selectAll< ^T > ( state : OrmState ) = 
        selectHelper< ^T > state ( fun x -> $"select {x}" ) 

        
    let inline makeParameter ( state : OrmState ) : DbParameter =
        match state with
        | MSSQL _ -> SqlParameter( )
        | MySQL _ -> MySqlParameter( )
        | PSQL _ -> NpgsqlParameter( )
        | SQLite _ -> SqliteParameter( )
    
    
    let inline insert< ^T > ( state : OrmState ) insertKeys ( instance : ^T ) =
        let query = insertBase< ^T > state insertKeys 
        let paramChar = getParamChar state 
        withTransaction 
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
                    let result =  command.ExecuteNonQuery ( )
                    connection.Close( )
                    result
            )

    let inline insertMany< ^T > ( state : OrmState ) insertKeys ( instances : ^T seq ) =
        let paramChar = ( getParamChar state )
        let numCols = columns< ^T > state |> Seq.length
        let query = insertManyBase< ^T > state insertKeys instances 
        withTransaction 
            state 
            ( fun transaction ->  
                use command = parameterizeSeqCommand state query ( transaction.Connection ) instances //makeCommand query connection state
                log (fun _ -> 
                    printfn "Query generated: %s" query
                    printfn "Param count: %A" command.Parameters.Count
                    for i in [0..command.Parameters.Count-1] do 
                        printfn "Param %d - %A: %A" i command.Parameters[i].ParameterName command.Parameters[i].Value
                )   
                command.Transaction <- transaction
                command.ExecuteNonQuery ( )
            )
            ( fun connection -> 
                connection.Open( )
                use command = parameterizeSeqCommand state query connection instances //makeCommand query connection state
                log (fun _ -> 
                    printfn "Query generated: %s" query
                    printfn "Param count: %A" command.Parameters.Count
                    for i in [0..command.Parameters.Count-1] do 
                        printfn "Param %d - %A: %A" i command.Parameters[i].ParameterName command.Parameters[i].Value
                )   
                let result = command.ExecuteNonQuery ( )
        
                connection.Close( )
                result 
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
            |> Seq.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Seq.filter (fun col -> not col.IsKey) //Can't update keys
        log ( fun _ -> printfn "columns to update: %A" cols )
        let queryParams = 
            cols 
            |> Seq.map (fun col -> pchar + col.FSharpName ) // @col1, @col2, @col3
            

        let table = table< ^T > state 
        let set = 
            Seq.zip cols queryParams
            |> Seq.map ( fun x -> sprintf "%s = %s" (fst x).QuotedSqlName (snd x) ) 
            |> String.concat ", "

        "update " + table + " set " + set 

    let inline ensureId< ^T > ( state: OrmState ) = 
        mapping< ^T > state 
        |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
        |> Array.filter ( fun x -> x.IsKey )
        |> fun x -> if Array.length x = 0 then "Record must have at least one ID attribute specified..." |> exn |> Error else Ok x
    
    let inline updateHelper<^T> ( state : OrmState ) ( whereClause : string ) ( instance : ^T ) = 
        let query = ( updateBase< ^T > state ) + whereClause 
        withTransaction 
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
        
        
    let inline update< ^T > ( state : OrmState ) ( instance: ^T ) transaction = 
        let table = table< ^T > state 
        let paramChar = getParamChar state
        
        ensureId< ^T > state 
        |> Result.bind (fun sqlMapping ->
            sqlMapping
            |> Seq.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Seq.map ( fun x -> sprintf "%s.%s = %s%s" table x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun idConditional -> updateHelper< ^T > state ( sprintf " where %s" idConditional ) instance transaction
        )
        
    let inline updateMany< ^T > ( state : OrmState ) ( instances: ^T seq ) transaction = 
        Seq.map ( fun instance -> update<^T> state instance transaction ) instances 
        
    let inline updateWhere< ^T > ( state : OrmState ) ( where : string ) ( instance: ^T )  = 
        updateHelper< ^T > state ( sprintf " where %s" where ) instance 
        
    let inline deleteBase< ^T > state =
        table< ^T > state 
        |> sprintf "delete from %s where "

    let inline deleteHelper< ^T > ( state : OrmState ) ( whereClause : string ) ( instance : ^T ) =
        let query = deleteBase< ^T > state + whereClause 
        withTransaction 
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
    
    let inline deleteManyHelper< ^T > ( state : OrmState ) ( whereClause : string ) ( instances : ^T seq ) =
        let query = deleteBase< ^T > state + whereClause 
        withTransaction 
            state 
            ( fun transaction -> 
                use command = parameterizeSeqCommand< ^T > state query ( transaction.Connection ) instances 
                command.Transaction <- transaction
                command.ExecuteNonQuery ( )        
            )
            ( fun connection -> 
                connection.Open( )
                use cmd = parameterizeSeqCommand< ^T > state query connection instances 
                let result = cmd.ExecuteNonQuery ( )
                connection.Close()
                result
            )
        
    let inline delete< ^T > state instance transaction = 
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            sqlMapping
            |> Seq.filter ( fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries
            |> Seq.map ( fun x -> sprintf "%s.%s = %s%s" tableName x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun where -> deleteHelper< ^T > state where instance transaction 
        )

    let inline deleteMany< ^T > state instances transaction =
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            instances 
            |> Seq.mapi ( fun i _ ->
                sqlMapping
                |> Seq.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries
                |> Seq.map ( fun x -> sprintf "%s.%s = %s%s%i" tableName x.QuotedSqlName paramChar x.FSharpName i)
                |> String.concat " and "
            
            )
            |> String.concat ") OR ("
            |> sprintf "( %s )" 
            |> fun where -> deleteManyHelper< ^T > state where instances transaction
        )        
        
    /// <Warning> Running this function is equivalent to DELETE 
    /// FROM table WHERE whereClause </Warning>
    let inline deleteWhere< ^T > state whereClause  = 
        let query =  (deleteBase< ^T > state) + whereClause
        withTransaction
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

    let inline lookupId<^S> state : string seq =
        columnMapping<^S> state
        |> Seq.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^S > state  ) //! Filter out joins for non-select queries
        |> Seq.filter (fun col -> col.IsKey) 
        |> Seq.map (fun keyCol -> keyCol.QuotedSqlName)

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
                selectWhere<^S> state whereClause None
                |> function 
                | Ok vals when Seq.length vals > 0 ->
                    Some <| Seq.head vals    
                | _ -> 
                    Option.None 
                |> fun (v : option<'S>) -> { inst with value = v}
