```fsharp
[Ok Articles; Ok User;]

type Query = Context -> DbConnection -> DbTransaction -> ( string | DbCommand ) seq -> Result<IDataReader, exn> seq
type Command = Context -> DbConnection -> DbTransaction -> ( string | DbCommand ) seq -> Result<int, exn> seq
type Query = DbCommand -> DbConnection -> DbTransaction -> IDataReader | int
module Unbatchable =
    let selectAll<^T> db =
        Orm.selectAll<^T> db |> executeAndConsume<^T> // -> Result<seq<User>, exn>
insert<User> user db |> execute // -> Result<int, exn> 

    batch [insert<Article> article; insert<ArticleUser> articleUser; selectWhere<> "whatever"] db // -> Result  -->

    fun context -> 
        fun connection ->
            fun transaction ->
                fun querys ->
                    let cmds = querys |> Seq.map toCmd
                    let mutable errored = false
                    seq { for cmd in cmds do 
                        if not errored 
                        then 
                            match cmd.Execute() with 
                            | Ok state -> 
                                yield Ok state 
                            | Error e -> 
                                errored <- true
                                transaction.Rollback()
                                yield Error e
                                
                            [Ok 1; Ok IDataReader; Error e]
                    }
                    type QueryReturn =
                        | int
                        | IDataReader
                    
                    |> Seq.takeWhile
                        match query with 
                        | str -> buildCommand(str)
                        | cmd -> ()
                        |> fun cmd -> cmd.Execute
                        |> function 
                        | Ok _ -> true
                        | Error _ -> 
                            transaction.Rollback()
                            false
                    |> Seq.fold (
                        fun element state ->
                            match state with 
                            | Ok state ->
                                state @ element.Execute() |> Ok
                            | Error e -> 
                                transaction.Rollback
                                Error e
                    [Ok 1; Ok IDataReader; Error exn; Error exn]
                    ) (Ok 0)
                   

consumeReaders : seq<QueryResults> -> seq<>
    match input with 
    | IDataReader ->
        consume it
    | int ->
       Error exn "Can't consume int"
    [Ok 1]
type QueryResult =
    | Int of int 
    | DataReader of IDataReader * Type
    | Thing of obj * Type
batch seq {insert<User> user db; selectWhere<User> "id = 1" db, User; delete<User> user db} // [Int 1; (DataReader dr, User); (DataReader dr, Article); Int 1]
|> map 
    function
    | Ok el ->
        match el with 
        | Int i -> Int i
        | DataReader (dr,type_) -> Thing <| consumeReader<type_> dr, type_  

match Unbatchable.selectAll<User> db with 
| Ok result -> 
    match result with 
    | Int i -> 
    | Reader reader -> consumeReader<User> reader
| Error e -> Error e

Batchable.selectAll<User> db -->
```