namespace Form 

module Logging = 
    open Microsoft.Extensions.Logging
    open Microsoft.Extensions.Logging.Console

    /// Ignore this. This is solely to give extra context to the default logger.
    /// Again, due to inlining constraints, we are unable to mark it as private.
    type Form = unit
    let mutable logger = 
        LoggerFactory.Create( fun builder -> builder.AddConsole() |> ignore ).CreateLogger<Form>()
    
    let inline log msg = 
        #if DEBUG 
            printfn "%s" msg
        #endif  
            ()