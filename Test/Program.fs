open Form
open dotenv.net
open System
open Microsoft.FSharp.Core.LanguagePrimitives

//DotEnv.Load( new DotEnvOptions( envFilePaths = ["../.env"], ignoreExceptions = false ) )

[<Flags>]
type Contexts   =     
    | Auth  = 1
    | Payments  = 2


printfn "%A" ((Contexts.Auth :> Enum))

// type TestContext =
//     | Auth // of DbContext
//     | Payments // of DbContext
// let [<Literal>] context = TestContext()
let auth = PSQL( "Auth", Contexts.Auth )
// let payments = PSQL( "", Payment)

[<AttributeUsage(AttributeTargets.Class)>]
type TablesAttribute( alias : string, ctx : Contexts) = 
    inherit Attribute()
    member _.Value = (alias, EnumToValue ctx)


//[<Table("PaymentsUser", Contexts.Payments)>]
[<Table("AuthUser", Contexts.Auth)>]
type User = 
    { //[<Column("AuthId", Contexts.Auth)>]
      Id : int 
    }

let test_user = {Id = 64}

printfn "%A" (System.Attribute.GetCustomAttributes(typedefof<User>, typedefof<TableAttribute>))
printfn "%A" (typedefof<User>.GetCustomAttributes(typedefof<TableAttribute>,false))
//Orm.columns< User > auth |> printfn "%A"