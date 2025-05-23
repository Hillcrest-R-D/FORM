namespace Form.Attributes.Test
open Expecto

module Main =

    [<EntryPoint>]
    let main argv =
        Tests.runTestsInAssemblyWithCLIArgs [] argv
