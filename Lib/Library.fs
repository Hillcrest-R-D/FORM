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
// type SchemaAttribute( alias : string, context : obj ) = 
//     inherit DbAttribute( )
//     override _.Value = ( alias, ( box( context ) :?> DbContext )  |> EnumToValue )
    

///<Description>An attribute type which specifies a Table name</Description>
[<AttributeUsage( AttributeTargets.Class, AllowMultiple = true )>]
type TableAttribute( alias : string , context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( alias, ( context :?> DbContext )  |> EnumToValue )
    member _.Context = ( context :?> DbContext )  |> EnumToValue


///<Description>An attribute type which specifies a Column name</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ColumnAttribute( alias : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( alias,  ( context :?> DbContext )  |> EnumToValue )

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
type IdAttribute(context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( "index",  ( context :?> DbContext )  |> EnumToValue )

[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type SQLTypeAttribute( definition : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( definition,  ( context :?> DbContext )  |> EnumToValue )
    
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type UniqueAttribute( grouping : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( grouping,  ( context :?> DbContext )  |> EnumToValue )
    member _.Group = grouping
    
///<Description>A record type which holds the information required to map across BE And DB. </Description>
type SqlMapping = { 
    Index : int
    IsKey : bool
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
    | ODBC      of ( string * Enum ) // SQL Driver = SQL Server Native 11.0
  
