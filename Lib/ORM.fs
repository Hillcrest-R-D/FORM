namespace Form 

open System
open System.Data
open FSharp.Reflection
open Npgsql
open Microsoft.Data.SqlClient
open System.Data.SQLite
open MySqlConnector
open System.Data.Common
open Form.Attributes
    
module Orm = 
    open Utilities
    ///<Description>Stores the flavor And context used for a particular connection.</Description>
    
    let inline connect ( state : OrmState ) = Utilities.connect state

    let inline beginTransaction ( state : OrmState ) =
        match connect state with 
        | Ok connection ->
            try 
                Some ( connection.BeginTransaction() )
            with 
            | exn -> 
                log ( fun _ -> printfn "Exception when beginning transaction: %A" exn )
                None
        | Error e -> 
            log ( fun _ -> printfn "Error when beginning transaction: %A" e )
            None
    

 
    let commitTransaction = 
        Option.map ( fun ( transaction : DbTransaction ) -> transaction.Commit() )
    let rollbackTransaction = 
        Option.map ( fun ( transaction : DbTransaction ) -> transaction.Rollback() )

    let tryCommit (transaction : DbTransaction option) = // option<Transaction> -> Result<unit, exn>
            try 
                commitTransaction transaction |> Ok 
            with  
            | exn -> 
                rollbackTransaction transaction |> ignore
                exn |> Error

    let inline consumeReader<^T > ( state : OrmState ) ( reader : IDataReader ) = Utilities.consumeReader<^T> state reader

    let inline execute ( state : OrmState ) ( transaction : DbTransaction option ) sql =
        transaction 
        |> withTransaction 
            state
            ( fun transaction -> 
                use cmd = makeCommand state sql ( transaction.Connection )
                cmd.Transaction <- transaction  
                cmd.ExecuteNonQuery( )
            )
            ( fun connection -> 
                use cmd = makeCommand state sql connection  
                let result = cmd.ExecuteNonQuery( )
                connection.Close() 
                result
            )
    
    ///<Description>
    /// Takes a function of IDataReader -> Result< 't seq, exn> (see FORMs consumeReader function as example) to 
    /// transfer the results of executing the specified sql against the specified database given by state into an 
    /// arbitrary type 't, defined by you in the readerFunction.
    /// </Description>
    let inline generateReader state sql =
        match connect state with
        | Ok conn -> 
            try 
                use cmd = makeCommand state sql conn 
                cmd.ExecuteReader( CommandBehavior.CloseConnection )
                |> Ok
            with 
            | exn -> Error exn
        | Error e -> Error e

    let inline executeWithReader ( state : OrmState ) ( transaction : DbTransaction option ) sql ( readerFunction : IDataReader -> 't ) = //Result<'t, exn>
        transaction
        |> withTransaction 
            state
            ( fun transaction -> 
                seq {
                    use cmd = makeCommand state sql <| transaction.Connection
                    cmd.Transaction <- transaction
                    use reader = cmd.ExecuteReader( )
                    yield! readerFunction reader
                } 
            )
            ( fun connection -> 
                seq {
                    use cmd = makeCommand state sql connection 
                    use reader = cmd.ExecuteReader( CommandBehavior.CloseConnection )
                    yield! readerFunction reader
                    connection.Close()
                }
            )
            
    let inline selectLimit< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) lim = 
        selectHelper< ^T > state transaction ( fun x -> 
            match state with 
            | MSSQL _ -> $"select top {lim} {x}" 
            | _ -> $"select {x} limit {lim}" 
        ) 

    let inline selectWhere< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) where   = 
        selectHelper< ^T > state transaction ( fun x -> $"select {x} where {where}" ) 
        
    let inline selectAll< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) = 
        selectHelper< ^T > state transaction ( fun x -> $"select {x}" ) 
    
    
    let inline insert< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) insertKeys ( instance : ^T ) =
        let query = insertBase< ^T > state insertKeys 
        transaction 
        |> withTransaction 
            state 
            ( fun transaction ->
                use command = parameterizeCommand state query (transaction.Connection) instance //makeCommand query conn state
                log (fun _ -> 
                    printfn "Param count: %A" command.Parameters.Count
                    for i in [0..command.Parameters.Count-1] do 
                        printfn "Param %d - %A: %A" i command.Parameters[i].ParameterName command.Parameters[i].Value
                )  
                command.Transaction <- transaction
                command.ExecuteNonQuery ( ) 
            )
            ( fun connection ->
                let query = insertBase< ^T > state insertKeys 
                use command = parameterizeCommand state query connection instance //makeCommand query connection state
                log (fun _ -> 
                    printfn "Param count: %A" command.Parameters.Count
                    for i in [0..command.Parameters.Count-1] do 
                        printfn "Param %d - %A: %A" i command.Parameters[i].ParameterName command.Parameters[i].Value
                )   
                let result = command.ExecuteNonQuery ( )
                connection.Close( )
                result
            )
            
    let inline insertMany< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) insertKeys ( instances : ^T seq ) =
        let query = insertBase< ^T > state insertKeys 
        transaction
        |> withTransaction 
            state 
            ( fun transaction ->  
                parameterizeSeqAndExecuteCommand state query transaction instances //makeCommand query connection state
            )
            ( fun connection -> 
                use transaction = connection.BeginTransaction()
                parameterizeSeqAndExecuteCommand state query transaction instances //makeCommand query connection state
                |> fun x -> transaction.Commit();connection.Close(); x
            )

    
    let inline update< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( instance: ^T ) = 
        let table = table< ^T > state 
        let paramChar = getParamChar state
        
        ensureId< ^T > state 
        |> Result.bind (fun sqlMapping ->
            sqlMapping
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> sprintf "%s.%s = %s%s" table x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun idConditional -> updateHelper< ^T > state transaction ( sprintf " where %s" idConditional ) instance 
        )
        
    let inline updateMany< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( instances: ^T seq )  = 
        // Array.map ( fun instance -> update<^T> state transaction instance ) instances 
        let table = table<^T> state
        let paramChar = getParamChar state
        
        ensureId< ^T > state 
        |> Result.bind (fun sqlMapping ->
            sqlMapping
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> sprintf "%s.%s = %s%s" table x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun idConditional -> updateManyHelper< ^T > state transaction ( sprintf " where %s" idConditional ) instances 
        )

    let inline updateWhere< ^T > ( state : OrmState ) transaction ( where : string ) ( instance: ^T )  = 
        updateHelper< ^T > state transaction ( sprintf " where %s" where ) instance 
        
    let inline delete< ^T > state ( transaction : DbTransaction option )  instance = 
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            sqlMapping
            |> Array.filter ( fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> sprintf "%s.%s = %s%s" tableName x.QuotedSqlName paramChar x.FSharpName )
            |> String.concat " and "
            |> fun where -> deleteHelper< ^T > state transaction where instance  
        )

    let inline deleteMany< ^T > state ( transaction : DbTransaction option ) instances  =
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            sqlMapping
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> sprintf "%s.%s = %s%s" tableName x.QuotedSqlName paramChar x.FSharpName)
            |> String.concat " and " // id1 = @id1 AND id2 = @id2
            |> fun where -> deleteManyHelper< ^T > state transaction where instances 
        )        
        
    /// <Warning> Running this function is equivalent to DELETE 
    /// FROM table WHERE whereClause </Warning>
    let inline deleteWhere< ^T > state ( transaction : DbTransaction option ) whereClause = 
        let query =  (deleteBase< ^T > state) + whereClause
        transaction 
        |> withTransaction
            state
            ( fun transaction -> 
                use cmd = makeCommand state query ( transaction.Connection ) 
                cmd.Transaction <- transaction
                cmd.ExecuteNonQuery ( )
            )
            ( fun connection -> 
                use cmd = makeCommand state query connection 
                cmd.ExecuteNonQuery ( )
                |> fun res -> connection.Close(); res
            )