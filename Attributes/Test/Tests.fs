namespace Form.Attributes.Test

open Expecto
open Form.Attributes

module Tests = 
    open Utilities

    type Relation<^P, ^C>(keyId : int, state : OrmState) =
        let mutable value : Result<^C, exn> seq option = None
        member _.parent = typeof< ^P >
        member _.child = typeof< ^C >
        member _.keyId = keyId 
        member _.state = state
        interface IRelation

    type SubType =
        {
            id : int
        }
    [<Table("public.Test", Contexts.Default, Schema = "public")
    ; Table("Test", Contexts.Secondary, Schema = "HCRD")
    >]
    type Testabetical =
        {
            [<PrimaryKey("test_pk", Contexts.Default, Order = 1)>]
            id : int
            [<Column("different_name", Contexts.Default)>]
            differentName : string
            [<ByJoin(typeof<SubType>, Contexts.Default)>]
            lookupData : string
            autoRelation : Relation<Testabetical, SubType>
            [<LazyEvaluation>]
            lazyRelation : Relation<Testabetical, SubType>
        }

    [<Tests>]
    let tests =
        let testMapping = Utilities.columnMapping<Testabetical> dummbyState
        printfn "%A" testMapping
        testList "samples" [

            test "I am (should fail)" {
                Expect.isSome testMapping[0].PrimaryKey "id is Primary Key"
            }
        ]
