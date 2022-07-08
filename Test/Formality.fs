module HCRD.FORM.Tests.Formality

open Form
open Ality
open HCRD.FORM.Tests.Setup 
open NUnit.Framework

let dumbyState = MSSQL("", DbContext.Default)

let areEqual expect got =
    Assert.AreEqual( expect, got )

[<SetUp>]
let Setup () = ()


[<Test>]
let simpleComp () = 
    dumbyState
    |> (First ( Single ( "Col1", Equals "3" ))).Compile
    |> areEqual "[Col1] = 3"

[<Test>]
let fullComp () =
    dumbyState
    |> (First 
            ( Many   
                [ First ( Single ("Col1", NotEquals "'mycat'"))
                ; And ( Single ("Col2", GreaterThan "42"))
                ; Or ( Many
                        [ Parenthesize 
                            ( Many 
                                [ First ( Single ("Col3", Equals "1"))
                                ; And ( Single ("Col4", Equals "5"))
                                ]
                            )
                        ]
                     )
                ]
            )
        ).Compile 
    |> fun x -> printfn "%s" x; areEqual "[Col1] <> 'mycat' AND [Col2] > 42 OR ([Col3] = 1 AND [Col4] = 5)" x

// let inline first ( input : ^T ) = 
//     match typeof< ^T > with
//     | t when t = typeof< Conjunction seq > -> First (Many input)
//     | t when t = typeof< (string * Predicate) > -> First (Single input)

[<Test>]
let fullComp2 () =
    seq { "Col3" >>= "1" |> firstSingle; "Col4" <<= "5" |> andSingle }
    |> Many
    |> Parenthesize 
    |> fun x -> seq {x}
    |> Many
    |> fun x -> 
        seq { "Col1" != "'mycat'" |> firstSingle
            ; "Col2" >>>> "42" |> andSingle
            ; Or x }
    |> Many 
    |> First 
    |> fun x -> x.Compile dumbyState
    |> fun x -> printfn "%s" x; areEqual "[Col1] <> 'mycat' AND [Col2] > 42 OR ([Col3] >= 1 AND [Col4] <= 5)" x  


let (&&&) = 


[<Test>]
let turst () =
    "mii" >>= "you"
    |> firstManyThenAndSingle 
    |> Conjunction.compile dumbyState
    |> printfn "%A"

[<Test>]
let fullComp5Real3Mii () =
    dumbyState 
    |> ( firstMany [ firstSingle <| "Col1" ->> "(1,2,3)"
                   ; andMany [ parenthesizeMany <|  [ "Col2" >-< "(2,3)" |> firstSingle
                                                    ; "Col3" != "'bonjour'" |> andSingle 
                                                    ;  "Col4" %% "'%yeet%'" |> orSingle
                                                    ]
                            ]
                        ] ).Compile 
    |> printfn "%s"

