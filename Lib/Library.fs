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
    | PrimaryKey = 1
    // | Foreign = 2
    | Alternate = 3
    | Composite = 4
    | Super = 5
    | CAndidate = 6
    | Unique = 7 
    // static member Value =
    //     function
    //     | PrimaryKey "1"
    //     | Foreign -> "2"
    //     | Alternate -> "3"
    //     | Composite -> "4"
    //     | Super -> "5"
    //     | CAndidate -> "6"
    //     | Unique -> "7"

type ContextInfo = ( string * DbContext ) array

[<AbstractClass>]
type DbAttribute( ) = 
    inherit Attribute( )
    abstract Value : ( string * int )
    
// ///<Description>An attribute type which specifies a schema name</Description>
// [<AttributeUsage( AttributeTargets.Class, AllowMultiple = true )>]
// type SchemaAttribute( aliAs : string, context : obj ) = 
//     inherit DbAttribute( )
//     override _.Value = ( aliAs, ( box( context ) :?> DbContext )  |> EnumToValue )
    

///<Description>An attribute type which specifies a Table name</Description>
[<AttributeUsage( AttributeTargets.Class, AllowMultiple = true )>]
type TableAttribute( aliAs : string , context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( aliAs, ( context :?> DbContext )  |> EnumToValue )
    member _.Context = ( context :?> DbContext )  |> EnumToValue


///<Description>An attribute type which specifies a Column name</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ColumnAttribute( aliAs : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( aliAs,  ( context :?> DbContext )  |> EnumToValue )

///<Description>An attribute type which specifies a Column name</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type KeyAttribute( key : obj, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( unbox<string> key,  ( context :?> DbContext )  |> EnumToValue )
    member _.Key = key
    
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ConstraintAttribute( definition : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( definition,  ( context :?> DbContext )  |> EnumToValue )

[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type SQLTypeAttribute( definition : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( definition,  ( context :?> DbContext )  |> EnumToValue )
    

    
///<Description>A record type which holds the information required to map across BE And DB. </Description>
type SqlMapping = { 
    Index : int
    SqlName : string 
    QuotedSqlName : string
    FSharpName : string
    Type : Type
    PropertyInfo: PropertyInfo
}

///<Description>Stores the flavor And context used for a particular connection.</Description>
type OrmState = 
    | MSSQL     of ( string * Enum )
    | MySQL     of ( string * Enum )
    | PSQL      of ( string * Enum )
    | SQLite    of ( string * Enum )

// type Relation<^T,^S> =
//     {
//         id : ^T 
//         value : ^S option    
//     }
//     static member inline Value state =
//         let id = lookupId<^S>()
//         Orm.SelectWhere<^S> $"{id} = '{state.id}'"  

    // ormstate
    // |> Relation.Value transaction.employee 

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
        
    let inline TableName< ^T > ( this : OrmState ) = 
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
        

    let inline ColumnMapping< ^T > ( this : OrmState ) = 
        FSharpType.GetRecordFields typedefof< ^T > 
        |> Array.mapi ( fun i x -> 
            let sqlName =  
                x.GetCustomAttributes( typeof< ColumnAttribute >, false ) 
                |> Array.map ( fun y -> y :?> DbAttribute )
                |> fun y -> attrFold y ( context< ^T > this )  //attributes< ^T, ColumnAttribute> this
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

    let inline Table< ^T > ( this : OrmState ) = 
        TableName< ^T > this
    
    let inline mapping< ^T > ( this : OrmState ) = 
        ColumnMapping< ^T > this

    let inline Columns< ^T > ( this : OrmState ) = 
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
            seq { for fld in ( ColumnMapping< ^T > this ) -> fld.SqlName, fld } 
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
        
        $"Insert Into {tableName}( {columnNames} ) Values ( {placeHolders} )"

    //Insert Into Table1 Values
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
        let cols = Columns< ^T > this 
        ( String.concat ", " cols ) + " From " + Table< ^T > this

    let inline exceptionHandler f =
        try 
            Ok <| f( )
        with 
        | exn -> Error exn

    let inline private Select< ^T > query ( this : OrmState ) = 
        match connect this with 
        | Ok conn -> 
            conn.Open( )
            use cmd = makeCommand query conn this
            use reader = cmd.ExecuteReader( CommandBehavior.CloseConnection )
            
            let result = exceptionHandler ( fun ( ) -> generateReader< ^T > reader this )
            result
        | Error e -> Error e


    let inline SelectHelper< ^T > f ( this : OrmState ) = 
        queryBase< ^T > this
        |> f
        |> fun x -> Select< ^T > x this
    
    let inline SelectLimit< ^T > lim  ( this : OrmState ) = 
        SelectHelper< ^T > ( fun x -> $"Select Top {lim} {x}" ) this

    let inline SelectWhere< ^T > where  ( this : OrmState ) = 
        SelectHelper< ^T > ( fun x -> $"Select {x} Where {where}" ) this
        
    let inline SelectAll< ^T >  ( this : OrmState ) = 
        SelectHelper< ^T > ( fun x -> $"Select {x}" ) this

        
    let inline makeParameter ( this : OrmState ) : DbParameter =
        match this with
        | MSSQL _ -> SqlParameter( )
        | MySQL _ -> MySqlParameter( )
        | PSQL _ -> NpgsqlParameter( )
        | SQLite _ -> SqliteParameter( )
    
    
    let inline Insert< ^T > ( instance : ^T )  ( this : OrmState ) =
        match connect this with
        | Ok conn ->
            conn.Open( )
            let query = makeInsert ( Table< ^T > this ) ( Columns< ^T > this ) this
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

    let inline InsertAll< ^T > ( instances : ^T seq )  ( this : OrmState ) =
        match connect this with
        | Ok conn -> 
            conn.Open( )
            let query = makeInsertMany ( Table< ^T > this )  ( Columns< ^T > this )  instances this
            use cmd = makeCommand query conn this
            let paramChar = ( makeParamChar this )
            let numCols = Columns< ^T > this |> Seq.length
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
            printfn "Param count: %A" cmd.Parameters.Count
            
            for i in [0..cmd.Parameters.Count-1] do 
                printfn "Param %d - %A: %A" i cmd.Parameters[i].ParameterName cmd.Parameters[i].Value
#endif
            let result = 
                exceptionHandler ( fun ( ) -> cmd.ExecuteNonQuery ( ) ) 
    
            conn.Close( )
            result
        | Error e -> Error e

module DSL = 
    open Orm

    type ConjunctionState = 
        | Open 
        | None 
        | Close
        
        // member inline this.Compile = 
        //     match this with 
        //     | First c -> c.Name + " Like '%" + c.Value.ToString( ) + "%'"
        //     | Or c -> " or " + c.Name + " Like '%" + c.Value.ToString( ) + "%'"
        //     | And c -> " And " + c.Name + " Like '%" + c.Value.ToString( ) + "%'"

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

        member this.Value ( state : OrmState ) = 
            match this with 
            | Equals v -> "= " + v 
            | NotEquals v -> "<> " + v 
            | GreaterThan v -> "> " + v 
            | GreaterThanOrEqualTo v -> ">= " + v 
            | LessThan v -> "< " + v 
            | LessThanOrEqualTo v -> "<= " + v 
            | Is v -> "IS " + v 
            | Exists v -> "Exists " + v 
            | Between v -> "Between " + v 
            | In v -> "IN " + v 
            | Like v -> "Like " + v 
            | ILike v -> "ILike " + v 

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
            | And ( c, pred ) -> $" And {sqlQuote c state} {pred.Value state}"
            | Parenthesize cons -> 
                cons
                |> Seq.map ( fun x -> x.Compile state ) 
                |> Seq.fold ( fun acc x -> acc + x ) "( "
                |> fun x -> x + " )"

    let compile ( conjunctions : Conjunction seq ) ( state : OrmState  ) = 
        conjunctions
        |> Seq.map ( fun x -> x.Compile state )
        |> String.concat " "

    let inline Select< ^T > ( state : OrmState ) = 
        Select ( "Select " + ( String.concat ", " ( Columns< ^T > state ) ) )
    
    let inline From< ^T > ( state : OrmState ) = 
        From ( "From " + ( Table< ^T > state ) )

    let inline Join< ^T > ( conjunctions : Conjunction seq ) ( state : OrmState ) = //Join "Payments.User" ON Col1 = Col2 And Col3 = 5
        Join ( "Join " + ( Table< ^T > state ) + " ON " + compile conjunctions state )
    
    let inline Where ( conjunctions : Conjunction seq ) ( state : OrmState ) = 
        Where ( "Where " + compile conjunctions state )
    
    let inline GroupBy ( cols: string seq ) ( state : OrmState ) = 
        GroupBy ( "Group By " + ( String.concat ", " cols ) )

    let inline OrderBy ( cols: ( string * Order option ) seq ) ( state : OrmState ) =
        let ordering = 
            cols 
            |> Seq.map ( fun x -> 
                let direction = 
                    match snd x with 
                    | Some order -> 
                        match order with 
                        | Ascending -> " Asc"
                        | Descending -> " Desc"
                    | _ -> ""
                $"{fst x}{direction}"
            ) 
            |> String.concat ", "
        OrderBy ( "Order By " + ordering )

    let inline skip ( Nyn : int ) ( state : OrmState ) =
        Skip ( $"OFFSet {Nyn}" )

    let inline take ( Nyn : int ) ( state : OrmState ) =
        Take ( $"Limit {Nyn}" )

    type ClauseState = 
        | TakesOrm of ( OrmState -> Clause )
        | Doesnt of Clause

    type Query = 
        { clauses: ( OrmState -> Clause ) list }
        member this.Compile ( state : OrmState ) =
            List.fold ( fun acc ( elem : ( OrmState -> Clause ) ) -> if acc = "" then ( elem state ).Compile  else acc + "\n" + ( elem state ).Compile ) "" this.clauses

    type UnionAll =
        { queries: Query list }
        member this.Compile ( state : OrmState ) =
            List.fold ( fun acc ( elem : Query ) -> if acc = "" then elem.Compile state else acc + "\n\nUnion All\n\n" + elem.Compile state ) "" this.queries



    type IDataTransferObject =
        interface
        end

module SqlGeneration = 
    open FSharp.Reflection
    
    let queries = []


    // queries.add <- "select xyz from table a"

    // queries.add <- "select xc from table a left join b on asdfasdf"

    // queries.compile <- "alter table add column"

    exception InvalidAlias of string

    type Select = Unit
    type Insert = Unit
    type Update = Unit
    type Delete = Unit
    
    type From = Unit
    type Where = Unit
    type GroupBy = Unit
    type OrderBy = Unit
    type Having = Unit
    type Limit = int
    type OffSet = int

    type KeyWord = 
        | Add                   // 	Adds a Column in an existing Table
        | AddConstraint         // 	Adds a Constraint after a Table is already Created
        | All                   // 	Returns true if All of the subquery Values meet the condition
        | Alter                 // 	Adds, Deletes, or modifies Columns in a Table, or changes the data type of a Column in a Table
        | AlterColumn           // 	Changes the data type of a Column in a Table
        | AlterTable            //  Adds, Deletes, or modifies Columns in a Table
        | And                   // 	Only includes Rows Where both conditions is true
        | Any                   // 	Returns true if Any of the subquery Values meet the condition
        | As                    // 	Renames a Column or Table with an aliAs
        | Asc                   // 	Sorts the result Set in Ascending Order
        | BackupDatabase        // 	Creates a back up of an existing Database
        | Between               // 	Selects Values within a given range
        | Case                  // 	Creates different outputs bAsed on conditions
        | Check                 // 	A Constraint that Limits the value that can be placed in a Column
        | Column                // 	Changes the data type of a Column or Deletes a Column in a Table
        | Constraint            // 	Adds or Deletes a Constraint
        | Create                // 	Creates a Database, Index, View, Table, or Procedure
        | CreateDatabase        // 	Creates a new SQL Database
        | CreateIndex           // 	Creates an Index on a Table ( Allows duplicate Values )
        | CreateOrReplaceView   // 	Updates a View
        | CreateTable           // 	Creates a new Table in the Database
        | CreateProcedure       // 	Creates a stored Procedure
        | CreateUniqueIndex     // 	Creates a Unique Index on a Table ( no duplicate Values )
        | CreateView            // 	Creates a View bAsed on the result Set of a Select statement
        | Database              //	Creates or Deletes an SQL Database
        | Default               // 	A Constraint that provides a Default value for a Column
        | Delete                // 	Deletes Rows From a Table
        | Desc                  // 	Sorts the result Set in Descending Order
        | Distinct              // 	Selects only Distinct ( different ) Values
        | Drop                  // 	Deletes a Column, Constraint, Database, Index, Table, or View
        | DropColumn            // 	Deletes a Column in a Table
        | DropConstraint        // 	Deletes a Unique, PrimaryKey, FOREIGN KEY, or Check Constraint
        | DropDatabase          // 	Deletes an existing SQL Database
        | DropDefault           // 	Deletes a Default Constraint
        | DropIndex             // 	Deletes an Index in a Table
        | DropTable             // 	Deletes an existing Table in the Database
        | DropView              // 	Deletes a View
        | Exec                  // 	Executes a stored Procedure
        | Exists                // 	Tests for the existence of Any record in a subquery
        | ForeignKey            // 	A Constraint that is a key used to link two Tables together
        | From                  // 	Specifies which Table to Select or Delete data From
        | FullOuterJoin         // 	Returns All Rows when there is a match in either Left Table or Right Table
        | GroupBy               // 	Groups the result Set ( used with aggregate functions: COUNT, MAX, MIN, SUM, AVG )
        | Having                // 	Used instead of Where with aggregate functions
        | In                    // 	Allows you to specify multiple Values in a Where clause
        | Index                 // 	Creates or Deletes an Index in a Table
        | InnerJoin             // 	Returns Rows that have matching Values in both Tables
        | InsertInto            // 	Inserts new Rows in a Table
        | InsertIntoSelect      // 	Copies data From one Table Into aNother Table
        | IsNull                //	Tests for empty Values
        | IsNotNull             // 	Tests for non-empty Values
        | Join                  // 	Joins Tables
        | LeftJoin              // 	Returns All Rows From the Left Table, And the matching Rows From the Right Table
        | Like                  // 	Searches for a specified pattern in a Column
        | Limit                 // 	Specifies the Nynber of records to return in the result Set
        | Not                   // 	Only includes Rows Where a condition is Not true
        | NotNull               // 	A Constraint that enforces a Column to Not accept Null Values
        | Or                    // 	Includes Rows Where either condition is true
        | OrderBy               // 	Sorts the result Set in Ascending or Descending Order
        | OuterJoin             // 	Returns All Rows when there is a match in either Left Table or Right Table
        | PrimaryKey            // 	A Constraint that Uniquely identifies each record in a Database Table
        | Procedure             // 	A stored Procedure
        | RightJoin             // 	Returns All Rows From the Right Table, And the matching Rows From the Left Table
        | RowNyn                // 	Specifies the Nynber of records to return in the result Set
        | Select                // 	Selects data From a Database
        | SelectDistinct        // 	Selects only Distinct ( different ) Values
        | SelectInto            // 	Copies data From one Table Into a new Table
        | SelectTop             // 	Specifies the Nynber of records to return in the result Set
        | Set                   // 	Specifies which Columns And Values that should be Updated in a Table
        | Table                 // 	Creates a Table, or adds, Deletes, or modifies Columns in a Table, or Deletes a Table or data inside a Table
        | Top                   // 	Specifies the Nynber of records to return in the result Set
        | TruncateTable         // 	Deletes the data inside a Table, but Not the Table itself
        | Union                 // 	Combines the result Set of two or more Select statements ( only Distinct Values )
        | UnionAll              // 	Combines the result Set of two or more Select statements ( Allows duplicate Values )
        | Unique                // 	A Constraint that ensures that All Values in a Column are Unique
        | Update                // 	Updates existing Rows in a Table
        | Values                // 	Specifies the Values of an Insert Into statement
        | View                  // 	Creates, Updates, or Deletes a View
        | Where                 // 	Filters a result Set to include only records that fulfill a specified condition

    type Expr = 
        | Create of string 
        | Exprs of Expr seq

    type DDL = 
        | Create
        | Drop 
        | Alter 
        | Truncate

    type DML =
        | Insert 
        | Update 
        | Delete 
        // | CAll 
        // | ExplainCAll
        // | Lock 

    // type TCL =
    //     | Commit 
    //     | SavePoint
    //     | RollBack
    //     | SetTransaction 
    //     | SetConstraint

    type DQL =
        | Select 

    type DCL =
        | Grant 
        | Revoke

    type Command = 
        | DDL of DDL
        | DML of DML 
        // | TCL of TCL
        // | DQL of DQL 
        | DCL of DCL

    type DataType = 
        | Int
        | BigInt
        | Float
        | Double
        | Bit of int option
        | BitVarying of int option
        | Boolean 
        | Character of int 
        | CharacterVarying of int 
        | Date 
        | Time 
        | DateTime
        | Numeric of int * int 

    type Column = 
        { Name : string 
          DataType : DataType 
        }

    // type Table = 
    //     { Name : string 
    //       Columns : Column array
    //     }

    //first string is F# name, second is aliAs
    type Alias = string * string
    
    /// Computes a value that may thRow an exception at instantiation... which should be at startup. 
    type Table ( ty : Type, TableAliAs : Alias option, ColumnAliases : Alias seq option ) = 
        let TableName = 
            match TableAliAs with 
            | Some name -> snd name 
            | None -> ty.Name

        /// ThRows exception at start up if aliAses are given but Not matched with source.
        let ColumnNames = 
            match ColumnAliases with
            | Some aliAses ->
                let comp = Set.ofSeq ( Seq.map fst aliAses )
                let mapper = Map.ofSeq aliAses
                let names = 
                    FSharpType.GetRecordFields typeof< ^T > 
                    |> Array.map ( fun x -> x.Name )
                    |> Set.ofArray

                if Set.count comp <> Set.count ( Set.intersect comp names ) 
                then 
                    sprintf "Unable to match an aliAs provided %A with members %A on %s" comp names ty.Name |> InvalidAlias |> raise 
                else 
                    FSharpType.GetRecordFields typeof< ^T > 
                    |> Array.map ( fun x -> x.Name )
                    |> Array.map ( fun x -> if Map.containsKey x mapper then Map.find x mapper else x )
                
            | None -> FSharpType.GetRecordFields typeof< ^T > |> Array.map ( fun x -> x.Name )

        member _.MakeTable ( ) =
            sprintf "Create Table %s "


///<Description>An attribute type which specifies a Column name</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ForeignKeyAttribute( Table : ^T, Column : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( Column,  ( box( context ) :?> DbContext )  |> EnumToValue )
    member _.Table = Table
    member _.Column = Table 