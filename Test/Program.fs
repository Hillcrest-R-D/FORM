open Form
open Orm
open dotenv.net
open System
open Microsoft.FSharp.Core.LanguagePrimitives

//DotEnv.Load( new DotEnvOptions( envFilePaths = ["../.env"], ignoreExceptions = false ) )

[<Flags>]
type Contexts =     
    | TestContext1 = 1
    | TestContext2 = 2
    | Auth = 3

let connectionString = ""


let auth = PSQL( connectionString, Contexts.Auth)

printfn "%A" ((box(Contexts.TestContext2) :?> DbContext))


let testState1 = PSQL( "TestContext1", Contexts.TestContext1 )
let testState2 = PSQL( "TestContext2", Contexts.TestContext2)

// [<AttributeUsage(AttributeTargets.Class)>]
// type TablesAttribute( alias : string, ctx : ^T when ^T :> Enum) = 
//     inherit Attribute()
//     member _.Value = (alias,  EnumToValue ctx)


[<Table("TestContext2.User", Contexts.TestContext2)>]
[<Table( "TestContext1.User" , Contexts.TestContext1 )>]
[<Table( "public.authuser" , Contexts.Auth )>]
type User = 
    { 
      [<Column("Id", Contexts.TestContext1)>]
      [<Column("Id", Contexts.TestContext2)>]
      Id : string 
      TenantId : string
      [<Column("Login", Contexts.TestContext1)>]
      [<Column("Name", Contexts.TestContext2)>]
      Login : string
      [<Column("Password", Contexts.TestContext1)>]
      Password : string
      Locale : int option
      TimeZone  : string option
      Salt : string
    }




let query = 
    { clauses = 
        [ select<User>
        ; from<User>
        ; join<User> [First ("User.Col1", Equals "2")]
        ] 
    }.Compile testState1
let query2 =
    { clauses = 
        [ select<User>
        ; from<User>
        ] 
    }.Compile testState2
// printfn "%A\n\n%A" query query2
// printfn "%A" (System.Attribute.GetCustomAttributes(typedefof<User>, typedefof<TableAttribute>, false))
// printfn "%A" (typedefof<User>.GetCustomAttributes(typedefof<TableAttribute>,false))
// Orm.queryBase< User > testState2 |> printfn "queryBase: %A"

let conditionals = 
        [ Parenthesize 
            [ Parenthesize 
                [ First ("Col1", Equals "1") 
                ; Or ("Col2", Equals "5")
                ] 
            ; And ("Col3", Equals "5")
            ] 
        ; Or ("Col4", Equals "0")
        ]

// printfn "%A" conditionals
// printfn "%A" ( conditionals |> compile )

Orm.selectAll<User> auth 
|> function 
| Ok data -> 
    Array.head data 
    |> fun x -> printfn "%A" x; Orm.insert<User> x auth
    |> printfn "%A"
| Error e ->
    printfn "%A" e


//insert into "public"."authuser"( "Id", "TenantId", "Login", "Password", "Locale", "TimeZone", "Salt" ) values ( ?, ?, ?, ?, ?, ?, ? )


