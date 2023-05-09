namespace Form.Attributes

open System
open System.Reflection
open FSharp.Reflection
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
    
///<Description>An attribute type which specifies a Column name</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ForeignKeyAttribute( table : obj, column : string, properties : obj, field: string,  context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( column,  ( box( context ) :?> DbContext )  |> EnumToValue )
    member _.table = table
    member _.column = column   

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
    // | ODBC      of ( string * Enum ) // SQL Driver = SQL Server Native 11.0
    
  