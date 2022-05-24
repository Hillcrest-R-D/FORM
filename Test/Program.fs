open Form
open dotenv.net
open System
open Microsoft.FSharp.Core.LanguagePrimitives

//DotEnv.Load( new DotEnvOptions( envFilePaths = ["../.env"], ignoreExceptions = false ) )

[<Flags>]
type Contexts   =     
    | Auth  = 1
    | Payments  = 2

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


[<Table("PaymentsUser", Contexts.Payments)>]
[<Table( "AuthUser" , Contexts.Auth )>]
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

let test_user = {Id = 64; Login = "Me"; Password = "You!"}

printfn "The current context is: %A" (Orm.context<User> auth)

printfn "%A" (System.Attribute.GetCustomAttributes(typedefof<User>, typedefof<TableAttribute>))
printfn "%A" (typedefof<User>.GetCustomAttributes(typedefof<TableAttribute>,false))
Orm.queryBase< User > payments |> printfn "queryBase: %A"