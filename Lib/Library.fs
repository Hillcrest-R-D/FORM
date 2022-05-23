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


type DbContext = Enum
 
type ContextInfo = (string * DbContext) array

[<AbstractClass>]
type DbAttribute() = 
    inherit Attribute()
    abstract Value : (string * DbContext)

///<description>An attribute type which specifies a schema name</description>
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = true)>]
type SchemaAttribute( alias : string, context : DbContext ) = 
    inherit DbAttribute()
    override _.Value = (alias, context)
    

///<description>An attribute type which specifies a table name</description>
[<AttributeUsage(AttributeTargets.Class)>]
type TableAttribute( alias : string , ctx : ^T when ^T :> Enum and ^T : (member GetValue : int32)) = 
    inherit Attribute()
    member _.Value = alias
    member _.Contexts = ctx |> EnumToValue

///<description>An attribute type which specifies a column name</description>
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type ColumnAttribute( alias : string, context : DbContext ) = 
    inherit DbAttribute()
    override _.Value = (alias, context)
    
///<description>A record type which holds the information required to map across BE and DB. </description>
type SqlMapping = { 
    Index : int
    SqlName : string 
    QuotedSqlName : string
    FSharpName : string
    Type : Type
    PropertyInfo: PropertyInfo
}

type OrmState = 
    | MSSQL     of ( string * DbContext )
    | MySQL     of ( string * DbContext )
    | PSQL      of ( string * DbContext )
    | SQLite    of ( string * DbContext )

module Orm = 
    let inline connection< ^T > ( this : OrmState ) : DbConnection = 
        match this with 
        | MSSQL     ( str, _ ) -> new SqlConnection( str ) 
        | MySQL     ( str, _ ) -> new MySqlConnection( str )
        | PSQL      ( str, _ ) -> new NpgsqlConnection( str )
        | SQLite    ( str, _ ) -> new SqliteConnection( str )

    let inline sqlQuote< ^T > str ( this : OrmState ) =
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

    let inline attrFold (attrs : DbAttribute array) (ctx : DbContext) = 
        printfn "HERE!"
        Array.fold ( fun s (x : DbAttribute) ->  
                if snd x.Value = ctx 
                then fst x.Value
                else s
            ) "" attrs
    
    

        
        
    let inline tableName< ^T > ( this : OrmState ) = 
        let attrs =
            typedefof< ^T >.GetCustomAttributes(typeof< TableAttribute >, false)
            |> Array.map ( fun x -> x :?> DbAttribute)

        if attrs = Array.empty then
            typedefof< ^T >.Name
        else 
            attrFold attrs (context< ^T > this)
        //attributes< ^T, TableAttribute > this

    let inline columnMapping< ^T > ( this : OrmState ) = 
        FSharpType.GetRecordFields typedefof< ^T > 
        |> Array.mapi ( fun i x -> 
            let sqlName =  
                x.GetCustomAttributes(typeof< ColumnAttribute >, false) 
                |> Array.map ( fun y -> y :?> DbAttribute)
                |> fun y -> attrFold y (context< ^T > this)  //attributes< ^T, ColumnAttribute> this
            let fsharpName = x.Name
            let quotedName = sqlQuote< ^T > sqlName this
            { 
                Index = i
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
        |> Array.map ( fun x -> x.SqlName )
        
    let inline fields< ^T >  ( this : OrmState ) = 
        mapping< ^T > this
        |> Array.map ( fun x -> x.FSharpName )
    
    let inline toOptionDynamic< ^T > (typ: Type) (value: obj)  ( _ : OrmState ) =
        let opttyp = typedefof<Option<_>>.MakeGenericType([|typ|])
        let tag, varr = if DBNull.Value.Equals(value) then 0, [||] else 1, [|value|]
        let case = FSharpType.GetUnionCases(opttyp) |> Seq.find (fun uc -> uc.Tag = tag)
        FSharpValue.MakeUnion(case, varr)

    let inline optionTypeArg< ^T > (typ : Type)  ( _ : OrmState ) =
        let isOp = typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<Option<_>>
        if isOp 
        then Some (typ.GetGenericArguments()[0]) 
        else None
    
    let inline generateReader< ^T > ( reader : IDataReader )  ( this : OrmState ) = 
        let rty = typeof< ^T>
        let makeEntity vals = FSharpValue.MakeRecord(rty, vals) :?>  ^T
        let fields = 
            seq { for fld in ( columnMapping< ^T > this ) -> fld.SqlName, fld } 
            |> dict 
        seq { while reader.Read() do
                yield seq { 0..reader.FieldCount-1 }
                    |> Seq.map (fun i -> reader.GetName(i), reader.GetValue(i) )
                    // |> fun x -> printfn "%A" x; x
                    |> Seq.sortBy (fun (n, _) ->  fields[n].Index )
                    |> Seq.map (fun (n, v) -> 
                        match optionTypeArg< ^T > fields[n].Type this with
                        | Some t -> toOptionDynamic< ^T > t v this
                        | None   -> v
                    )
                    |> Seq.toArray
                    |> makeEntity } 
        |> Seq.toArray

    let inline makeInsert< ^T > tableName columns  ( _ : OrmState ) =
        let placeHolders = 
            columns 
            |> Seq.map ( fun _ -> 
                "?"
            )
            |> String.concat ", "
        let columnNames =
            String.concat ", " columns
        
        $"insert into {tableName}( {columnNames} ) values ( {placeHolders} )" 
    
    let inline makeCommand< ^T > ( query : string ) ( connection : DbConnection )  ( this : OrmState ) : DbCommand = 
        // printfn "%A" query
        match this with 
        | MSSQL _ -> new SqlCommand ( query, connection :?> SqlConnection )
        | MySQL _ -> new MySqlCommand ( query, connection :?> MySqlConnection)
        | PSQL _ -> new NpgsqlCommand ( query, connection :?> NpgsqlConnection)
        | SQLite _ -> new SqliteCommand ( query, connection :?> SqliteConnection)

    let inline execute< ^T > sql  ( this : OrmState ) =
        use conn = connection< ^T > this
        conn.Open()
        use cmd = makeCommand< ^T > sql conn this
        cmd.ExecuteNonQuery()
    
    let inline executeReader< ^T > sql f  ( this : OrmState ) =
        use conn = connection< ^T > this
        conn.Open()
        use cmd = makeCommand< ^T > sql conn this
        use reader = cmd.ExecuteReader()
        f reader 
    
    let inline queryBase< ^T >  ( this : OrmState ) = 
        printfn "No over here!"
        let cols = columns< ^T > this |> Array.map ( fun x -> sqlQuote< ^T > x this )
        printfn "Done with cols: %A" cols
        ( String.concat ", " cols ) + " from " + table< ^T > this

    let inline exceptionHandler f =
        try 
            Ok f
        with 
        | exn -> Error {| Message = exn.ToString() |}

    
    let inline selectLimit< ^T > lim  ( this : OrmState ) = 
        use conn = connection< ^T > this
        conn.Open()
        let queryBase = 
            queryBase< ^T > this
        let query = 
            $"select top {lim} {queryBase}"

        use cmd = makeCommand< ^T > query conn this
        use reader = cmd.ExecuteReader(CommandBehavior.CloseConnection)

        exceptionHandler ( generateReader< ^T > reader this )

    let inline selectWhere< ^T > where  ( this : OrmState ) = 
        use conn = connection< ^T > this
        conn.Open()
        let queryBase = 
            queryBase< ^T > this
        
        let query = 
            $"select {queryBase} where {where}"

        use cmd = makeCommand< ^T > query conn this
        use reader = cmd.ExecuteReader(CommandBehavior.CloseConnection)
        exceptionHandler ( generateReader< ^T > reader this )
        

    let inline selectAll< ^T >  ( this : OrmState ) = 
        use conn = connection< ^T > this
        conn.Open()
        let queryBase = 
            queryBase< ^T > this
        let query = 
            $"select {queryBase}"
        use cmd = makeCommand< ^T > query conn this
        use reader = cmd.ExecuteReader(CommandBehavior.CloseConnection)
        exceptionHandler ( generateReader< ^T > reader this )

    let inline insert< ^T > ( instance : ^T )  ( this : OrmState ) =
        use connection = connection< ^T > this
        connection.Open()
        let query = makeInsert ( table< ^T > this ) ( columns< ^T > this ) this
        use cmd = makeCommand< ^T > query connection this
        
        mapping< ^T > this
        |> Seq.map ( fun x -> 
            cmd.Parameters.Add( x.PropertyInfo.GetValue( instance ) )
        )
        |> ignore

        exceptionHandler ( cmd.ExecuteNonQuery() )