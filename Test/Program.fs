open Form
open dotenv.net

DotEnv.Load( new DotEnvOptions( envFilePaths = ["../.env"], ignoreExceptions = false ) )
    

type A = 
    | Auth
    | Payments
    type B =
        { Thing : string}

    let test = { Thing = "test" }

A.test |> printfn "%A"

// type TestContext =
//     | Auth // of DbContext
//     | Payments // of DbContext
 
// let [<Literal>] context = TestContext()
// let auth = PSQL( "", context.Auth)
// let payments = PSQL( "", context.Payment)

// [<Table(Auth "AuthUser")>]
// [<Table("PaymentsUser", paymentContext)>]
// type User = 
//     { [<Column("AuthId", authContext)>]
//       Id : int 
//     }


// Orm< User >.queryBase auth |> printfn "%s"f