namespace Form 


module Orm = 
    open System
    open System.Data
    open FSharp.Reflection
    open Npgsql
    open System.Data.SQLite
    open MySqlConnector
    open System.Data.Common
    open Form.Attributes
    open Utilities
    open Logging
    
    ///<Description>Stores the flavor And context used for a particular connection.</Description>
    let inline connect ( state : OrmState ) = Utilities.connect state

    let inline beginTransaction ( state : OrmState ) =
        match connect state with 
        | Ok connection ->
            try 
                Some ( connection.BeginTransaction() )
            with 
            | exn -> 
                log ( sprintf "Exception when beginning transaction: %A" exn )
                None
        | Error e -> 
            log ( sprintf "Error when beginning transaction: %A" e )
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

    ///<description>WARNING! Execute takes a raw string literal to execute against the specified DB state, which is inherently unsafe and exposed to SQL injection, do not use this in a context where strings aren't being escaped properly before hand.</description>
    let inline execute ( state : OrmState ) ( transaction : DbTransaction option ) sql =
        transaction 
        |> withTransaction 
            state
            ( fun transaction -> 
                use cmd = makeCommand state sql ( transaction.Connection )
                cmd.Transaction <- transaction  
                cmd.ExecuteNonQuery( )
                |> Ok 
            )
            ( fun connection -> 
                use cmd = makeCommand state sql connection  
                let result = cmd.ExecuteNonQuery( )
                connection.Close() 
                result
            )
    
    ///<summary>
    /// Takes a function of IDataReader -> Result&lt; 't seq, exn&gt; (see FORMs consumeReader function as example) to 
    /// transfer the results of executing the specified sql against the specified database given by state into an 
    /// arbitrary type 't, defined by you in the readerFunction.
    /// </summary>
    let inline generateReader state sql =
        match connect state with
        | Ok conn -> 
            try 
                use cmd = makeCommand state (sql) conn 
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
                    use cmd = makeCommand state (sql) <| transaction.Connection
                    cmd.Transaction <- transaction
                    use reader = cmd.ExecuteReader( )
                    yield! readerFunction reader
                } 
                |> Ok
            )
            ( fun connection -> 
                seq {
                    use cmd = makeCommand state (sql) connection 
                    use reader = cmd.ExecuteReader( CommandBehavior.CloseConnection )
                    yield! readerFunction reader
                    connection.Close()
                }
            )
    
    ///<summary>Select <paramref name="limit"/> records <typeparamref name="^T"/> from the table <typeparamref name="^T"/> @ <paramref name="state"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="limit"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks>
    ///<example>
    ///     <code>selectlimit&lt;^T&gt; someState None 5</code>
    ///</example>
    /// </remarks>
    let inline selectLimit< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( limit : int ) = 
        selectHelper< ^T > state transaction ( fun x -> 
            match state with 
            | MSSQL _ -> $"select top {limit} {x}" 
            | MySQL _ 
            | PSQL _ 
            | SQLite _ 
            | ODBC _ -> $"select {x} limit {limit} " 
            // | ODBC _ -> $"select {x} order by 1 fetch first {limit} rows only" 
        ) 

    ///<summary>Select all records <typeparamref name="^T"/> from the table <typeparamref name="^T"/> @ <paramref name="state"/> using the conditional <paramref name="where"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="where"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks>
    /// </remarks>
    ///<example>
    ///     <code>selectWhere&lt;^T&gt; someState None where</code>
    ///</example>
    let inline selectWhere< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) (where) = 
        selectHelper< ^T > state transaction ( fun x -> $"select {x} where {escape where}" ) 
        
    ///<summary>Select all records from the table <typeparamref name="^T"/> @ <paramref name="state"/></summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="includeKeys"></param>
    ///<param name="instances"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks><para></para>
    ///<para></para></remarks>
    ///<example>
    ///     <code>selectAll&lt;^T&gt; someState None</code>
    ///</example>
    let inline selectAll< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) = 
        selectHelper< ^T > state transaction ( fun x -> $"select {x}" ) 
    
    ///<summary>Insert an <paramref name="instance"/> of <typeparamref name="^T"/> into the table <typeparamref name="^T"/> @ <paramref name="state"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="includeKeys"></param>
    ///<param name="instance"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks><para>Using <paramref name="includeKeys"/> = true will likely be the default behavior desired in most instances - it should be set to false only in circumstances where you have default behavior on the table generating keys for you.</para>
    ///<para></para></remarks>
    ///<example>
    ///     <code>insert&lt;^T&gt; someState None true anInstanceOfT</code>
    ///</example>
    let inline insert< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) includeKeys ( instance : ^T ) =
        let query = insertBase< ^T > state includeKeys 
        log $"Insert Query Generated: {query}"
        transaction 
        |> withTransaction 
            state 
            ( fun transaction ->
                use command = parameterizeCommand state query transaction includeKeys Insert instance //makeCommand query conn state
                log ( 
                    sprintf "Param count: %A" command.Parameters.Count :: 
                    [ for i in [0..command.Parameters.Count-1] do 
                        yield sprintf "Param %d - %A: %A" i command.Parameters[i].ParameterName command.Parameters[i].Value 
                    ]
                    |> String.concat "\n"
                )  
                command.Transaction <- transaction
                command.ExecuteNonQuery ( ) 
                |> Ok
            )
            ( fun connection ->
                let query = insertBase< ^T > state includeKeys 
                use transaction = connection.BeginTransaction()
                use command = parameterizeCommand state query transaction includeKeys Insert instance //makeCommand query connection state
                log (
                    sprintf "Param count: %A" command.Parameters.Count ::
                    [ for i in [0..command.Parameters.Count-1] do 
                        yield sprintf "Param %d - %A: %A" i command.Parameters[i].ParameterName command.Parameters[i].Value
                    ] |> String.concat "\n"
                )   
                command.ExecuteNonQuery ( )
                |> fun x -> transaction.Commit(); connection.Close(); x
            )
    
    ///<summary>Insert a seq&lt;<typeparamref name="^T"/>&gt; <paramref name="instances"/> into the table <typeparamref name="^T"/> @ <paramref name="state"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="includeKeys"></param>
    ///<param name="instances"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks><para>Using <paramref name="includeKeys"/> = true will likely be the default behavior desired in most instances - it should be set to false only in circumstances where you have default behavior on the table generating keys for you.</para>
    ///<para></para></remarks>
    ///<example>
    ///     <code>insertMany&lt;^T&gt; someState None true instancesOfT</code>
    ///</example>
    let inline insertMany< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) includeKeys ( instances : ^T seq ) =
        let query = insertBase< ^T > state includeKeys 
        transaction
        |> withTransaction 
            state 
            ( fun transaction -> 
                seq {
                    yield parameterizeSeqAndExecuteCommand state query transaction includeKeys Insert instances //makeCommand query connection state
                } |> Ok 
            )
            ( fun connection -> 
                seq {
                    use transaction = connection.BeginTransaction()
                    yield parameterizeSeqAndExecuteCommand state query transaction includeKeys Insert instances //makeCommand query connection state
                    transaction.Commit()
                    connection.Close()
                }
            )

    ///<summary>Update a record <paramref name="instance"/> of <typeparamref name="^T"/> in the table <typeparamref name="^T"/> @ <paramref name="state"/> using the keys/identity attribute(s) of <typeparamref name="^T"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="instance"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks><para>There must be atleast one <see cref="PrimaryKeyAttribute">PrimaryKeyAttribute</see> or <see cref="IdAttribute">IdAttribute</see> on <typeparamref name="^T"/> for an update call to succeed.</para>
    ///<para></para></remarks>
    ///<example>
    ///     <code>update&lt;^T&gt; someState None instanceOfT</code>
    ///</example>
    let inline update< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( instance: ^T ) = 
        let table = table< ^T > state 
        let paramChar = getParamChar state
        
        ensureId< ^T > state 
        |> Result.bind (fun sqlMapping ->
            sqlMapping
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName< ^T > state  ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> 
                match state with 
                | ODBC _ -> sprintf "%s.%s = %s" table x.QuotedSqlName paramChar 
                | _ -> sprintf "%s.%s = %s%s" table x.QuotedSqlName paramChar x.FSharpName 
            )
            |> String.concat " and "
            |> fun idConditional -> updateHelper< ^T > state transaction ( sprintf " where %s" idConditional ) instance 
        )
    
    ///<summary>Update a seq&lt;<typeparamref name="^T"/>&gt; of <paramref name="instances"/> in the table <typeparamref name="^T"/> @ <paramref name="state"/> using the keys/identity attribute(s) of <typeparamref name="^T"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="includeKeys"></param>
    ///<param name="instances"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks><para>There must be atleast one <see cref="PrimaryKeyAttribute">PrimaryKeyAttribute</see> or <see cref="IdAttribute">IdAttribute</see> on <typeparamref name="^T"/> for an update call to succeed.</para>
    ///<para></para></remarks>
    ///<example>
    ///     <code>updateMany&lt;^T&gt; someState None instancesOfT</code>
    ///</example>
    let inline updateMany< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( instances: ^T seq )  = 
        let tableName = table<^T> state
        let paramChar = getParamChar state
        
        ensureId< ^T > state 
        |> Result.bind (fun sqlMapping ->
            sqlMapping
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName  ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> 
                match state with 
                | ODBC _ -> sprintf "%s.%s = %s" tableName x.QuotedSqlName paramChar 
                | _ -> sprintf "%s.%s = %s%s" tableName x.QuotedSqlName paramChar x.FSharpName 
            ) 
            |> String.concat " and "
            |> fun idConditional -> updateManyHelper< ^T > state transaction ( sprintf " where %s" idConditional ) instances 
        )

    ///<summary>Update an <paramref name="instance"/> of <typeparamref name="^T"/> in the table <typeparamref name="^T"/> @ <paramref name="state"/> using the conditional <paramref name="where"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="where"></param>
    ///<param name="instance"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks>
    ///   <para>While <see cref="update">update</see> and <see cref="updateMany">updateMany</see> require key/id attributes on the record types of interest, this function uses conditionals to perform the update. Be careful to fully qualify your where clause to avoid undesired data mutation!</para>
    ///   <para>While the <paramref name="where"/> clause is handled to avoid the possibility of sql injection, it is always a good idea to escape any user input you are passing into your conditions.</para>
    /// </remarks>
    ///<example>
    ///     <code>updateWhere&lt;^T&gt; someState None where instancesOfT</code>
    ///</example>
    let inline updateWhere< ^T > ( state : OrmState ) transaction ( where ) ( instance: ^T )  = 
        updateHelper< ^T > state transaction ( sprintf " where %s" (escape where) ) instance 
        
    ///<summary>Delete an <paramref name="instance"/> of <typeparamref name="^T"/> in the table <typeparamref name="^T"/> @ <paramref name="state"/> using the key/id attributes on <typeparamref name="^T"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="instance"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks>
    ///   <para>Just like with delete statements in plain SQL, be careful when using this - it deletes stuff!</para>
    ///   <para>There must be atleast one <see cref="PrimaryKeyAttribute">PrimaryKeyAttribute</see> or <see cref="IdAttribute">IdAttribute</see> on <typeparamref name="^T"/> for a delete call to succeed.</para>
    /// </remarks>
    ///<example>
    ///     <code>delete&lt;^T&gt; someState None instanceOfT</code>
    ///</example>
    let inline delete< ^T > state ( transaction : DbTransaction option )  instance = 
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            sqlMapping
            |> Array.filter ( fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> 
                match state with 
                | ODBC _ -> sprintf "%s.%s = %s" tableName x.QuotedSqlName paramChar 
                | _ -> sprintf "%s.%s = %s%s" tableName x.QuotedSqlName paramChar x.FSharpName 
            ) 
            |> String.concat " and "
            |> fun where -> deleteHelper< ^T > state transaction where instance  
        )

    ///<summary>Delete a seq&lt;<typeparamref name="^T"/>&gt; of <paramref name="instances"/> in the table <typeparamref name="^T"/> @ <paramref name="state"/> using the keys/identity attribute(s) of <typeparamref name="^T"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="includeKeys"></param>
    ///<param name="instances"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks><para>There must be atleast one <see cref="PrimaryKeyAttribute">PrimaryKeyAttribute</see> or <see cref="IdAttribute">IdAttribute</see> on <typeparamref name="^T"/> for a delete call to succeed.</para>
    ///<para></para></remarks>
    ///<example>
    ///     <code>deleteMany&lt;^T&gt; someState None instancesOfT</code>
    ///</example>
    let inline deleteMany< ^T > state ( transaction : DbTransaction option ) instances  =
        ensureId< ^T > state 
        |> Result.bind ( fun sqlMapping -> 
            let tableName = table< ^T > state 
            let paramChar = getParamChar state
            sqlMapping
            |> Array.filter (fun mappedInstance -> mappedInstance.QuotedSource = tableName ) //! Filter out joins for non-select queries
            |> Array.map ( fun x -> 
                match state with 
                | ODBC _ -> sprintf "%s.%s = %s" tableName x.QuotedSqlName paramChar 
                | _ -> sprintf "%s.%s = %s%s" tableName x.QuotedSqlName paramChar x.FSharpName 
            ) 
            |> String.concat " and " // id1 = @id1 AND id2 = @id2
            |> fun where -> deleteManyHelper< ^T > state transaction where instances 
        )        
        
    ///<summary>Delete an <paramref name="instance"/> of <typeparamref name="^T"/> in the table <typeparamref name="^T"/> @ <paramref name="state"/> using the conditional <paramref name="where"/>.</summary>
    ///<param name="state"></param>
    ///<param name="transaction"></param>
    ///<param name="where"></param>
    ///<typeparam name="^T">The record type representation of the table being acted on.</typeparam>
    ///<remarks>
    ///   <para>While <see cref="delete">delete</see> and <see cref="deleteMany">deleteMany</see> require key/id attributes on the record types of interest, this function uses conditionals to perform the delete. Be careful to fully qualify your where clause to avoid undesired data loss!</para>
    ///   <para>While the <paramref name="where"/> clause is handled to avoid the possibility of sql injection, it is always a good idea to escape any user input you are passing into your conditions.</para>
    ///   <para>Other opWhere functions in <see cref="Form">FORM</see> also take an instance of the desired type (i.e. <see cref="updateWhere">updateWhere</see>), the difference comes from the fact that we don't need any reference data here, where as in the update we need to know what we are updating stuff to.</para>
    /// </remarks>
    ///<example>
    ///     <code>deleteWhere&lt;^T&gt; someState None where</code>
    ///</example>
    let inline deleteWhere< ^T > ( state : OrmState ) ( transaction : DbTransaction option ) ( where : (string * obj seq) ) = 
        let query = $"{deleteBase< ^T > state} {escape where}"
        transaction 
        |> withTransaction
            state
            ( fun transaction -> 
                use cmd = makeCommand state query ( transaction.Connection ) 
                cmd.Transaction <- transaction
                cmd.ExecuteNonQuery ( )
                |> Ok
            )
            ( fun connection -> 
                use cmd = makeCommand state query connection 
                cmd.ExecuteNonQuery ( )
                |> fun res -> connection.Close(); res
            )