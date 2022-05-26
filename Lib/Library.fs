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


type DbContext =
    | Default = 99
 
type ContextInfo = (string * DbContext) array

[<AbstractClass>]
type DbAttribute() = 
    inherit Attribute()
    abstract Value : (string * int)
    
// ///<description>An attribute type which specifies a schema name</description>
// [<AttributeUsage(AttributeTargets.Class, AllowMultiple = true)>]
// type SchemaAttribute( alias : string, context : obj ) = 
//     inherit DbAttribute()
//     override _.Value = (alias, (box(context) :?> DbContext)  |> EnumToValue)
    

///<description>An attribute type which specifies a table name</description>
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = true)>]
type TableAttribute( alias : string , context : obj) = 
    inherit DbAttribute()
    override _.Value = (alias, (box(context) :?> DbContext)  |> EnumToValue)
    member _.Context = (box(context) :?> DbContext)  |> EnumToValue


///<description>An attribute type which specifies a column name</description>
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type ColumnAttribute( alias : string, context : obj) = 
    inherit DbAttribute()
    override _.Value = (alias,  (box(context) :?> DbContext)  |> EnumToValue)
    
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
    | MSSQL     of ( string * Enum )
    | MySQL     of ( string * Enum )
    | PSQL      of ( string * Enum )
    | SQLite    of ( string * Enum )

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

    let inline attrFold (attrs : DbAttribute array) ( ctx : Enum ) = 
        Array.fold ( fun s (x : DbAttribute) ->  
                if snd x.Value = ((box(ctx) :?> DbContext) |> EnumToValue) 
                then fst x.Value
                else s
            ) "" attrs
        
    let inline tableName< ^T > ( this : OrmState ) = 
        let attrs =
            typedefof< ^T >.GetCustomAttributes(typeof< TableAttribute >, false)
            |> Array.map ( fun x -> x :?> DbAttribute)
        
        let name = 
            if attrs = Array.empty then
                typedefof< ^T >.Name
            else 
                attrFold attrs (context< ^T > this)
        
        name.Split(".")
        |> Array.map ( fun x -> sqlQuote< ^T > x this)
        |> String.concat "."
        

    let inline columnMapping< ^T > ( this : OrmState ) = 
        FSharpType.GetRecordFields typedefof< ^T > 
        |> Array.mapi ( fun i x -> 
            let sqlName =  
                x.GetCustomAttributes(typeof< ColumnAttribute >, false) 
                |> Array.map ( fun y -> y :?> DbAttribute)
                |> fun y -> attrFold y (context< ^T > this)  //attributes< ^T, ColumnAttribute> this
                |> fun y -> if y = "" then x.Name else y 
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
        //|> Array.takeWhile (fun x -> x.SqlName <> "")

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

    
    type Column< ^T > = 
        { Name : string 
          Value : ^T
        }

    type ConjunctionState = 
        | Open 
        | None 
        | Close
        
        // member inline this.Compile = 
        //     match this with 
        //     | First c -> c.Name + " like '%" + c.Value.ToString() + "%'"
        //     | Or c -> " or " + c.Name + " like '%" + c.Value.ToString() + "%'"
        //     | And c -> " and " + c.Name + " like '%" + c.Value.ToString() + "%'"

    type Clause =
        | Select of string
        | From of string
        | Join of string
        | Where of string
        | GroupBy of string
        | Having of string
        | OrderBy of string
        | Take of string
        | Skip of string

        member this.Compile  = 
            match this with 
            | Select v ->  v
            | From v -> v
            | Join v -> v
            | Where v -> v
            | GroupBy v -> v
            | Having v -> v
            | OrderBy v -> v
            | Take v -> v
            | Skip v -> v

    type Predicate =
        | Equals of string
        | NotEquals of string
        | GreaterThan of string
        | GreaterThanOrEqualTo of string
        | LessThan of string
        | LessThanOrEqualTo of string
        | Is of string
        | Exists of string
        | Between of string
        | In of string
        | Like of string
        | ILike of string
        // | All of ( Predicate * string ) 
        // | Any of ( Predicate * string )
        // | Some_ of ( Predicate * string )

        member this.Value = 
            match this with 
            | Equals v -> "= " + v
            | NotEquals v -> "<> " + v
            | GreaterThan v -> "> " + v
            | GreaterThanOrEqualTo v -> ">= " + v
            | LessThan v -> "< " + v
            | LessThanOrEqualTo v -> "<= " + v
            | Is v -> "IS " + v
            | Exists v -> "EXISTS " + v
            | Between v -> "BETWEEN " + v
            | In v -> "IN " + v
            | Like v -> "LIKE " + v
            | ILike v -> "ILIKE " + v

    type Order =
        | Descending
        | Ascending


        
    type Conjunction =
        | First of ( string * Predicate )
        | Or of ( string * Predicate )
        | And of ( string * Predicate )
        | Parenthesize of Conjunction seq

        member this.Compile = 
            match this with 
            | First ( c, pred ) -> $"{c} {pred.Value}"
            | Or ( c, pred ) -> $" OR {c} {pred.Value}"
            | And ( c, pred ) -> $" AND {c} {pred.Value}"
            | Parenthesize cons -> 
                cons
                |> Seq.map ( fun x -> x.Compile ) 
                |> Seq.fold ( fun acc x -> acc + x ) "( "
                |> fun x -> x + " )"

    let compile ( conjunctions : Conjunction seq ) = 
        conjunctions
        |> Seq.map ( fun x -> x.Compile )
        |> String.concat " "

    let inline select< ^T > (state : OrmState) = 
        Select ( "SELECT " + (String.concat ", " (columns< ^T > state)) )
    
    let inline from< ^T > (state : OrmState) = 
        From ( "FROM " + (table< ^T > state) )

    let inline join< ^T > (conjunctions : Conjunction seq) (state : OrmState) = //JOIN "Payments.User" ON Col1 = Col2 AND Col3 = 5
        Join ("JOIN " + (table< ^T > state) + " ON " + compile conjunctions)
    
    let inline where ( conditionals : Conjunction seq ) (state : OrmState) = 
        Where ( "WHERE " + compile conditionals )
    
    let inline groupBy ( cols: string seq ) (state : OrmState) = 
        GroupBy ( "GROUP BY " + ( String.concat ", " cols ) )

    let inline orderBy ( cols: ( string * Order option ) seq ) (state : OrmState) =
        let ordering = 
            cols 
            |> Seq.map ( fun x -> 
                let direction = 
                    match snd x with 
                    | Some order -> 
                        match order with 
                        | Ascending -> " ASC"
                        | Descending -> " DESC"
                    | _ -> ""
                
                $"{fst x}{direction}"
            ) 
            |> String.concat ", "
        OrderBy ( "ORDER BY " + ordering)

    let inline skip (num : int) (state : OrmState) =
        Skip ( $"OFFSET {num}" )

    let inline take (num : int) (state : OrmState) =
        Take ( $"LIMIT {num}" )

    type ClauseState = 
        | TakesOrm of ( OrmState -> Clause )
        | Doesnt of Clause

    type Query = 
        { clauses: ( OrmState -> Clause ) list }
        member this.Compile ( state : OrmState ) =
            List.fold ( fun acc ( elem : ( OrmState -> Clause ) ) -> if acc = "" then (elem state).Compile  else acc + "\n" + (elem state).Compile ) "" this.clauses

    type UnionAll =
        { queries: Query list }
        member this.Compile ( state : OrmState ) =
            List.fold ( fun acc ( elem : Query ) -> if acc = "" then elem.Compile state else acc + "\n\nUNION ALL\n\n" + elem.Compile state ) "" this.queries