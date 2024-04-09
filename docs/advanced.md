# Advanced

There's really only one more thing to learn about FORM. As we said, the insert, update, and delete function always have a transaction associated with them -- whether provided by you or created by them. If you have a sequence of items you're performing these actions on, it becomes extremely inefficient to constantly create and commit these transactions (and to also make all the network trips). So we've given you some functions specifically for sequences of data.

Using the setup from the last page, instead of calling insert for each record, we can simply write it like this

```fsharp
Form.Orm.insertMany state None true myUsers
```

We've found that packaging a bunch of rows into a single insert statement is significantly slower than executing an insert statement for each row of values, so insertMany essentially performs the latter; it will, however, generally perform better (not to mention be more ergonomic to the programmer) than iterating over a list and calling `Form.Orm.insert`. As we continue to experiment with this function, the goal is eventually to end up with a single call to the DB with all of the individual insert statements.

## Oneshot multi-op

A lot of the way FORM works is setup to work with piping as ergonomically as possible, consider the following statement (augmented from the last statement in [Basics](./basics.md)):

```fsharp
let transaction = Form.Orm.beginTransaction state

Form.Orm.selectWhere<User> state transaction ( """"id"=:1""", [| "3" |] ) 
|> Form.Orm.toResultSeq
|> Result.map ( Seq.head >> fun x -> { x with first = "Michelle" }  )
|> Result.bind ( Form.Orm.update<User> state transaction ) 
|> Result.map ( fun x -> Form.Orm.commitTransaction transaction; x ) 
|> Result.mapError ( fun e -> Form.Orm.rollbackTransaction transaction; e )
|> Result.bind ( fun x -> Form.Orm.selectWhere<User> state None ( """"id"=:1""", [| "3" |] ) |> Form.Orm.toResultSeq )
|> printfn "%A"
```

If everything goes well, we will perform the update, commit the transaction, and the updated user is returned (not that you would need to perform the select at the end in practice, since you would presumably have the updated user already in memory somewhere). In the error state, the transaction is rolled back and the failure message is carried forward. In general, we found that using Result.map(Error) with the transaction operations and having the result passed forward is the best way to both perform the commit/rollback while also carrying forward the result that dictated that we perform that operation (we could also introduce other checks, like ensuring the update actually impacted exactly 1 record, among other possibilities, before commiting).

One good way to jump into writing with FORM is to look at the Test section of the repository; the tests should be more or less fully representative of the functionality of FORM and main will always have 100% of the tests passing.