open Form
open Orm
open dotenv.net
open System
open Microsoft.FSharp.Core.LanguagePrimitives

//DotEnv.Load( new DotEnvOptions( envFilePaths = ["../.env"], ignoreExceptions = false ) )

[<Flags>]
type Contexts =     
    | Auth = 1
    | Payments = 2

type DbContexts =
    | Auth
    | Payments

let unionAsString case = 
    match case with
    | Auth -> "Auth"
    | Payments -> "Payments"

printfn "%A" ((box(Contexts.Payments) :?> DbContext))

// type TestContext =
//     | Auth // of DbContext
//     | Payments // of DbContext
// let [<Literal>] context = TestContext()
let auth = PSQL( "Auth", Contexts.Auth )
let payments = PSQL( "Payments", Contexts.Payments)

// [<AttributeUsage(AttributeTargets.Class)>]
// type TablesAttribute( alias : string, ctx : ^T when ^T :> Enum) = 
//     inherit Attribute()
//     member _.Value = (alias,  EnumToValue ctx)


[<Table("Payments.User", Contexts.Payments)>]
[<Table( "Auth.User" , Contexts.Auth )>]
type User = 
    { [<Column("Id", Contexts.Auth)>]
      [<Column("Id", Contexts.Payments)>]
      Id : int 
      [<Column("Login", Contexts.Auth)>]
      [<Column("Name", Contexts.Payments)>]
      Login : string
      [<Column("Password", Contexts.Auth)>]
      Password : string
    }



// 
// PaymentUser (Id, Name)
// AuthUser => Paymentuser (Password)

// let test_user = {Id = 64; Login = "Me"; Password = "You!"}

// printfn "The current context is: %A" (Orm.context<User> auth)

// Orm.selectAll< User > auth

let query = 
    { clauses = 
        [ select<User>
        ; from<User>
        ] 
    }.Compile auth
let query2 =
    { clauses = 
        [ select<User>
        ; from<User>
        ] 
    }.Compile payments
printfn "%A\n\n%A" query query2
// printfn "%A" (System.Attribute.GetCustomAttributes(typedefof<User>, typedefof<TableAttribute>, false))
// printfn "%A" (typedefof<User>.GetCustomAttributes(typedefof<TableAttribute>,false))
// Orm.queryBase< User > payments |> printfn "queryBase: %A"

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

printfn "%A" conditionals
printfn "%A" ( conditionals |> compile )