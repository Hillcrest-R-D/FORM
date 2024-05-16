<<<<<<< HEAD
﻿module Test
open Expecto

[<EntryPoint>]
let main argv =
    Tests.runTestsInAssembly defaultConfig argv
=======
﻿namespace Form.Attributes.Test
open Expecto

module Main =

    [<EntryPoint>]
    let main argv =
        Tests.runTestsInAssemblyWithCLIArgs [] argv
>>>>>>> ef3c08d919ff69869e7858e66b1944d429bf46fb
