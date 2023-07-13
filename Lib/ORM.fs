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


    let inline sqlQuote str ( state : OrmState ) =
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
        |> Array.map ( fun x -> sqlQuote x state )
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
            let fsharpName = x.Name
            let quotedName = sqlQuote sqlName state
            { 
                Index = i
                IsKey = isKey
                IsIndex = isIndex
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
        |> Array.map ( fun x -> x.QuotedSqlName )
       
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
            
    
    ///<Description> Takes a reader of type IDataReader and a state of type OrmState -> consumes the reader and returns a sequence of type ^T.</Description>
    let inline consumeReader< ^T > ( reader : IDataReader )  ( state : OrmState ) = 
        // Heavily inspired by http://www.fssnip.net/gE -- thanks igeta!
        let rty = typeof< ^T >
        let makeEntity vals = FSharpValue.MakeRecord( rty, vals ) :?>  ^T
        let fields = 
            seq { for fld in ( columnMapping< ^T > state ) -> fld.SqlName, fld } 
            |> dict 
        seq { 
            while reader.Read( ) do
                yield 
                    seq { 0..reader.FieldCount-1 }
                    |> Seq.map ( fun i -> reader.GetName( i ), reader.GetValue( i ) )
                    |> Seq.sortBy ( fun ( name, _ ) ->  fields[name].Index )
                    |> Seq.map ( fun ( name, value ) -> 
                        match optionType fields[name].Type with
                        | Some ``type`` -> toOption ``type`` value
                        | None   -> value
                    )
                    |> Seq.toArray
                    |> makeEntity 
        } 
    
    // [Ok Articles; Ok User;]

    // type Query = Context -> DbConnection -> DbTransaction -> ( string | DbCommand ) seq -> Result<IDataReader, exn> seq
    // type Command = Context -> DbConnection -> DbTransaction -> ( string | DbCommand ) seq -> Result<int, exn> seq
    // type Query = DbCommand -> DbConnection -> DbTransaction -> IDataReader | int
    // module Unbatchable =
    //     let selectAll<^T> db =
    //         Orm.selectAll<^T> db |> executeAndConsume<^T> // -> Result<seq<User>, exn>
    // insert<User> user db |> execute // -> Result<int, exn> 

    // batch [insert<Article> article; insert<ArticleUser> articleUser; selectWhere<> "whatever"] db // -> Result 
    (*
        fun context -> 
            fun connection ->
                fun transaction ->
                    fun querys ->
                        let cmds = querys |> Seq.map toCmd
                        let mutable errored = false
                        seq { for cmd in cmds do 
                            if not errored 
                            then 
                                match cmd.Execute() with 
                                | Ok state -> 
                                    yield Ok state 
                                | Error e -> 
                                    errored <- true
                                    transaction.Rollback()
                                    yield Error e
                                    
                                [Ok 1; Ok IDataReader; Error e]
                        }
                        type QueryReturn =
                            | int
                            | IDataReader
                        
                        |> Seq.takeWhile
                            match query with 
                            | str -> buildCommand(str)
                            | cmd -> ()
                            |> fun cmd -> cmd.Execute
                            |> function 
                            | Ok _ -> true
                            | Error _ -> 
                                transaction.Rollback()
                                false
                        |> Seq.fold (
                            fun element state ->
                                match state with 
                                | Ok state ->
                                    state @ element.Execute() |> Ok
                                | Error e -> 
                                    transaction.Rollback
                                    Error e
                        [Ok 1; Ok IDataReader; Error exn; Error exn]
                        ) (Ok 0)
    *)                       

    // consumeReaders : seq<QueryResults> -> seq<>
    //     match input with 
    //     | IDataReader ->
    //         consume it
    //     | int ->
    //        Error exn "Can't consume int"
    //     [Ok 1]
    // type QueryResult =
    //     | Int of int 
    //     | DataReader of IDataReader * Type
    //     | Thing of obj * Type
    // batch seq {insert<User> user db; selectWhere<User> "id = 1" db, User; delete<User> user db} // [Int 1; (DataReader dr, User); (DataReader dr, Article); Int 1]
    // |> map 
    //     function
    //     | Ok el ->
    //         match el with 
    //         | Int i -> Int i
    //         | DataReader (dr,type_) -> Thing <| consumeReader<type_> dr, type_  
    
    // match Unbatchable.selectAll<User> db with 
    // | Ok result -> 
    //     match result with 
    //     | Int i -> 
    //     | Reader reader -> consumeReader<User> reader
    // | Error e -> Error e

    // Batchable.selectAll<User> db
    

    let inline getParamChar state = 
        match state with
        | MSSQL _ -> "@"
        | MySQL _ -> "$"
        | PSQL _ -> "@"
        | SQLite _ -> "@"   
        
    let inline insertBase< ^T > insertKeys ( state : OrmState ) =
        let paramChar = getParamChar state
        let tableName = ( table< ^T > state ) 
        let cols = 
            mapping< ^T > state 
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
    let inline insertManyBase< ^T > insertKeys ( instances : ^T seq ) ( state : OrmState ) =
        let paramChar   = getParamChar     state
        let tableName   = table< ^T >       state 
        let cols = 
            mapping< ^T > state 
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
    
    let inline makeCommand ( query : string ) ( connection : DbConnection )  ( state : OrmState ) : DbCommand = 
        log (fun _ -> printfn "Query being generated:\n\n%s\n\n\n" query )
        match state with 
        | MSSQL _ -> new SqlCommand ( query, connection :?> SqlConnection )
        | MySQL _ -> new MySqlCommand ( query, connection :?> MySqlConnection )
        | PSQL _ -> new NpgsqlCommand ( query, connection :?> NpgsqlConnection )
        | SQLite _ -> new SqliteCommand ( query, connection :?> SqliteConnection )

    let inline execute sql  ( state : OrmState ) =
        match connect state with 
        | Ok conn -> 
            conn.Open( )
            use cmd = makeCommand sql conn state
            let result = cmd.ExecuteNonQuery( )
            conn.Close( )
            Ok result
        | Error e -> Error e
    
    ///<Description>
    /// Takes a function of IDataReader -> Result< 't seq, exn> (see FORMs consumeReader function as example) to 
    /// transfer the results of executing the specified sql against the specified database given by state into an 
    /// arbitrary type 't, defined by you in the readerFunction.
    /// </Description>
    let inline executeWithReader sql ( readerFunction : IDataReader -> 't ) ( state : OrmState ) = //Result<'t, exn>
        match connect state with
        | Ok conn -> 
            try 
                conn.Open( )
                seq { 
                    use cmd = makeCommand sql conn state
                    use reader = cmd.ExecuteReader( CommandBehavior.CloseConnection )
                    while reader.Read() do yield readerFunction reader 
                } |> Ok
            with 
            | exn -> Error exn
        | Error e -> Error e
    
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

    let inline parameterizeCmd< ^T > query conn ( instance : ^T ) state =
        let cmd = makeCommand query conn state
        
        printfn "Type %A - %A" typeof<^T> (mapping< ^T > state)
        mapping< ^T > state
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

    let inline parameterizeSeqCmd< ^T > query conn ( instances : ^T seq ) state =
        let cmd = makeCommand query conn state
        
        instances 
        |> Seq.iteri ( fun index instance ->
        
            mapping< ^T > state
            |> Seq.iter ( fun mappedInstance -> 
                let paramChar = getParamChar state
                let formattedParam = 
                    sprintf "%s%s%i" paramChar mappedInstance.FSharpName index //``the instance formerly known as mappedInstance``
                let param = 
                    let mutable tmp = cmd.CreateParameter( ) //cmd.CreateParameter( )
                    
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
                
                // param.ParameterName <- 
                //     sprintf 
                //         "%s%s%i" 
                //         ( getParamChar state ) 
                //         mappedInstance.FSharpName 
                //         index
                
                // if mappedInstance.PropertyInfo.GetValue( instance ) = null 
                // then 
                //     param.IsNullable <- true
                //     param.Value <- DBNull.Value
                // else 
                //     param.Value <- 
                //         ( mappedInstance.PropertyInfo.GetValue( instance ) )

                cmd.Parameters.Add ( param ) |> ignore
            )
        )
        
        cmd
        
    
    let inline queryBase< ^T >  ( state : OrmState ) = 
        let cols = columns< ^T > state 
        ( String.concat ", " cols ) + " From " + table< ^T > state

    let inline exceptionHandler f =
        try 
            Ok <| f( )
        with 
        | exn -> Error exn

    let inline private select< ^T > query ( state : OrmState ) = 
        match connect state with 
        | Ok conn -> 
            exceptionHandler ( fun ( ) ->  
                conn.Open( )   
                seq {
                    use cmd = makeCommand query conn state
                    use reader = cmd.ExecuteReader( ) // CommandBehavior.CloseConnection
                    yield! consumeReader< ^T > reader state 
                }
            )
        | Error e -> Error e


    let inline selectHelper< ^T > f ( state : OrmState ) = 
        queryBase< ^T > state
        |> f
        |> fun x -> select< ^T > x state
    
    let inline selectLimit< ^T > lim  ( state : OrmState ) = 
        selectHelper< ^T > ( fun x -> $"select top {lim} {x}" ) state

    let inline selectWhere< ^T > where  ( state : OrmState ) = 
        selectHelper< ^T > ( fun x -> $"select {x} where {where}" ) state
        
    let inline selectAll< ^T >  ( state : OrmState ) = 
        selectHelper< ^T > ( fun x -> $"select {x}" ) state

        
    let inline makeParameter ( state : OrmState ) : DbParameter =
        match state with
        | MSSQL _ -> SqlParameter( )
        | MySQL _ -> MySqlParameter( )
        | PSQL _ -> NpgsqlParameter( )
        | SQLite _ -> SqliteParameter( )
    
    
    let inline insert< ^T > insertKeys ( instance : ^T )  ( state : OrmState ) =
        match connect state with
        | Ok conn ->
            conn.Open( )
            let query = insertBase< ^T > insertKeys state
            let paramChar = getParamChar state 
            use cmd = parameterizeCmd query conn instance state//makeCommand query conn state
            log (fun _ -> 
                printfn "Param count: %A" cmd.Parameters.Count
                for i in [0..cmd.Parameters.Count-1] do 
                    printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
            )   
            let result = exceptionHandler ( fun ( ) -> cmd.ExecuteNonQuery ( ) )
            conn.Close( )
            result
        | Error e -> Error e

    let inline insertMany< ^T > insertKeys ( instances : ^T seq )  ( state : OrmState ) =
        match connect state with
        | Ok conn -> 
            conn.Open( )
            let query = insertManyBase< ^T > insertKeys instances state
            let paramChar = ( getParamChar state )
            let numCols = columns< ^T > state |> Seq.length
            use cmd = parameterizeSeqCmd query conn instances state//makeCommand query conn state
            log (fun _ -> 
                printfn "Query generated: %s" query
                printfn "Param count: %A" cmd.Parameters.Count
                for i in [0..cmd.Parameters.Count-1] do 
                    printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
            )   
            let result = 
                exceptionHandler ( fun ( ) -> cmd.ExecuteNonQuery ( ) ) 
    
            conn.Close( )
            result
        | Error e -> Error e
    
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
        |> Array.filter ( fun x -> x.IsKey )
        |> fun x -> if Array.length x = 0 then "Record must have at least one ID attribute specified..." |> exn |> Error else Ok x
    
    let inline updateHelper<^T> ( whereClause : string ) ( instance : ^T ) ( state : OrmState ) = 
        connect state
        |> Result.bind ( fun conn -> 
            exceptionHandler ( fun ( ) ->  
                let query =  updateBase< ^T > state + whereClause //" where " + idConditional    
                // let paramChar = getParamChar state

                conn.Open( )
                // use cmd = makeCommand query conn state
                
                // mapping< ^T > state
                // |> Seq.iter ( fun x -> 
                //     let formattedParam = 
                //         sprintf "%s%s" paramChar x.FSharpName
                //     let param = 
                //         let mutable tmp = cmd.CreateParameter( )
                //         tmp.ParameterName <- formattedParam 
                        
                //         if x.PropertyInfo.GetValue( instance ) = null 
                //         then 
                //             tmp.IsNullable <- true
                //             tmp.Value <- DBNull.Value
                //         else 
                //             tmp.Value <- ( x.PropertyInfo.GetValue( instance ) )
                        
                //         tmp

                //     cmd.Parameters.Add ( param ) |> ignore
                // )
                
                use cmd = parameterizeCmd< ^T > query conn instance state

                // log (fun _ ->
                //     printfn "Param count: %A" cmd.Parameters.Count
                    
                //     for i in [0..cmd.Parameters.Count-1] do 
                //         printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
                // )

                cmd.ExecuteNonQuery ( )
            )        
        )
        
    let inline update< ^T > ( instance: ^T ) ( state : OrmState ) = 
        (*
            uPDATE tableT
                SET 
                    col1 = instance.col1, 
                    col2 = instance.col2 
                    ... 
                    coln = instance.coln
                WHERE
                    (id_col) = instance.(id_field)
        *)
        let table = table< ^T > state 
        let paramChar = getParamChar state
        
        ensureId< ^T > state 
        |> Result.bind (fun sqlMapping ->
            sqlMapping
            |> Seq.map ( fun x -> sprintf "%s.%s = %s%s" table x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun idConditional -> updateHelper< ^T > ( sprintf " where %s" idConditional ) instance state
        )
        
    let inline updateMany< ^T > ( instances: ^T seq ) ( state : OrmState ) = 
        Seq.map ( fun instance -> update<^T> instance state ) instances 
        
    let inline updateWhere< ^T > ( where : string ) ( instance: ^T ) ( state : OrmState ) = 
        (*
            uPDATE tableT
                SET 
                    col1 = item.col1, 
                    col2 = item.col2 
                    ... 
                    coln = item.coln
                WHERE
                    
        *)
        updateHelper< ^T > ( sprintf " where %s" where )  instance state    
        
    // let inline updatecolumns< ^T > ( cols: PropertyInfo seq ) ( item: ^T ) ( state : OrmState ) = 
        
    let inline deleteBase< ^T > state =
        table< ^T > state 
        |> sprintf "delete from %s where "

    let inline deleteHelper< ^T > ( whereClause : string ) ( instance : ^T ) ( state : OrmState ) =
        connect state 
        |> Result.bind ( fun conn -> 
            exceptionHandler ( fun ( ) ->  
                let query =  deleteBase< ^T > state + whereClause //" where " + idConditional    
                conn.Open( )
                use cmd = parameterizeCmd< ^T > query conn instance state
                
                log ( fun _ ->
                    printfn "Param count: %A" cmd.Parameters.Count
                    for i in [0..cmd.Parameters.Count-1] do 
                        printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
                )
                
                cmd.ExecuteNonQuery ( )
            )        
        )

    let inline deleteManyHelper< ^T > ( whereClause : string ) ( instances : ^T seq ) ( state : OrmState ) =
        connect state 
        |> Result.bind ( fun conn -> 
            exceptionHandler ( fun ( ) ->  
                let query =  deleteBase< ^T > state + whereClause //" where " + idConditional    
                conn.Open( )
                use cmd = parameterizeSeqCmd< ^T > query conn instances state

                log ( fun _ -> 
                    printfn "Param count: %A" cmd.Parameters.Count
                    for i in [0..cmd.Parameters.Count-1] do 
                        printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
                )

                cmd.ExecuteNonQuery ( )
            )        
        ) 
        
    let inline delete< ^T > instance state = 
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            sqlMapping
            |> Seq.map ( fun x -> sprintf "%s.%s = %s%s" tableName x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun where -> deleteHelper< ^T > where instance state
        )

    let inline deleteMany< ^T > instances state =
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            // WHERE (id1 = @id1 AND id2 = @id2) OR (id1 = @id12 AND id2 = @id22)
            instances 
            |> Seq.mapi ( fun i _ ->
                sqlMapping
                |> Seq.map ( fun x -> sprintf "%s.%s = %s%s%i" tableName x.QuotedSqlName paramChar x.FSharpName i)
                |> String.concat " and "
            
            )
            |> String.concat ") OR ("
            |> sprintf "( %s )" 
            |> fun where -> deleteManyHelper< ^T > where instances state
        )        
        
    /// <Warning> Running this function is equivalent to DELETE 
    /// FROM table WHERE whereClause </Warning>
    let inline deleteWhere< ^T > whereClause state = 
        
        connect state 
        |> Result.bind ( fun conn -> 
            exceptionHandler ( fun ( ) ->  
                let query =  deleteBase< ^T > state + whereClause     
                conn.Open( )
                use cmd = makeCommand query conn state

                cmd.ExecuteNonQuery ( )
            )        
        )
    let inline lookupId<^S> state : string seq =
        columnMapping<^S> state
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
                selectWhere<^S> whereClause state 
                |> function 
                | Ok vals when Seq.length vals > 0 ->
                    Some <| Seq.head vals    
                | _ -> 
                    Option.None 
                |> fun (v : option<'S>) -> { inst with value = v}
