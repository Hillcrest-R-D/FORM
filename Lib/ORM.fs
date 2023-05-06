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
    let inline connect ( this : OrmState ) : Result< DbConnection, exn > = 
        try 
            match this with 
            | MSSQL     ( str, _ ) -> new SqlConnection( str ) :> DbConnection
            | MySQL     ( str, _ ) -> new MySqlConnection( str ) :> DbConnection
            | PSQL      ( str, _ ) -> new NpgsqlConnection( str ) :> DbConnection
            | SQLite    ( str, _ ) -> new SqliteConnection( str ) :> DbConnection
            |> Ok
        with 
        | exn -> Error exn

    let inline sqlQuote str ( this : OrmState ) =
        match this with 
        | MSSQL _ -> $"[{str}]"
        | MySQL _ -> $"`{str}`"
        | PSQL _ | SQLite _ -> $"\"{str}\""

    let inline context< ^T > ( this : OrmState ) = 
        match this with 
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
        
    let inline tableName< ^T > ( this : OrmState ) = 
        let attrs =
            typedefof< ^T >.GetCustomAttributes( typeof< TableAttribute >, false )
            |> Array.map ( fun x -> x :?> DbAttribute )
        
        let name = 
            if attrs = Array.empty then
                typedefof< ^T >.Name
            else 
                attrFold attrs ( context< ^T > this )
        
        name.Split( "." )
        |> Array.map ( fun x -> sqlQuote x this )
        |> String.concat "."
        

    let inline columnMapping< ^T > ( this : OrmState ) = 
        FSharpType.GetRecordFields typedefof< ^T > 
        |> Array.mapi ( fun i x -> 
            let sqlName =  
                x.GetCustomAttributes( typeof< ColumnAttribute >, false ) 
                |> Array.map ( fun y -> y :?> DbAttribute )
                |> fun y -> attrFold y ( context< ^T > this )  //attributes< ^T, ColumnAttribute> this
                |> fun y -> if y = "" then x.Name else y 
            let isKey =
                x.GetCustomAttributes( typeof< IdAttribute >, false ) 
                |> Array.map ( fun y -> y :?> DbAttribute )
                |> fun y -> attrFold y ( context< ^T > this )  //attributes< ^T, ColumnAttribute> this
                |> fun y -> if y = "" then false else true 
            let fsharpName = x.Name
            let quotedName = sqlQuote sqlName this
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

    let inline table< ^T > ( this : OrmState ) = 
        tableName< ^T > this
    
    let inline mapping< ^T > ( this : OrmState ) = 
        columnMapping< ^T > this

    let inline columns< ^T > ( this : OrmState ) = 
        mapping< ^T > this
        |> Array.map ( fun x -> x.QuotedSqlName )
        
    let inline fields< ^T >  ( this : OrmState ) = 
        mapping< ^T > this
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
    
    let inline generateReader< ^T > ( reader : IDataReader )  ( this : OrmState ) = 
        let rty = typeof< ^T >
        let makeEntity vals = FSharpValue.MakeRecord( rty, vals ) :?>  ^T
        let fields = 
            seq { for fld in ( columnMapping< ^T > this ) -> fld.SqlName, fld } 
            |> dict 
        seq { 
            while reader.Read( ) do
                yield 
                    seq { 0..reader.FieldCount-1 }
                    |> Seq.map ( fun i -> reader.GetName( i ), reader.GetValue( i ) )
                    |> Seq.sortBy ( fun ( n, _ ) ->  fields[n].Index )
                    |> Seq.map ( fun ( n, v ) -> 
                        match optionType< ^T > fields[n].Type this with
                        | Some t -> toOption< ^T > t v this
                        | None   -> v
                    )
                    |> Seq.toArray
                    |> makeEntity 
        } 

    let inline makeParamChar this = 
        match this with
        | MSSQL _ -> "@"
        | MySQL _ -> "$"
        | PSQL _ -> "@"
        | SQLite _ -> "@"   

    let inline makeInsert< ^T > tableName columns ( this : OrmState ) =
        let paramChar = makeParamChar this
        let placeHolders = 
            columns 
            |> Seq.mapi ( fun i _ -> 
                $"{paramChar}{i+1}"
            )
            |> String.concat ", "
        let columnNames =
            String.concat ", " columns
        
        $"Insert Into {tableName}( {columnNames} ) Values ( {placeHolders} )"

    //Insert Into table1 Values
    // ( $1, $2, $3 ),
    // ( $4, $5, $6 ),
    let inline makeInsertMany< ^T > tableName columns ( instances : ^T seq ) ( this : OrmState ) =
        let paramChar = makeParamChar this
        let placeHolders = 
            instances 
            |> Seq.mapi ( fun j e ->
#if DEBUG
                printfn "%A" e
#endif          
                columns 
                |> Seq.mapi ( fun i _ -> 
                    $"{paramChar}{i+1+j*Seq.length( columns )}"
                )
                |> String.concat ", "
            )
            |> String.concat " ), ( "
        // printfn "%A" placeHolders
        let columnNames =
            String.concat ", " columns
        
        $"Insert Into {tableName}( {columnNames} ) Values ( {placeHolders} );" 
    
    let inline makeCommand ( query : string ) ( connection : DbConnection )  ( this : OrmState ) : DbCommand = 
#if DEBUG
        printfn "Query being generated:\n\n%s\n\n\n" query
#endif
        match this with 
        | MSSQL _ -> new SqlCommand ( query, connection :?> SqlConnection )
        | MySQL _ -> new MySqlCommand ( query, connection :?> MySqlConnection )
        | PSQL _ -> new NpgsqlCommand ( query, connection :?> NpgsqlConnection )
        | SQLite _ -> new SqliteCommand ( query, connection :?> SqliteConnection )

    let inline Execute sql  ( this : OrmState ) =
        match connect this with 
        | Ok conn -> 
            conn.Open( )
            use cmd = makeCommand sql conn this
            let result = cmd.ExecuteNonQuery( )
            conn.Close( )
            Ok result
        | Error e -> Error e
    
    let inline ExecuteReader sql f  ( this : OrmState ) =
        match connect this with
        | Ok conn -> 
            conn.Open( )
            use cmd = makeCommand sql conn this
            use reader = cmd.ExecuteReader( )
            let result = f reader
            conn.Close( )
            result
        | Error e -> Error e
        
    
    let inline queryBase< ^T >  ( this : OrmState ) = 
        let cols = columns< ^T > this 
        ( String.concat ", " cols ) + " From " + table< ^T > this

    let inline exceptionHandler f =
        try 
            Ok <| f( )
        with 
        | exn -> Error exn

    let inline private select< ^T > query ( this : OrmState ) = 
        match connect this with 
        | Ok conn -> 
            exceptionHandler ( fun ( ) ->  
                conn.Open( )   
                seq {
                    use cmd = makeCommand query conn this
                    use reader = cmd.ExecuteReader( ) // CommandBehavior.CloseConnection
                    yield! generateReader< ^T > reader this 
                }
            )
        | Error e -> Error e


    let inline selectHelper< ^T > f ( this : OrmState ) = 
        queryBase< ^T > this
        |> f
        |> fun x -> select< ^T > x this
    
    let inline selectLimit< ^T > lim  ( this : OrmState ) = 
        selectHelper< ^T > ( fun x -> $"select top {lim} {x}" ) this

    let inline selectWhere< ^T > where  ( this : OrmState ) = 
        selectHelper< ^T > ( fun x -> $"select {x} where {where}" ) this
        
    let inline selectAll< ^T >  ( this : OrmState ) = 
        selectHelper< ^T > ( fun x -> $"select {x}" ) this

        
    let inline makeParameter ( this : OrmState ) : DbParameter =
        match this with
        | MSSQL _ -> SqlParameter( )
        | MySQL _ -> MySqlParameter( )
        | PSQL _ -> NpgsqlParameter( )
        | SQLite _ -> SqliteParameter( )
    
    
    let inline insert< ^T > ( instance : ^T )  ( this : OrmState ) =
        match connect this with
        | Ok conn ->
            conn.Open( )
            let query = makeInsert ( table< ^T > this ) ( columns< ^T > this ) this
            use cmd = makeCommand query conn this
            
            let paramChar = makeParamChar this 

            mapping< ^T > this
            |> Array.mapi ( fun index x -> 
                let param = 
                    if ( x.PropertyInfo.GetValue( instance ) ) = null then 
                        let mutable tmp = cmd.CreateParameter( )
                        tmp.ParameterName <- $"{paramChar}{( index + 1 )}"
                        tmp.IsNullable <- true
                        tmp.Value <- DBNull.Value
                        tmp
                    else 
                        let mutable tmp = cmd.CreateParameter( )
                        tmp.ParameterName <- $"{paramChar}{( index + 1 )}"
                        tmp.Value <- ( x.PropertyInfo.GetValue( instance ) )
                        tmp
                cmd.Parameters.Add ( param )
            ) |> ignore
            
#if DEBUG
            printfn "Param count: %A" cmd.Parameters.Count
            
            for i in [0..cmd.Parameters.Count-1] do 
                printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
#endif

            let result = exceptionHandler ( fun ( ) -> cmd.ExecuteNonQuery ( ) )
            conn.Close( )
            result
        | Error e -> Error e

    let inline insertAll< ^T > ( instances : ^T seq )  ( this : OrmState ) =
        match connect this with
        | Ok conn -> 
            conn.Open( )
            let query = makeInsertMany ( table< ^T > this )  ( columns< ^T > this )  instances this
            use cmd = makeCommand query conn this
            let paramChar = ( makeParamChar this )
            let numCols = columns< ^T > this |> Seq.length
            instances
            |> Seq.iteri ( fun jindex instance  -> 
                mapping< ^T > this
                |> Array.mapi ( fun index x -> 
                    
                    let param = 
                        if ( x.PropertyInfo.GetValue( instance ) ) = null then 
                            let mutable tmp = cmd.CreateParameter( )
                            tmp.ParameterName <- $"{paramChar}{( index + 1 + jindex * numCols )}"
                            tmp.IsNullable <- true
                            tmp.Value <- DBNull.Value
                            tmp
                        else 
                            let mutable tmp = cmd.CreateParameter( )
                            tmp.ParameterName <- $"{paramChar}{( index + 1 + jindex*numCols )}"
                            tmp.Value <- ( x.PropertyInfo.GetValue( instance ) )
                            tmp

                    cmd.Parameters.Add ( param )
                ) |> ignore                
            ) 
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
    let inline updateBase< ^T > ( this : OrmState ) = 
        let pchar = makeParamChar this
        let cols = 
            mapping< ^T > this
            |> Seq.filter (fun col -> not col.IsKey) //Can't update keys
        let queryParams = 
            cols 
#if DEBUG
            |> fun cols -> 
                printfn "columns to update: %A" cols; cols 
#endif
            |> Seq.map (fun col -> pchar + col.FSharpName ) // @col1, @col2, @col3

        let table = table< ^T > this 
        let set = 
            Seq.zip cols queryParams
            |> Seq.map ( fun x -> sprintf "%s = %s" (fst x).QuotedSqlName (snd x) ) 
            |> String.concat ", "

        "update " + table + " set " + set 

    // SET col1 = a, col2=b WHERE this = that
    // SET col1 = a;
    // update<User> User.last_access.name now() ormState

    let inline ensureId< ^T > ( this: OrmState ) = 
        mapping< ^T > this 
        |> Array.filter ( fun x -> x.IsKey )
        |> fun x -> if Array.length x = 0 then "Record must have at least one ID attribute specified..." |> exn |> Error else Ok x
    
    let inline updateHelper<^T> ( whereClause : string ) ( instance : ^T ) ( this : OrmState ) = 
        connect this
        |> Result.bind ( fun conn -> 
            exceptionHandler ( fun ( ) ->  
                let query =  updateBase< ^T > this + whereClause //" where " + idConditional    
                let paramChar = makeParamChar this

                conn.Open( )
                use cmd = makeCommand query conn this
                
                mapping< ^T > this
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
        
    let inline update< ^T > ( instance: ^T ) ( this : OrmState ) = 
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
        let table = table< ^T > this 
        let paramChar = makeParamChar this
        
        ensureId< ^T > this 
        |> Result.bind (fun sqlMapping ->
            sqlMapping
            |> Seq.map ( fun x -> sprintf "%s.%s = %s%s" table x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun idConditional -> updateHelper< ^T > ( sprintf " where %s" idConditional ) instance this
        )
        
    let inline updateAll< ^T > ( instances: ^T seq ) ( this : OrmState ) = 
        Seq.map ( fun instance -> update<^T> instance this ) instances 
        
    let inline updateWhere< ^T > ( where : string ) ( instance: ^T ) ( this : OrmState ) = 
        (*
            uPDATE tableT
                SET 
                    col1 = item.col1, 
                    col2 = item.col2 
                    ... 
                    coln = item.coln
                WHERE
                    
        *)
        updateHelper< ^T > ( sprintf " where %s" where )  instance this    
        
    // let inline updatecolumns< ^T > ( cols: PropertyInfo seq ) ( item: ^T ) ( this : OrmState ) = 
        


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
