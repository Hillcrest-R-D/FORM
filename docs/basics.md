# The Basics

We said in the overview that FORM aims to be ergonomic. Staying true to this goal, we tried to make setting it up as easy as possible. However, FORM adds one layer of complexity that's, sometimes, lacking in other ORMs -- first-class support for multiple data sources. 

Form was bred from the need to quickly build bespoke data pipelines. Because of this, we have developed a strategy where the same record can represent the table layout for multiple databases and also have the ability to have database-specific identifiers for things like tables and column names. But, before we get into all that, let's do the minimal setup.

## Definitions 

```fs
let connectionString = "DataSource=./data.db"

type Context = 
    | Primary = 1

let state = Form.Attributes.OrmState.SQLite( connectionString, Context.Primary )

(*
    Even though this looks like a union type, it's actually an Enum. This is because 
    there is a limitation with Attributes and that you can only pass constants to them.
*)

let createTable = 
    """
    drop table if exists;
    create table user (
        id int not null,
        "first" varchar(32) not null,
        "last" varchar(32) not null
    )"""

type User = {
    id: int
    first: string
    last: string
}

(*
    This is a very basic setup. As of right now, we don't support a code-first, 
    however it's on the roadmap.
*)


(*  ! WARNING ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    This function, along with Form.Orm.executeReader, can be dangerous. 
    Never allow sql generate from user input to be executed by these two
    functions without meticulously sanitizing it first. Since we are in 
    full control of this statement, it's perfectly fine to use in this
    fashion.
*)
Form.Orm.execute state None createTable
|> printfn "%A"
```

Ok, you defined your data, setup the connection, even created the tables... now what? Let's create some fake data and insert it. 

```fs
let myUsers = [
    { id = 1; first = "Clarice"; last = "Johanssen" }
    { id = 2; first = "Jimothy"; last = "MacDermont" }
    { id = 3; first = "Michael"; last = "McDoesn'tExist" }
]

myUsers
|> Seq.map ( Form.Orm.insert state None true )
|> printfn "%A"

Form.Orm.selectAll<User> state None
|> printfn "%A"

```

But, as you develop, requirements change, you forgot you needed keys, maybe a few columns, and {some other thing here}. Let's fix our schema real quick.

```fs
(*
    Replace the User type above with this. And let's modify the table.
*)
type User = {
    [<Form.Attributes.PrimaryKey("pkey_user_id", Context.Primary)>]
    id: int
    [<Form.Attributes.Column("firstName", Context.Primary)>]
    first: string
    [<Form.Attributes.Column("lastName", Context.Primary)>]
    last: string
    email: string option
}

let createTable = 
    """
    drop table if exists user;
    create table user (
        id int not null,
        "firstName" varchar(32) not null,
        "lastName" varchar(32) not null,
        email varchar(64) null
    )"""

Form.Orm.execute state None createTable
|> printfn "%A"

let myUsers = [
    { id = 1; first = "Clarice"; last = "Johanssen"; email = None }
    { id = 2; first = "Jimothy"; last = "MacDermont"; email = None }
    { id = 3; first = "Michael"; last = "McDoesn'tExist"; email = Some "doesexist@doesntexist.com" }
]

(*
    Note the change here in the 3rd parameter. This is a flag to allow FORM
    to insert the keys into the table. If your database generates your keys,
    simply set the field to some dummy data and set this flag to false and
    the next time you read it from the db, you'll have your keys.
*)
Form.Orm.insertMany state None true myUsers 
|> printfn "%A"

Form.Orm.selectAll<User> state None
|> printfn "%A"
```

As you're looking this over, you might see some patterns in the API. This is by-design. Parameter ordering tries to keep the same pattern:

```fsharp
OrmState -> DbTransaction -> {BehaviorFlags} -> {^T State}
```
> Where BehaviorFlags and "^T State", here, are only required for a subset of the functions.

Beyond keeping the parameter ordering the same, we also kept the naming conventions the same:

```fs
Form.Orm.insert
Form.Orm.update
Form.Orm.delete
Form.Orm.selectWhere
Form.Orm.insertWhere
Form.Orm.updateWhere
Form.Orm.deleteWhere
Form.Orm.insertMany
Form.Orm.updateMany
Form.Orm.deleteMany
```

These names stay true to their SQL equivalents.

Let's try getting a single record from the db.

```fs
Form.Orm.selectWhere<User> state None ( """"id"=:1""", [| "3" |] )
```

Here, we are using the selectWhere function and passing arbitrary string to the where clause of the query. When we call the *Where functions, we need to separate out the string format and the data so that form is able to do some escaping to prevent sql injection. You can tell the formatter to place items based on preceding which element of the list it is with a colon. IE ":1" for the first element of the array, which, in this case, gets replaced by "3".

Now, let's do something a bit more complex. Let's take this record, update it, save it back to the db, and then read it back just to be sure.

```fs
Form.Orm.selectWhere<User> state None ( """"id"=:1""", [| "3" |] ) 
|> Result.map ( Seq.head >> fun x -> { x with first = "Michelle" }  )
|> Result.bind ( Form.Orm.update<User> state None  ) 
|> printfn "%A"

Form.Orm.selectWhere<User> state None ( """"id"=:1""", [| "3" |] ) 
|> printfn "%A"
(*
    Ok (seq [{ id = 3
           first = "Michelle"
           last = "McDoesn'tExist"
           email = Some "doesexist@doesntexist.com" }])
*)
```

That's great, but what if I want to make sure nothing affects my data until all changes are set? Well, we have support for transactions! See that `None`? That's a `DbTransaction Option`. So we simply need to call `Form.Orm.beginTransaction`. 

    Note, because the transactions take a DbTransaction Option, the return of this method is a DbTransaction Option.


```fs
let transaction = Form.Orm.beginTransaction state

Form.Orm.selectWhere<User> state transaction ( """"id"=:1""", [| "3" |] ) 
|> Result.map ( Seq.head >> fun x -> { x with first = "Michelle" }  )
|> Result.bind ( Form.Orm.update<User> state transaction ) 
|> printfn "%A"

Form.Orm.commitTransaction transaction
|> printfn "%A"

Form.Orm.selectWhere<User> state None ( """"id"=:1""", [| "3" |] ) 
|> printfn "%A"
```

It's also important to know that the delete, insert, and update functions establish their own transactions that are automatically committed once they're done executing when passing None as the DbTransaction Option's state. If passing in a Some DbTransaction, then it wont auto-commit the transaction and it need to be explicitly committed by calling `Form.Orm.commitTransaction` function.

With this info, you can get started using FORM. There's just one more thing to cover around batch-style commands [here](./advanced.md) if you'd like to read it.