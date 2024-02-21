open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations

type MyRecord = {
    name: string
}

let getPropertyInfo<'T> (expression: Expr<'T>) = 
    match expression with 
    | PropertyGet( _, propertyInfo, _ ) -> Some propertyInfo
    | _ -> None

printfn "%A" ( getPropertyInfo <@ fun x -> x.Name @> )