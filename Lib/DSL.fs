namespace Form

open Orm
open System.Reflection
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.LanguagePrimitives
open System
open Microsoft.FSharp.Reflection

module DSL = 
    

    type ConjunctionState = 
        | Open 
        | None 
        | Close
        
        // member inline this.Compile = 
        //     match this with 
        //     | First c -> c.Name + " Like '%" + c.Value.ToString( ) + "%'"
        //     | Or c -> " or " + c.Name + " Like '%" + c.Value.ToString( ) + "%'"
        //     | And c -> " And " + c.Name + " Like '%" + c.Value.ToString( ) + "%'"

    type Clause =
        | Select of string
        | From of string
        | Join of string
        | Where of string
        | GroupBy of string
        | Having of string
        | OrderBy of string
        | Take of string
        | Skip of string

        member this.Compile  = 
            match this with 
            | Select v ->  v
            | From v -> v
            | Join v -> v
            | Where v -> v
            | GroupBy v -> v
            | Having v -> v
            | OrderBy v -> v
            | Take v -> v
            | Skip v -> v

    type Predicate =
        | Equals of string
        | NotEquals of string
        | GreaterThan of string
        | GreaterThanOrEqualTo of string
        | LessThan of string
        | LessThanOrEqualTo of string
        | Is of string
        | Exists of string
        | Between of string
        | In of string
        | Like of string
        | ILike of string
        // | All of ( Predicate * string ) 
        // | Any of ( Predicate * string )
        // | Some_ of ( Predicate * string )

        member this.Value ( state : OrmState ) = 
            match this with 
            | Equals v -> "= " + v 
            | NotEquals v -> "<> " + v 
            | GreaterThan v -> "> " + v 
            | GreaterThanOrEqualTo v -> ">= " + v 
            | LessThan v -> "< " + v 
            | LessThanOrEqualTo v -> "<= " + v 
            | Is v -> "IS " + v 
            | Exists v -> "Exists " + v 
            | Between v -> "Between " + v 
            | In v -> "IN " + v 
            | Like v -> "Like " + v 
            | ILike v -> "ILike " + v 

    type Order =
        | Descending
        | Ascending


        
    type Conjunction =
        | First of ( string * Predicate )
        | Or of ( string * Predicate )
        | And of ( string * Predicate )
        | Parenthesize of Conjunction seq

        member this.Compile ( state : OrmState ) = 
            match this with 
            | First ( c, pred ) -> $"{sqlQuote c state} {pred.Value state}"
            | Or ( c, pred ) -> $" OR {sqlQuote c state} {pred.Value state}"
            | And ( c, pred ) -> $" And {sqlQuote c state} {pred.Value state}"
            | Parenthesize cons -> 
                cons
                |> Seq.map ( fun x -> x.Compile state ) 
                |> Seq.fold ( fun acc x -> acc + x ) "( "
                |> fun x -> x + " )"

    let compile ( conjunctions : Conjunction seq ) ( state : OrmState  ) = 
        conjunctions
        |> Seq.map ( fun x -> x.Compile state )
        |> String.concat " "

    let inline Select< ^T > ( state : OrmState ) = 
        Select ( "Select " + ( String.concat ", " ( columns< ^T > state ) ) )
    
    let inline From< ^T > ( state : OrmState ) = 
        From ( "From " + ( table< ^T > state ) )

    let inline Join< ^T > ( conjunctions : Conjunction seq ) ( state : OrmState ) = //Join "Payments.User" ON Col1 = Col2 And Col3 = 5
        Join ( "Join " + ( table< ^T > state ) + " ON " + compile conjunctions state )
    
    let inline Where ( conjunctions : Conjunction seq ) ( state : OrmState ) = 
        Where ( "Where " + compile conjunctions state )
    
    let inline GroupBy ( cols: string seq ) ( state : OrmState ) = 
        GroupBy ( "Group By " + ( String.concat ", " cols ) )

    let inline OrderBy ( cols: ( string * Order option ) seq ) ( state : OrmState ) =
        let ordering = 
            cols 
            |> Seq.map ( fun x -> 
                let direction = 
                    match snd x with 
                    | Some order -> 
                        match order with 
                        | Ascending -> " Asc"
                        | Descending -> " Desc"
                    | _ -> ""
                $"{fst x}{direction}"
            ) 
            |> String.concat ", "
        OrderBy ( "order by " + ordering )

    let inline skip ( num : int ) ( state : OrmState ) =
        Skip ( $"offset {num}" )

    let inline take ( num : int ) ( state : OrmState ) =
        Take ( $"limit {num}" )

    type ClauseState = 
        | TakesOrm of ( OrmState -> Clause )
        | Doesnt of Clause

    type Query = 
        { clauses: ( OrmState -> Clause ) list }
        member this.Compile ( state : OrmState ) =
            List.fold ( fun acc ( elem : ( OrmState -> Clause ) ) -> if acc = "" then ( elem state ).Compile  else acc + "\n" + ( elem state ).Compile ) "" this.clauses

    type UnionAll =
        { queries: Query list }
        member this.Compile ( state : OrmState ) =
            List.fold ( fun acc ( elem : Query ) -> if acc = "" then elem.Compile state else acc + "\n\nUnion All\n\n" + elem.Compile state ) "" this.queries



    type IDataTransferObject =
        interface
        end

module SqlGeneration = 
    
    
    let queries = []


    // queries.add <- "select xyz from table a"

    // queries.add <- "select xc from table a left join b on asdfasdf"

    // queries.compile <- "alter table add column"

    exception InvalidAlias of string

    type Select = Unit
    type Insert = Unit
    type Update = Unit
    type Delete = Unit
    
    type From = Unit
    type Where = Unit
    type GroupBy = Unit
    type OrderBy = Unit
    type Having = Unit
    type Limit = int
    type OffSet = int

    type KeyWord = 
        | Add                   // 	Adds a Column in an existing Table
        | AddConstraint         // 	Adds a Constraint after a Table is already Created
        | All                   // 	Returns true if All of the subquery Values meet the condition
        | Alter                 // 	Adds, Deletes, or modifies Columns in a Table, or changes the data type of a Column in a Table
        | AlterColumn           // 	Changes the data type of a Column in a Table
        | AlterTable            //  Adds, Deletes, or modifies Columns in a Table
        | And                   // 	Only includes Rows Where both conditions is true
        | Any                   // 	Returns true if Any of the subquery Values meet the condition
        | As                    // 	Renames a Column or Table with an aliAs
        | Asc                   // 	Sorts the result Set in Ascending Order
        | BackupDatabase        // 	Creates a back up of an existing Database
        | Between               // 	Selects Values within a given range
        | Case                  // 	Creates different outputs bAsed on conditions
        | Check                 // 	A Constraint that Limits the value that can be placed in a Column
        | Column                // 	Changes the data type of a Column or Deletes a Column in a Table
        | Constraint            // 	Adds or Deletes a Constraint
        | Create                // 	Creates a Database, Index, View, Table, or Procedure
        | CreateDatabase        // 	Creates a new SQL Database
        | CreateIndex           // 	Creates an Index on a Table ( Allows duplicate Values )
        | CreateOrReplaceView   // 	Updates a View
        | CreateTable           // 	Creates a new Table in the Database
        | CreateProcedure       // 	Creates a stored Procedure
        | CreateUniqueIndex     // 	Creates a Unique Index on a Table ( no duplicate Values )
        | CreateView            // 	Creates a View bAsed on the result Set of a Select statement
        | Database              //	Creates or Deletes an SQL Database
        | Default               // 	A Constraint that provides a Default value for a Column
        | Delete                // 	Deletes Rows From a Table
        | Desc                  // 	Sorts the result Set in Descending Order
        | Distinct              // 	Selects only Distinct ( different ) Values
        | Drop                  // 	Deletes a Column, Constraint, Database, Index, Table, or View
        | DropColumn            // 	Deletes a Column in a Table
        | DropConstraint        // 	Deletes a Unique, PrimaryKey, FOREIGN KEY, or Check Constraint
        | DropDatabase          // 	Deletes an existing SQL Database
        | DropDefault           // 	Deletes a Default Constraint
        | DropIndex             // 	Deletes an Index in a Table
        | DropTable             // 	Deletes an existing Table in the Database
        | DropView              // 	Deletes a View
        | Exec                  // 	Executes a stored Procedure
        | Exists                // 	Tests for the existence of Any record in a subquery
        | ForeignKey            // 	A Constraint that is a key used to link two Tables together
        | From                  // 	Specifies which Table to Select or Delete data From
        | FullOuterJoin         // 	Returns All Rows when there is a match in either Left Table or Right Table
        | GroupBy               // 	Groups the result Set ( used with aggregate functions: COUNT, MAX, MIN, SUM, AVG )
        | Having                // 	Used instead of Where with aggregate functions
        | In                    // 	Allows you to specify multiple Values in a Where clause
        | Index                 // 	Creates or Deletes an Index in a Table
        | InnerJoin             // 	Returns Rows that have matching Values in both Tables
        | InsertInto            // 	Inserts new Rows in a Table
        | InsertIntoSelect      // 	Copies data From one Table Into aNother Table
        | IsNull                //	Tests for empty Values
        | IsNotNull             // 	Tests for non-empty Values
        | Join                  // 	Joins Tables
        | LeftJoin              // 	Returns All Rows From the Left Table, And the matching Rows From the Right Table
        | Like                  // 	Searches for a specified pattern in a Column
        | Limit                 // 	Specifies the Nynber of records to return in the result Set
        | Not                   // 	Only includes Rows Where a condition is Not true
        | NotNull               // 	A Constraint that enforces a Column to Not accept Null Values
        | Or                    // 	Includes Rows Where either condition is true
        | OrderBy               // 	Sorts the result Set in Ascending or Descending Order
        | OuterJoin             // 	Returns All Rows when there is a match in either Left Table or Right Table
        | PrimaryKey            // 	A Constraint that Uniquely identifies each record in a Database Table
        | Procedure             // 	A stored Procedure
        | RightJoin             // 	Returns All Rows From the Right Table, And the matching Rows From the Left Table
        | RowNyn                // 	Specifies the Nynber of records to return in the result Set
        | Select                // 	Selects data From a Database
        | SelectDistinct        // 	Selects only Distinct ( different ) Values
        | SelectInto            // 	Copies data From one Table Into a new Table
        | SelectTop             // 	Specifies the Nynber of records to return in the result Set
        | Set                   // 	Specifies which Columns And Values that should be Updated in a Table
        | Table                 // 	Creates a Table, or adds, Deletes, or modifies Columns in a Table, or Deletes a Table or data inside a Table
        | Top                   // 	Specifies the Nynber of records to return in the result Set
        | TruncateTable         // 	Deletes the data inside a Table, but Not the Table itself
        | Union                 // 	Combines the result Set of two or more Select statements ( only Distinct Values )
        | UnionAll              // 	Combines the result Set of two or more Select statements ( Allows duplicate Values )
        | Unique                // 	A Constraint that ensures that All Values in a Column are Unique
        | Update                // 	Updates existing Rows in a Table
        | Values                // 	Specifies the Values of an Insert Into statement
        | View                  // 	Creates, Updates, or Deletes a View
        | Where                 // 	Filters a result Set to include only records that fulfill a specified condition

    type Expr = 
        | Create of string 
        | Exprs of Expr seq

    type DDL = 
        | Create
        | Drop 
        | Alter 
        | Truncate

    type DML =
        | Insert 
        | Update 
        | Delete 
        // | CAll 
        // | ExplainCAll
        // | Lock 

    // type TCL =
    //     | Commit 
    //     | SavePoint
    //     | RollBack
    //     | SetTransaction 
    //     | SetConstraint

    type DQL =
        | Select 

    type DCL =
        | Grant 
        | Revoke

    type Command = 
        | DDL of DDL
        | DML of DML 
        // | TCL of TCL
        // | DQL of DQL 
        | DCL of DCL

    type DataType = 
        | Int
        | BigInt
        | Float
        | Double
        | Bit of int option
        | BitVarying of int option
        | Boolean 
        | Character of int 
        | CharacterVarying of int 
        | Date 
        | Time 
        | DateTime
        | Numeric of int * int 

    type Column = 
        { Name : string 
          DataType : DataType 
        }

    // type Table = 
    //     { Name : string 
    //       Columns : Column array
    //     }

    //first string is F# name, second is aliAs
    type Alias = string * string
    
    /// Computes a value that may thRow an exception at instantiation... which should be at startup. 
    type Table ( ty : Type, TableAliAs : Alias option, ColumnAliases : Alias seq option ) = 
        let TableName = 
            match TableAliAs with 
            | Some name -> snd name 
            | None -> ty.Name

        /// ThRows exception at start up if aliAses are given but Not matched with source.
        let ColumnNames = 
            match ColumnAliases with
            | Some aliAses ->
                let comp = Set.ofSeq ( Seq.map fst aliAses )
                let mapper = Map.ofSeq aliAses
                let names = 
                    FSharpType.GetRecordFields ty
                    |> Array.map ( fun x -> x.Name )
                    |> Set.ofArray

                if Set.count comp <> Set.count ( Set.intersect comp names ) 
                then 
                    sprintf "Unable to match an aliAs provided %A with members %A on %s" comp names ty.Name |> InvalidAlias |> raise 
                else 
                    FSharpType.GetRecordFields ty
                    |> Array.map ( fun x -> x.Name )
                    |> Array.map ( fun x -> if Map.containsKey x mapper then Map.find x mapper else x )
                
            | None -> FSharpType.GetRecordFields ty |> Array.map ( fun x -> x.Name )

        member _.MakeTable ( ) =
            sprintf "Create Table %s "


