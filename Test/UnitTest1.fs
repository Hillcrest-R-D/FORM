module Test

open NUnit.Framework

[<SetUp>]
let Setup () =
    ()

let inline passIfTrue cond = 
    if cond then
        Assert.Pass()
    else 
        Assert.Fail()

[<Test>]
let Test1 () =
    Assert.Pass()

[<Test>]
let VerifyMath () =
    passIfTrue ( 1+1 = 2 )

[<Test>]
let VerifyMath2 () =
    passIfTrue ( 1+1 = 3 )
    
[<Test>]
let VacuousFailure () = 
    passIfTrue ("!yourmom" = "!notyourmom") 

