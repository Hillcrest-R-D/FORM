namespace Form.Attributes

open System
open System.Reflection
open Microsoft.FSharp.Core.LanguagePrimitives


type DbContext =
    | Default = 0
 
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
    abstract Value : ( string * obj )
    abstract Context : System.Enum
    
///<summary>An attribute type which specifies a schema name</summary>
///<Warning>Not Implemented into Form, no testing has been performed for this</Warning>
[<AttributeUsage( AttributeTargets.Class, AllowMultiple = true )>]
type SchemaAttribute( alias : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( alias, context )
    override _.Context = unbox context
/// <summary>Place on a record type to specify what (table * context) the type relates to. Multiple TableAttributes can be placed on a single type.</summary>
/// <remarks>Schemas and Catalogs can be directly specified using the named parameters <paramref name="Schema"/> and <paramref name="Catalog"/>, or by including the entire definition in the <paramref name="alias"/> parameter as "catalog.schema.table".</remarks>
/// <warning>Don't use <paramref name="Catalog"/> specifiers with an RDBMS that doesn't support cross-catalog relations, use contexts instead.</warning>
[<AttributeUsage( AttributeTargets.Class, AllowMultiple = true )>]
type TableAttribute( alias : string , context : obj) = 
    inherit DbAttribute( )
    override _.Value = ( alias, context )
    override _.Context = unbox context
    member val Schema = null
        with get,set
    member val Catalog = null
        with get,set

///<summary>An attribute type which specifies a Column name</summary>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ColumnAttribute( alias : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( alias,  context )
    override _.Context = unbox context

///<summary>Placed on a record-field to indicate the fields membership in the corresponding (table * context)'s primary key. When a primary key is comprised of multiple columns, define the <paramref name="Order"/> sequentially across the attributes in the same context.</summary>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type PrimaryKeyAttribute( name : string,  context : obj ) = 
    inherit DbAttribute( )
    override _.Value = name, box context
    override _.Context = unbox (context :?> DbContext)

    member _.ContextType = context.GetType().GetEnumName context
    member val Order = 0
        with get,set

/// <summary>Defines a constraint on a column.</summary>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ConstraintAttribute( definition : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( definition,  context )
    override _.Context = unbox context

/// <summary>Used to mark a single field per context as the identifier for a table, generally for a serial column. Usage is uncommon, use <see cref="PrimaryKeyAttribute"/> instead.</summary>
/// <seealso cref="PrimaryKeyAttribute"/>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type IdAttribute(context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( "serialId",  context )
    override _.Context = unbox context

/// <summary>Used to define the SQL type defintion of a fields corresponding column in a context.</summary>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type SQLTypeAttribute( definition : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( definition,  context )
    override _.Context = unbox context

/// <summary>Used to define a set of fields that represent a <c>UNIQUE</c> contraint on a table. Fields are grouped using the <paramref name="group"/> parameter.</summary>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type UniqueAttribute( group : string, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = (group,  context)
    override _.Context = unbox context
    
///<summary>An attribute type which specifies a fields membership in a foreign key.</summary>
///<param name="table">The System.Type of the record type corresponding to the table the foreign key maps to.</param>
///<param name="ColumnField">The name of the field corresponding to the attributed field.</param>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ForeignKeyAttribute( table : Type, context : obj ) = 
    inherit DbAttribute( )
    override _.Value = ( table.Name,  context )
    override _.Context = unbox context
    member _.table = table
    member val ColumnField = null
        with get,set   


type JoinDirection = 
    | Left = 0
    | Right = 1
    | Inner = 2
    | Outer = 3

type EvaluationStrategy = 
    | Strict = 0
    | Lazy = 1

///<summary>An attribute type which allows the specification of some FSharp Record Type fields being sourced via joinery</summary>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ByJoinAttribute ( table : Type, context : obj ) =
    inherit DbAttribute( )
    override _.Value = ( table.Name,  context )
    override _.Context = unbox context
    member _.table = table
    

///<summary>An attribute type which allows the specification of what fields/columns to join on to bring in ByJoin fields/columns.</summary>
///<seealso name="ByJoinAttribute"/>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type OnAttribute (table : Type, key : int, part : int, fieldName : string, kind : JoinDirection, context : obj ) =
    inherit DbAttribute( )
    override _.Value = ( table.Name, context)
    override _.Context = unbox context
    member _.table = table
    member _.key = key
    member _.part = part
    member _.kind = kind 
    member _.fieldName = fieldName

///<summary>What was this for again?</summary>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type ArgumentsAttribute ( keyId : int, context : obj ) =
    inherit DbAttribute( )
    override _.Value = ( "", context)
    override _.Context = unbox context
    member _.key = keyId

///<summary>Used to indicate that a Relationship should be evaluated lazily.</summary>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type LazyEvaluationAttribute () =
    inherit DbAttribute( )
    override _.Value = ( "", -1)
    override _.Context = DbContext.Default

type PrimaryKeyMember =
    {
        name : string
        order : int
    }
type ForeignKeyMember =
    {
        name : string
        order : int
        _type : System.Type
    }
type IndexMember =
    {
        name : string
        order : int
    }
type ByJoinColumn =
    {
        table : string
        column : string
        direction : JoinDirection
        _type : System.Type
    }

///<summary>Field level information used to package attribute info for use in mapping across the DB<->BE layers. </summary>
type SqlMapping = { 
    Index               : int
    PrimaryKey          : option<PrimaryKeyMember>
    SqlIndex            : option<IndexMember>
    ForeignKey          : option<ForeignKeyMember>
    IsRelation          : bool
    IsLazilyEvaluated   : bool
    JoinOn              : option<ByJoinColumn> 
    Source              : string
    QuotedSource        : string 
    SqlName             : string 
    QuotedSqlName       : string
    FSharpName          : string
    Type                : Type
    PropertyInfo        : PropertyInfo
}

///<summary>Stores the flavor And context used for a particular connection.
/// Takes the connection string and context.
///</summary>
type OrmState = 
    | MSSQL     of ( string * Enum )
    | MySQL     of ( string * Enum )
    | PSQL      of ( string * Enum )
    | SQLite    of ( string * Enum )
    | ODBC      of ( string * Enum ) // SQL Driver = SQL Server Native 11.0
  