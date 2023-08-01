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


type JoinDirection = 
    | Left = 0
    | Right = 1
    | Inner = 2
    | Outer = 3
// [<Table("Article", Contexts.Main)>]
// type Article = 
//     {
//         id : string
//         specialId : string
//         body : string
//         [<Join("Comments", [("id", "commentId"), ("specialId", "specialId")], kind, Contexts.Main)>]
//         comments : Comment seq
//     }

// [<Table("Article", Contexts.Main)>]
// type ArticleByProxy = 
//     {
//         id : string
//         specialId : string
//         body : string
//         [<Join( ( "Lookup", [( "articleId", "id")]), ("Comments", [("id", "commentId")]), kind, Contexts.Main)>]
//         comments : Comment seq
//     }


///<Description>An attribute type which allows an FSharp record type to be defined using sql joinery</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type Join ( table : Type, kind : JoinDirection, context : obj ) =
    inherit DbAttribute( )
    override _.Value = ( table.Name,  ( box( context ) :?> DbContext )  |> EnumToValue )
    member _.table = table
    member _.kind = kind 


///<Description>An attribute type which allows an FSharp record type to be defined using sql joinery</Description>
[<AttributeUsage( AttributeTargets.Property, AllowMultiple = true )>]
type On (table : Type, on : string, context : obj ) =
    inherit DbAttribute( )
    override _.Value = ( table.Name, ( box( context ) :?> DbContext )  |> EnumToValue)

[<Table("UserInfo", DbContext.Default)>]
type UserInfo =
    {
        userId : string 
        otherThing : string 
        name : string 
        email : string 
        phone : string 
    }

[<Table("Secrets", DbContext.Default)>]
type UserSecrets =
    {
        userId : string 
        password : string 
    }
[<Table("User", DbContext.Default)>]
type User = 
    {
        [<JoinKey(typeof<UserInfo>, "userId", DbContext.Default)>]
        id : string // User
        [<JoinKey(typeof<UserInfo>, "otherThing", DbContext.Default)>]
        secondaryKey: int
        [<Join(typeof<UserInfo>, JoinDirection.Left, DbContext.Default)>]
        info : UserInfo 
        // [<Join("UserInfo", [("userId", "id")], JoinDirection.Left, DbContext.Default)>]
        // name : string // UserInfo
        // [<Join("UserInfo", [("userId", "id")], JoinDirection.Left, DbContext.Default)>]
        // email : string // UserInfo
        // [<Join("UserInfo", [("userId", "id")], JoinDirection.Left, DbContext.Default)>]
        // phone : string 
        [<Join("Passwords", [("userId", "id")], JoinDirection.Left, DbContext.Default)>]
        secrets : UserSecrets // Passwords
    }

    static member from = 
        seq {
            (DbContexts.Default, typeof<UserInfo>) , "from User u JOIN UserInfo ui ON ui.userId = u.id JOIN UserSecrets us ON us.userId = u.id"
        }
        |> dict

    User.from<UserInfo>
        

///<Description>A record type which holds the information required to map across BE And DB. </Description>
type SqlMapping = { 
    Index : int
    IsKey : bool
    IsIndex : bool
    SqlName : string 
    QuotedSqlName : string
    FSharpName : string
    Type : Type
    PropertyInfo: PropertyInfo
}

// type 
// let db = {connectionString = conn; context = Contexts.Main; transaction = None }

// let db = Orm.beginTransaction db
// let selectResult = Orm.selectAll<User> db //Result<^T seq, exn>
// let deleteResult = Orm.delete<user> db ""

// db 
// |> Orm.beginTransaction
// |> Orm.selectAll<user>
// |> fun (res, db) -> (Orm.delete<User> db "", res) // -> ((res,db), res)   
// |> fun ((secondResult, db), firstResult) -> (firstResult, secondResult, Orm.commitTransaction db ) // (Result<User,exn>, Result<int, exn>, Result<int,exn>)


// let Orm.selectAll<^T> db = fun ( transaction : IDbTransaction option) -> { computation  }; 

// let tran = Orm.beginTransaction db 


// Orm.selectAll<User> db tran
// Orm.delete<User> "" db tran

// Orm.commitTransaction tran


///<Description>Stores the flavor And context used for a particular connection.
/// Takes the connection string and context.
///</Description>
type OrmState = 
    | MSSQL     of ( string * Enum )
    | MySQL     of ( string * Enum )
    | PSQL      of ( string * Enum )
    | SQLite    of ( string * Enum )
    // | ODBC      of ( string * Enum ) // SQL Driver = SQL Server Native 11.0
    
// type OrmState2 =
//     {
//         connectionString : string 
//         context : Enum 
//         transaction : Data.IDbTransaction option
//     }