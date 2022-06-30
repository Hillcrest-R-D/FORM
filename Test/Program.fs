open Form
open Orm
open dotenv.net
open System
open Microsoft.FSharp.Core.LanguagePrimitives
open Microsoft.FSharp.Reflection

//DotEnv.Load( new DotEnvOptions( envFilePaths = ["../.env"], ignoreExceptions = false ) )

[<Flags>]
type Contexts =     
    | TestContext1 = 1
    | TestContext2 = 2
    | Auth = 3

let connectionString = ""


let auth = PSQL( connectionString, Contexts.Auth)

let test = SQLite( "Data Source=./test.db;", Contexts.TestContext1 )

printfn "%A" ((box(Contexts.TestContext2) :?> DbContext))


let testState1 = PSQL( "TestContext1", Contexts.TestContext1 )
let testState2 = PSQL( "TestContext2", Contexts.TestContext2)

// [<AttributeUsage(AttributeTargets.Class)>]
// type TablesAttribute( alias : string, ctx : ^T when ^T :> Enum) = 
//     inherit Attribute()
//     member _.Value = (alias,  EnumToValue ctx)

[<Table("Tenant" , Contexts.TestContext1 )>]
[<Table("Tenant", Contexts.TestContext2)>]
type Tenant = 
    { 
      [<Column("Id", Contexts.TestContext1)>]
      [<Column("Id", Contexts.TestContext2)>]
      id: string 
    }

    static member getColumnInfo name =
        FSharpType.GetRecordFields typedefof< Tenant > 
        |> Array.find (fun x -> x.Name = name)

    // [<Column()>] 
    // static member Id state = state.id


[<Table("User" , Contexts.TestContext1 )>]
type User = 
    { 
      [<Column("Id", Contexts.TestContext1)>]
      [<Key(Key.Primary, Contexts.TestContext1)>]  
      Id : string 
      [<Key(Key.Foreign,Contexts.TestContext1)>]
      [<ForeignKey(typeof<Tenant>, "Id", Contexts.TestContext1)>]
      TenantId : string
      [<Column("Login", Contexts.TestContext1)>]
      Login : string
      [<Column("Password", Contexts.TestContext1)>]
      Password : string
      Locale : int option
      TimeZone  : string option
      Salt : string
    }

let createTable = 
    "
    drop table if exists User;
    create table User (
        Id varchar(32) not null,
        TenantId varchar(32) not null,
        Login varchar(64) not null,
        Password varchar(512) not null,
        Locale int default null, 
        TimeZone varchar(64) default null,
        Salt varchar(128) not null
    );
    
    insert into user 
    values ( 'HotDog', '!HotDog', 'yamothawasahampsta', 'asdfastesfdvrrtyhrefsfsfdhe34tadfgdg', null, null, 'asdfasdfhafsdr' ); "




Orm.execute createTable test |> printfn "%A"
Orm.selectAll< User > test
    |> function 
    | Ok data -> 
        Array.head data 
        |> fun x -> printfn "%A" x; Orm.insert<User> x test
        |> printfn "%A"
        
        selectAll< User > test |> printfn "%A"
    | Error e ->
        printfn "%A" e
// let query = 
//     { clauses = 
//         [ select<User>
//         ; from<User>
//         ; join<User> [First ("User.Col1", Equals "2")]
//         ] 
//     }.Compile testState1
// let query2 =
//     { clauses = 
//         [ select<User>
//         ; from<User>
//         ] 
//     }.Compile testState2
// printfn "%A\n\n%A" query query2
// printfn "%A" (System.Attribute.GetCustomAttributes(typedefof<User>, typedefof<TableAttribute>, false))
// printfn "%A" (typedefof<User>.GetCustomAttributes(typedefof<TableAttribute>,false))
// Orm.queryBase< User > testState2 |> printfn "queryBase: %A"

// let conditionals = 
//         [ Parenthesize 
//             [ Parenthesize 
//                 [ First ("Col1", Equals "1") 
//                 ; Or ("Col2", Equals "5")
//                 ] 
//             ; And ("Col3", Equals "5")
//             ] 
//         ; Or ("Col4", Equals "0")
//         ]

// printfn "%A" conditionals
// printfn "%A" ( conditionals |> compile )

// Orm.selectAll<User> auth 
// |> function 
// | Ok data -> 
//     Array.head data 
//     |> fun x -> printfn "%A" x; Orm.insert<User> x auth
//     |> printfn "%A"
// | Error e ->
//     printfn "%A" e


//insert into "public"."authuser"( "Id", "TenantId", "Login", "Password", "Locale", "TimeZone", "Salt" ) values ( ?, ?, ?, ?, ?, ?, ? )


