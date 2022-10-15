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
 
type Key =
    | Primary = 1
    // | Foreign = 2
    | Alternate = 3
    | Composite = 4
    | Super = 5
    | Candidate = 6
    | Unique = 7 
    // static member Value =
    //     function
    //     | Primary -> "1"
    //     | Foreign -> "2"
    //     | Alternate -> "3"
    //     | Composite -> "4"
    //     | Super -> "5"
    //     | Candidate -> "6"
    //     | Unique -> "7"

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
    override _.Value = (alias, (context :?> DbContext)  |> EnumToValue)
    member _.Context = (context :?> DbContext)  |> EnumToValue


///<description>An attribute type which specifies a column name</description>
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type ColumnAttribute( alias : string, context : obj) = 
    inherit DbAttribute()
    override _.Value = (alias,  (context :?> DbContext)  |> EnumToValue)

///<description>An attribute type which specifies a column name</description>
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type KeyAttribute( key : obj, context : obj) = 
    inherit DbAttribute()
    override _.Value = (unbox<string> key,  (context :?> DbContext)  |> EnumToValue)
    member _.Key = key
    
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type ConstraintAttribute( definition : string, context : obj) = 
    inherit DbAttribute()
    override _.Value = (definition,  (context :?> DbContext)  |> EnumToValue)

[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type SQLTypeAttribute( definition : string, context : obj) = 
    inherit DbAttribute()
    override _.Value = (definition,  (context :?> DbContext)  |> EnumToValue)
    

    
///<description>A record type which holds the information required to map across BE and DB. </description>
type SqlMapping = { 
    Index : int
    SqlName : string 
    QuotedSqlName : string
    FSharpName : string
    Type : Type
    PropertyInfo: PropertyInfo
}

///<description>Stores the flavor and context used for a particular connection.</description>
type OrmState = 
    | MSSQL     of ( string * Enum )
    | MySQL     of ( string * Enum )
    | PSQL      of ( string * Enum )
    | SQLite    of ( string * Enum )

module Orm = 
    ///<description>Stores the flavor and context used for a particular connection.</description>
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
        |> Array.map ( fun x -> sqlQuote x this)
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
            let quotedName = sqlQuote sqlName this
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
    
    let inline toOption< ^T > (type_: Type) (value: obj)  ( _ : OrmState ) =
        let tag, variable = if DBNull.Value.Equals(value) then 0, [||] else 1, [|value|]
        let optionType = typedefof<Option<_>>.MakeGenericType([|type_|])
        let case = FSharpType.GetUnionCases(optionType) |> Seq.find (fun info -> info.Tag = tag)
        FSharpValue.MakeUnion(case, variable)

    let inline optionType< ^T > (type_ : Type)  ( _ : OrmState ) =
        if type_.IsGenericType && type_.GetGenericTypeDefinition() = typedefof<Option<_>>
        then Some (type_.GetGenericArguments() |> Array.head) // optionType Option<User> -> User  
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
                    |> Seq.sortBy (fun (n, _) ->  fields[n].Index )
                    |> Seq.map (fun (n, v) -> 
                        match optionType< ^T > fields[n].Type this with
                        | Some t -> toOption< ^T > t v this
                        | None   -> v
                    )
                    |> Seq.toArray
                    |> makeEntity } 
        |> Seq.toArray
    let inline makeParamChar this = 
        match this with
        | MSSQL _ -> "@"
        | MySQL _ -> "$"
        | PSQL _ -> "$"
        | SQLite _ -> "@a"   

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
        
        $"insert into {tableName}( {columnNames} ) values ( {placeHolders} )"

//INSERT INTO TABLE1 VALUES
// ($1, $2, $3),
// ($4, $5, $6),
    let inline makeInsertMany< ^T > tableName columns (instances : ^T seq) ( this : OrmState ) =
        let paramChar = makeParamChar this
        let placeHolders = 
            instances 
            |> Seq.mapi ( fun j _ ->
                columns 
                |> Seq.mapi ( fun i _ -> 
                    $"{paramChar}{i+1+j*Seq.length(columns)}"
                )
                |> String.concat ", "
            )
            |> String.concat "), ("
        // printfn "%A" placeHolders
        let columnNames =
            String.concat ", " columns
        
        $"insert into {tableName}( {columnNames} ) values ({placeHolders});" 
    
    let inline makeCommand ( query : string ) ( connection : DbConnection )  ( this : OrmState ) : DbCommand = 
#if DEBUG
        printfn "Query being generated:\n\n%s\n\n\n" query
#endif
        match this with 
        | MSSQL _ -> new SqlCommand ( query, connection :?> SqlConnection )
        | MySQL _ -> new MySqlCommand ( query, connection :?> MySqlConnection)
        | PSQL _ -> new NpgsqlCommand ( query, connection :?> NpgsqlConnection)
        | SQLite _ -> new SqliteCommand ( query, connection :?> SqliteConnection)

    let inline execute sql  ( this : OrmState ) =
        match connect this with 
        | Ok conn -> 
            conn.Open()
            use cmd = makeCommand sql conn this
            let result = cmd.ExecuteNonQuery()
            conn.Close()
            Ok result
        | Error e -> Error e
    
    let inline executeReader sql f  ( this : OrmState ) =
        match connect this with
        | Ok conn -> 
            conn.Open()
            use cmd = makeCommand sql conn this
            use reader = cmd.ExecuteReader()
            let result = f reader
            conn.Close()
            result
        | Error e -> Error e
        
    
    let inline queryBase< ^T >  ( this : OrmState ) = 
        let cols = columns< ^T > this 
        ( String.concat ", " cols ) + " from " + table< ^T > this

    let inline exceptionHandler f =
        try 
            Ok f
        with 
        | exn -> Error exn

    let inline private select< ^T > query ( this : OrmState ) = 
        match connect this with 
        | Ok conn -> 
            conn.Open()
            use cmd = makeCommand query conn this
            use reader = cmd.ExecuteReader(CommandBehavior.CloseConnection)
            
            let result = exceptionHandler ( generateReader< ^T > reader this )
            result
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

        
    let inline makeParameter (this : OrmState) : DbParameter =
        match this with
        | MSSQL _ -> SqlParameter()
        | MySQL _ -> MySqlParameter()
        | PSQL _ -> NpgsqlParameter()
        | SQLite _ -> SqliteParameter()
    
    
    let inline insert< ^T > ( instance : ^T )  ( this : OrmState ) =
        match connect this with
        | Ok conn ->
            conn.Open()
            let query = makeInsert ( table< ^T > this ) ( columns< ^T > this ) this
            use cmd = makeCommand query conn this
            
            let paramChar = makeParamChar this 

            mapping< ^T > this
            // |> fun x -> printfn "%A" x; x
            |> Array.mapi ( fun index x -> 
                let param = 
                    if (x.PropertyInfo.GetValue( instance )) = null then 
                        //DbValue.null
                        // printfn "BIG NIPS -- %A | %A" x instance
                        let mutable tmp = cmd.CreateParameter()
                        tmp.ParameterName <- $"a{( index + 1)}"
                        tmp.IsNullable <- true
                        tmp.Value <- DBNull.Value
                        tmp
                    else 
                        let mutable tmp = cmd.CreateParameter()
                        tmp.ParameterName <- $"a{( index + 1)}"
                        tmp.Value <- (x.PropertyInfo.GetValue( instance ))
                        tmp
                cmd.Parameters.Add ( param )
            ) |> ignore

            let result = exceptionHandler ( cmd.ExecuteNonQuery() )
            conn.Close()
            result
        | Error e -> Error e

    let inline insertAll< ^T > ( instances : ^T seq )  ( this : OrmState ) =
        match connect this with
        | Ok conn -> 
            conn.Open()
            let query = makeInsertMany ( table< ^T > this )  ( columns< ^T > this )  instances this
            use cmd = makeCommand query conn this
            // printfn "%A" query
            let paramChar = (makeParamChar this)
            let numCols = columns< ^T > this |> Seq.length
            instances
            |> Seq.iteri ( fun jindex instance  -> 
                mapping< ^T > this
                // |> fun x -> printfn "%A" x; x
                |> Array.mapi ( fun index x -> 
                    
                    let param = 
                        if (x.PropertyInfo.GetValue( instance )) = null then 
                            //DbValue.null
                            
                            // printfn "LITTLE NIPS -- %A | %A" x instance
                            let mutable tmp = cmd.CreateParameter()
                            tmp.ParameterName <- $"a{( index + 1 + jindex*numCols )}"
                            tmp.IsNullable <- true
                            tmp.Value <- DBNull.Value
                            tmp
                        else 
                            let mutable tmp = cmd.CreateParameter()
                            tmp.ParameterName <- $"a{( index + 1 + jindex*numCols )}"
                            tmp.Value <- (x.PropertyInfo.GetValue( instance ))
                            tmp
                    cmd.Parameters.Add ( param )
                ) |> ignore                
            ) 
            printfn "Param count: %A" cmd.Parameters.Count
            
            for i in [0..cmd.Parameters.Count-1] do 
                printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value

            let result = 
                cmd.ExecuteNonQuery()
                |> exceptionHandler 
    
            conn.Close()
            result
        | Error e -> Error e

module DSL = 
    open Orm
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

        member this.Value (state : OrmState) = 
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

        member this.Compile ( state : OrmState ) = 
            match this with 
            | First ( c, pred ) -> $"{sqlQuote c state} {pred.Value state}"
            | Or ( c, pred ) -> $" OR {sqlQuote c state} {pred.Value state}"
            | And ( c, pred ) -> $" AND {sqlQuote c state} {pred.Value state}"
            | Parenthesize cons -> 
                cons
                |> Seq.map ( fun x -> x.Compile state ) 
                |> Seq.fold ( fun acc x -> acc + x ) "( "
                |> fun x -> x + " )"

    let compile ( conjunctions : Conjunction seq ) ( state : OrmState  ) = 
        conjunctions
        |> Seq.map ( fun x -> x.Compile state )
        |> String.concat " "

    let inline select< ^T > (state : OrmState) = 
        Select ( "SELECT " + (String.concat ", " (columns< ^T > state)) )
    
    let inline from< ^T > (state : OrmState) = 
        From ( "FROM " + (table< ^T > state) )

    let inline join< ^T > (conjunctions : Conjunction seq) (state : OrmState) = //JOIN "Payments.User" ON Col1 = Col2 AND Col3 = 5
        Join ("JOIN " + (table< ^T > state) + " ON " + compile conjunctions state)
    
    let inline where ( conjunctions : Conjunction seq ) (state : OrmState) = 
        Where ( "WHERE " + compile conjunctions state )
    
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



    type IDataTransferObject =
        interface
        end

        
///<description>An attribute type which specifies a column name</description>
[<AttributeUsage(AttributeTargets.Property, AllowMultiple = true)>]
type ForeignKeyAttribute( table : ^T, column : string, context : obj) = 
    inherit DbAttribute()
    override _.Value = (column,  (box(context) :?> DbContext)  |> EnumToValue)
    member _.Table = table
    member _.column = table 