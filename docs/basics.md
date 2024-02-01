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

Form.Orm.insertMany state None false myUsers
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