namespace Form.Attributes

open System
open System.Reflection
open Microsoft.FSharp.Core.LanguagePrimitives


type DbContext =
    | Default = 99
 
type FKProperty =
    | Cascade
    | SetNull
    | SetValue of string 

type FKType = 
    | Update of FKProperty
    | Delete of FKProperty

type ContextInfo = ( string * DbContext ) array

[<AbstractClass>]
type DbAttribute( ) = 
    inherit Attribute( )
    abstract Value : ( string * int )
    
///<Description>An attribute type which specifies a schema name</Description>
///<Warning>Not Implemented, don't bother using yet...</Warning>
[<AttributeUsage( AttributeTargets.Class, AllowMultiple = true )>]
type SchemaAttribute( alias : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( alias, ( box( context ) :?> DbContext )  |> EnumToValue )

///<Description>An attribute type which specifies a table name</Description>
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
type PrimaryKeyAttribute( name : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = name, ( context :?> DbContext )  |> EnumToValue 
    
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
type UniqueAttribute( group : string,context : obj ) = 
    inherit DbAttribute( )
    override _.Value = (group,  ( context :?> DbContext )  |> EnumToValue)

[<AttributeUsage( AttributeTargets.Property, AllowMultiple = false )>]
type LazyAttribute() = 
    inherit DbAttribute( )
    override _.Value = ("lazy",  -1)
    
///<Description>An attribute type which specifies a Column name</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ForeignKeyAttribute( table : obj, column : string, properties : obj, field: string,  context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( column,  ( box( context ) :?> DbContext )  |> EnumToValue )
    member _.table = table
    member _.column = column   


type JoinDirection = 
    | Left = 0
    | Right = 1
    | Inner = 2
    | Outer = 3

type EvaluationStrategy = 
    | Strict = 0
    | Lazy = 1

///<Description>An attribute type which allows the specification of some FSharp Record Type fields being sourced via joinery</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ByJoinAttribute ( table : Type, context : obj ) =
    inherit DbAttribute( )
    override _.Value = ( table.Name,  ( box( context ) :?> DbContext )  |> EnumToValue )
    member _.table = table
    

///<Description>An attribute type which allows the specification of what fields/columns to join on to bring in ByJoin fields/columns... see ByJoinAttribute</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type OnAttribute (table : Type, key : int, part : int, on : string, kind : JoinDirection, context : obj ) =
    inherit DbAttribute( )
    override _.Value = ( table.Name, ( box( context ) :?> DbContext )  |> EnumToValue)
    member _.key = key
    member _.part = part
    member _.kind = kind 
    member _.on = on

///<Description>An attribute type which allows the specification of what fields/columns to join on to bring in ByJoin fields/columns... see ByJoinAttribute</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ArgumentsAttribute ( evaluationStrategy : EvaluationStrategy, keyId : int, context : obj ) =
    inherit DbAttribute( )
    override _.Value = ( "", ( box( context ) :?> DbContext )  |> EnumToValue)
    member _.key = keyId
    member _.evaluationStrategy = evaluationStrategy 

///<Description>A record type which holds the information required to map across BE And DB. </Description>
type SqlMapping = { 
    Index : int
    IsKey : bool
    IsIndex : bool
    JoinOn : ( string * string ) option 
    Source : string
    QuotedSource : string 
    SqlName : string 
    QuotedSqlName : string
    FSharpName : string
    Type : Type
    PropertyInfo: PropertyInfo
}

///<Description>Stores the flavor And context used for a particular connection.
/// Takes the connection string and context.
///</Description>
type OrmState = 
    | MSSQL     of ( string * Enum )
    | MySQL     of ( string * Enum )
    | PSQL      of ( string * Enum )
    | SQLite    of ( string * Enum )
    | ODBC      of ( string * Enum ) // SQL Driver = SQL Server Native 11.0
  