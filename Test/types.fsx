open FSharp.Reflection
open System

type My = { a : int option }

let my = { a = Some 1 }


let rec genericTypeString full ( _type : Type ) = 
    if not _type.IsGenericType 
    then _type.Name
    else 
        let typeName = 
            let mutable tmp = _type.GetGenericTypeDefinition().Name 
            tmp <- tmp.Substring(0, tmp.IndexOf('`'))
            tmp
        if not full 
        then typeName 
        else 
            let args = 
                _type.GetGenericArguments()
                |> Array.map (genericTypeString full)
                |> String.concat ","
            
            sprintf "%s<%s>" typeName args

FSharpType.GetRecordFields typedefof<My>
|> Array.iter( fun info ->  
    printfn "%A" ( genericTypeString false ( info.GetValue(my).GetType() ) )
)
