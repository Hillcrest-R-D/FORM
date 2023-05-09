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
                x.GetCustomAttributes( typeof< IdAttribute >, false ) 
                |> Array.map ( fun y -> y :?> DbAttribute )
                |> fun y -> attrFold y ( context< ^T > state )  //attributes< ^T, ColumnAttribute> state
                |> fun y -> if y = "" then false else true 
            let fsharpName = x.Name
            let quotedName = sqlQuote sqlName state
            { 
                Index = i
                IsKey = isKey
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
    
    let inline toOption< ^T > ( type_: Type ) ( value: obj )  ( _ : OrmState ) =
        let tag, variable = if DBNull.Value.Equals( value ) then 0, [||] else 1, [|value|]
        let optionType = typedefof<Option<_>>.MakeGenericType( [|type_|] )
        let Case = FSharpType.GetUnionCases( optionType ) |> Seq.find ( fun info -> info.Tag = tag )
        FSharpValue.MakeUnion( Case, variable )

    let inline optionType< ^T > ( type_ : Type )  ( _ : OrmState ) =
        if type_.IsGenericType && type_.GetGenericTypeDefinition( ) = typedefof<Option<_>>
        then Some ( type_.GetGenericArguments( ) |> Array.head ) // optionType Option<User> -> User  
        else None
    
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
                        match optionType< ^T > fields[name].Type state with
                        | Some ``type`` -> toOption< ^T > ``type`` value state
                        | None   -> value
                    )
                    |> Seq.toArray
                    |> makeEntity 
        } 

    let inline getParamChar state = 
        match state with
        | MSSQL _ -> "@"
        | MySQL _ -> "$"
        | PSQL _ -> "@"
        | SQLite _ -> "@"   

    // let inline insertBase< ^T >
        
    let inline insertBase< ^T > ( state : OrmState ) =
        let paramChar = getParamChar state
        let tableName = ( table< ^T > state ) 

        let placeHolders = 
            mapping< ^T > state 
            |> Seq.mapi ( fun i x -> sprintf "%s%s" paramChar x.FSharpName )
            |> String.concat ", "
        let columnNames = String.concat ", "  ( columns< ^T > state )
        
        sprintf "insert into %s ( %s ) values ( %s )" tableName columnNames placeHolders

    //Insert Into table1 Values
    // ( $1, $2, $3 ),
    // ( $4, $5, $6 ),
    let inline insertManyBase< ^T > ( instances : ^T seq ) ( state : OrmState ) =
        let paramChar   = getParamChar     state
        let tableName   = table< ^T >       state 
        let columns     = columns< ^T >     state
        let placeHolders = 
            instances 
            |> Seq.mapi ( fun index e ->
                mapping< ^T > state 
                |> Seq.mapi ( fun innerIndex x -> 
                    sprintf "%s%s%i" paramChar x.FSharpName index
                )
                |> String.concat ", "
            )
            |> String.concat " ), ( "
        let columnNames = String.concat ", " columns
        //placeHolders e.g. = "@cola1,@colb1),(@cola2,@colb2),(@cola3,@colb3"
        sprintf "insert into %s( %s ) values ( %s );"  tableName columnNames placeHolders
    
    let inline makeCommand ( query : string ) ( connection : DbConnection )  ( state : OrmState ) : DbCommand = 
#if DEBUG
        printfn "Query being generated:\n\n%s\n\n\n" query
#endif
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
    let inline executeWithReader sql ( readerFunction : IDataReader -> Result< 't seq, exn > ) ( state : OrmState ) =
        match connect state with
        | Ok conn -> 
            conn.Open( )
            use cmd = makeCommand sql conn state
            use reader = cmd.ExecuteReader( )
            let result = readerFunction reader
            conn.Close( )
            result
        | Error e -> Error e
    
    let inline paramaterizeCmd< ^T > query conn ( instance : ^T ) state =
        let cmd = makeCommand query conn state
        
        mapping< ^T > state
        |> Seq.iter ( fun x -> 
            let paramChar = getParamChar state
            let formattedParam = 
                sprintf "%s%s" paramChar x.FSharpName
            let param = 
                let mutable tmp = cmd.CreateParameter( )
                tmp.ParameterName <- formattedParam 
                
                if x.PropertyInfo.GetValue( instance ) = null 
                then 
                    tmp.IsNullable <- true
                    tmp.Value <- DBNull.Value
                else 
                    tmp.Value <- ( x.PropertyInfo.GetValue( instance ) )
                
                tmp

            cmd.Parameters.Add ( param ) |> ignore
        )
        cmd

    let inline paramaterizeSeqCmd< ^T > query conn ( instances : ^T seq ) state =
        let cmd = makeCommand query conn state
        
        instances 
        |> Seq.iteri ( fun index instance ->
        
            mapping< ^T > state
            |> Seq.iteri ( fun _ mappedInstance -> 
            
                let mutable param = cmd.CreateParameter( )
                param.ParameterName <- 
                    sprintf 
                        "%s%s%i" 
                        ( getParamChar state ) 
                        mappedInstance.FSharpName 
                        index
                
                if mappedInstance.PropertyInfo.GetValue( instance ) = null 
                then 
                    param.IsNullable <- true
                    param.Value <- DBNull.Value
                else 
                    param.Value <- ( mappedInstance.PropertyInfo.GetValue( instance ) )

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
    
    
    let inline insert< ^T > ( instance : ^T )  ( state : OrmState ) =
        match connect state with
        | Ok conn ->
            conn.Open( )
            let query = insertBase< ^T > state
            let paramChar = getParamChar state 
            use cmd = paramaterizeCmd query conn instance state//makeCommand query conn state
            
#if DEBUG
            printfn "Param count: %A" cmd.Parameters.Count
            
            for i in [0..cmd.Parameters.Count-1] do 
                printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
#endif

            let result = exceptionHandler ( fun ( ) -> cmd.ExecuteNonQuery ( ) )
            conn.Close( )
            result
        | Error e -> Error e

    let inline insertMany< ^T > ( instances : ^T seq )  ( state : OrmState ) =
        match connect state with
        | Ok conn -> 
            conn.Open( )
            let query = insertManyBase< ^T > instances state
            let paramChar = ( getParamChar state )
            let numCols = columns< ^T > state |> Seq.length
            use cmd = paramaterizeSeqCmd query conn instances state//makeCommand query conn state

#if DEBUG
            printfn "Query generated: %s" query
            printfn "Param count: %A" cmd.Parameters.Count
            
            for i in [0..cmd.Parameters.Count-1] do 
                printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
#endif
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
        let queryParams = 
            cols 
#if DEBUG
            |> fun cols -> 
                printfn "columns to update: %A" cols; cols 
#endif
            |> Seq.map (fun col -> pchar + col.FSharpName ) // @col1, @col2, @col3

        let table = table< ^T > state 
        let set = 
            Seq.zip cols queryParams
            |> Seq.map ( fun x -> sprintf "%s = %s" (fst x).QuotedSqlName (snd x) ) 
            |> String.concat ", "

        "update " + table + " set " + set 

    // SET col1 = a, col2=b WHERE state = that
    // SET col1 = a;
    // update<User> User.last_access.name now() ormState

    let inline ensureId< ^T > ( state: OrmState ) = 
        mapping< ^T > state 
        |> Array.filter ( fun x -> x.IsKey )
        |> fun x -> if Array.length x = 0 then "Record must have at least one ID attribute specified..." |> exn |> Error else Ok x
    
    let inline updateHelper<^T> ( whereClause : string ) ( instance : ^T ) ( state : OrmState ) = 
        connect state
        |> Result.bind ( fun conn -> 
            exceptionHandler ( fun ( ) ->  
                let query =  updateBase< ^T > state + whereClause //" where " + idConditional    
                let paramChar = getParamChar state

                conn.Open( )
                use cmd = makeCommand query conn state
                
                mapping< ^T > state
                |> Seq.iter ( fun x -> 
                    let formattedParam = 
                        sprintf "%s%s" paramChar x.FSharpName
                    let param = 
                        let mutable tmp = cmd.CreateParameter( )
                        tmp.ParameterName <- formattedParam 
                        
                        if x.PropertyInfo.GetValue( instance ) = null 
                        then 
                            tmp.IsNullable <- true
                            tmp.Value <- DBNull.Value
                        else 
                            tmp.Value <- ( x.PropertyInfo.GetValue( instance ) )
                        
                        tmp

                    cmd.Parameters.Add ( param ) |> ignore
                )
                
#if DEBUG
                printfn "Param count: %A" cmd.Parameters.Count
                
                for i in [0..cmd.Parameters.Count-1] do 
                    printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
#endif
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
                use cmd = paramaterizeCmd< ^T > query conn instance state
#if DEBUG
                printfn "Param count: %A" cmd.Parameters.Count
                for i in [0..cmd.Parameters.Count-1] do 
                    printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
#endif
                cmd.ExecuteNonQuery ( )
            )        
        )

    let inline deleteManyHelper< ^T > ( whereClause : string ) ( instances : ^T seq ) ( state : OrmState ) =
        connect state 
        |> Result.bind ( fun conn -> 
            exceptionHandler ( fun ( ) ->  
                let query =  deleteBase< ^T > state + whereClause //" where " + idConditional    
                conn.Open( )
                use cmd = paramaterizeSeqCmd< ^T > query conn instances state
#if DEBUG
                printfn "Param count: %A" cmd.Parameters.Count
                for i in [0..cmd.Parameters.Count-1] do 
                    printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
#endif
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
            printfn "Got ids: %A" id 
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
#if DEBUG 
            printfn "lookupId Id Column Name: %A" id 
            printfn "Where Clause: %A" whereClause 
#endif 
            if Seq.isEmpty id then {inst with value = None}
            else 
                printfn "Trying select where: "
                selectWhere<^S> whereClause state 
                |> function 
                | Ok vals when Seq.length vals > 0 ->
                    printfn "Got result, grabbing sequence head:"
                    Some <| Seq.head vals    
                | _ -> 
                    Option.None 
                |> fun (v : option<'S>) -> { inst with value = v}
