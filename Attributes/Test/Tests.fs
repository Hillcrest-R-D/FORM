module Tests

open Expecto
open Form.Attributes

[<Table("aTable", DbContext.Default, Schema = "testify")>]
type SomeRecord =
    {
        [<Column("Id", DbContext.Default)>]
        id : int
    }

[<Table("aTable", DbContext.Default, Schema = "testify")>]
type SomeClass(id) =
    [<Column("Id", DbContext.Default)>]
    member _.id = id

[<Tests>]
let tests =
  testList "samples" [
    testCase "universe exists (╭ರᴥ•́)" <| fun _ ->
      let subject = true
      Expect.isTrue subject "I compute, therefore I am."
  ]
