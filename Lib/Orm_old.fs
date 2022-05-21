namespace HCRD
module Orm =
    open System
    open System.Data
    open System.Reflection
    open FSharp.Reflection
    open Npgsql
    open NpgsqlTypes
    open Volight.Ulid
    
    ///<description>An attribute type which specifies a schema name</description>
    [<AttributeUsage(AttributeTargets.Class)>]
    type PrimarySchemaAttribute( alias: string ) = 
        inherit Attribute()
        member _.Value = alias

    ///<description>An attribute type which specifies a table name</description>
    [<AttributeUsage(AttributeTargets.Class)>]
    type PrimaryTableAttribute( alias: string ) = 
        inherit Attribute()
        member _.Value = alias

    ///<description>An attribute type which specifies a column name</description>
    [<AttributeUsage(AttributeTargets.Property)>]
    type PrimaryColumnAttribute( alias: string ) = 
        inherit Attribute()
        member _.Value = alias

    // [<AttributeUsage(AttributeTargets.Class)>]
    // type SecondarySchemaAttribute( alias: string ) = 
    //     inherit Attribute()
    //     member _.Value = alias
    // [<AttributeUsage(AttributeTargets.Class)>]
    // type SecondaryTableAttribute( alias: string ) = 
    //     inherit Attribute()
    //     member _.Value = alias
    // [<AttributeUsage(AttributeTargets.Property)>]
    // type SecondaryColumnAttribute( alias: string ) = 
    //     inherit Attribute()
    //     member _.Value = alias

    ///<description>An interface which handles the *preparation? and *loading? of data into a DB?
    /// Need confirmation on this description.</description>
    type IDataTransferObject = 
        interface 
        end

    ///<description>A restricted type which handles the retrieval of connection strings</description>
    /// <returns>A prepared connection string as a string (via members Primary or Secondary</returns>
    [<AbstractClass; Sealed>]
    type Connections =
        static member Primary =
            Config.getEnv "primary_connection_string"

        static member Secondary =
            Config.getEnv "Secondary"

    ///<description>A union type specifying a RDBM context (i.e. the flavor of SQL being used)</description>
    type DbContext = 
        | PSQL
        | MSSQL
        | MySQL
        | SQLite
        
    ///<description>A record type which holds the information required to map across BE and DB. </description>
    type SqlMapping = 
        { 
            Index : int
            SqlName : string 
            QuotedSqlName : string
            FSharpName : string
            Type : Type
            PropertyInfo: PropertyInfo
        }

    ///<description>A helper function which wraps the given strings in the context-appropriate quote characters</description>
    ///<returns>A quoted version of the input string</returns>
    let inline sqlQoute context str =
        match context with 
        | PSQL | SQLite -> $"\"{str}\""
        | MSSQL -> $"[{str}]"
        | MySQL -> $"`{str}`"

    ///<description>A helper function which joins a sequence of strings using ", "</description>
    ///<returns>A comma-space deliniated string</returns>
    let commaSeparate = 
        fun x -> String.concat ", " x
    
    ///<description>A helper function which joins a sequence of strings using char, with a whitespace 
    /// following each char if withSpace = True</description>
    ///<returns>A character deliniated string</returns>
    let characterSeparate char withSpace = 
        fun x -> String.concat (char + (if withSpace then " " else "")) x 

    ///<description>A stateless type containing functions mapping internal types to DB object specifications</description>
    ///<remarks>I'm not entirely sure what the complete scope of this type is. Is it just to bound all of 
    /// the mapping functions related to internal types?</remarks>
    type OrmReflectedType< ^T > = 
        ///<description>A function for converting an internal type which corresponds to a DB table into an array of SqlMapping</description>
        ///<returns>An array of column specifications (in SqlMapping instances)</returns>
        static member inline ColumnMapping context : SqlMapping array = 
            FSharpType.GetRecordFields typedefof< ^T > 
            |> Array.mapi ( fun i x -> 
                let sqlName =
                    match context with 
                    | _ -> 
                        let attr = 
                            x.GetCustomAttribute(typeof< PrimaryColumnAttribute >, false) 
                        if isNull attr then
                            x.Name
                        else 
                           ( attr :?> PrimaryColumnAttribute ).Value  
                    // | SecondaryContext -> 
                    //     let attr = 
                    //         x.GetCustomAttribute(typeof< SecondaryColumnAttribute >, false) 
                    //     if isNull attr then
                    //         x.Name
                    //     else 
                    //        ( attr :?> SecondaryColumnAttribute ).Value
                
                let fsharpName = x.Name
                let qoutedName = sqlQoute context sqlName
                { Index = i; SqlName = sqlName; QuotedSqlName = qoutedName; FSharpName = fsharpName; Type = x.PropertyType; PropertyInfo = x }
            )

        ///<description>A function which returns a table name assumption based on the presence of a TableAttribute</description>
        static member inline TableName context = 
            match context with 
            | _ -> 
                let attr = typedefof< ^T >.GetCustomAttribute(typeof< PrimaryTableAttribute >, false) 
                if isNull attr then
                    typedefof< ^T >.Name
                else 
                    ( attr :?> PrimaryTableAttribute ).Value
            // | SecondaryContext -> 
            //     let attr = typedefof< ^T >.GetCustomAttribute(typeof< SecondaryTableAttribute >, false) 
            //     if isNull attr then
            //         typedefof< ^T >.Name
            //     else 
            //         ( attr :?> SecondaryTableAttribute ).Value

    ///<description>I assume this function is used to make an option in place?</description>
    let inline toOptionDynamic (typ: Type) (value: obj) =
            let opttyp = typedefof<Option<_>>.MakeGenericType([|typ|])
            let tag, varr = if DBNull.Value.Equals(value) then 0, [||] else 1, [|value|]
            let case = FSharpType.GetUnionCases(opttyp) |> Seq.find (fun uc -> uc.Tag = tag)
            FSharpValue.MakeUnion(case, varr)

    ///<description>Take a type and returns an option of that type</description>
    let inline optionTypeArg (typ : Type) =
        let isOp = typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<Option<_>>
        if isOp then Some (typ.GetGenericArguments()[0]) else None

    ///<description>Evan will fill out the details here</description>
    let inline generateReader< ^T > context ( reader : IDataReader ) = 
        let rty = typeof< ^T>
        let makeEntity vals = FSharpValue.MakeRecord(rty, vals) :?>  ^T
        let fields = 
            seq { for field in ( OrmReflectedType< ^T >.ColumnMapping context ) -> field.SqlName, field } 
            |> dict 
        seq { while reader.Read() do
              yield seq { 0..reader.FieldCount-1 }
                    |> Seq.map (fun i -> reader.GetName(i), reader.GetValue(i) )
                    |> Seq.sortBy (fun (n, _) ->  fields[n].Index )
                    |> Seq.map (fun (n, v) -> 
                        match optionTypeArg fields[n].Type with
                        | Some t -> toOptionDynamic t v
                        | None   -> v
                    )
                    |> Seq.toArray
                    |> makeEntity } 
        |> Seq.toArray
    
    ///<description>Contains the SQL and NpgsqlDbType relating to an internal type</description>
    type DbTypeMapper = 
        {
            Sql: string 
            DbType: NpgsqlDbType
        }

    ///<description>A function which takes a type and returns an instance of DbTypeMapper corresponding to that type</description>
    let typeSwitch inType = 
        match inType with 
        | t when t = typeof<sbyte> -> { Sql = "int not null"; DbType = NpgsqlDbType.Integer}
        | t when t = typeof<sbyte option> -> { Sql = "int null"; DbType = NpgsqlDbType.Integer}
        | t when t = typeof<byte> -> { Sql = "int not null"; DbType = NpgsqlDbType.Integer}
        | t when t = typeof<byte option> -> { Sql = "int null"; DbType = NpgsqlDbType.Integer}
        | t when t = typeof<int> -> { Sql = "int not null"; DbType = NpgsqlDbType.Integer}
        | t when t = typeof<int option> -> { Sql = "int null"; DbType = NpgsqlDbType.Integer}
        | t when t = typeof<int64> -> { Sql = "bigint not null"; DbType = NpgsqlDbType.Bigint}
        | t when t = typeof<int64 option> -> { Sql = "bigint null" ; DbType = NpgsqlDbType.Bigint}
        | t when t = typeof<float> -> { Sql = "numeric(24, 7) not null"; DbType = NpgsqlDbType.Numeric}
        | t when t = typeof<float option> -> { Sql = "numeric(24, 7) null"; DbType = NpgsqlDbType.Numeric}
        | t when t = typeof<double> -> { Sql = "numeric(53, 15) not null"; DbType = NpgsqlDbType.Numeric}
        | t when t = typeof<double option> -> { Sql = "numeric(53, 15) null"; DbType = NpgsqlDbType.Numeric}
        | t when t = typeof<DateTime> -> { Sql = "timestamp without time zone not null"; DbType = NpgsqlDbType.Timestamp}
        | t when t = typeof<DateTime option> -> { Sql = "timestamp without time zone null"; DbType = NpgsqlDbType.Timestamp}
        | t when t = typeof<bool> -> { Sql = "bit not null"; DbType = NpgsqlDbType.Boolean}
        | t when t = typeof<bool option> -> { Sql = "bit null"; DbType = NpgsqlDbType.Boolean}
        | t when t = typeof<Ulid> -> { Sql = "varchar(26) not null"; DbType = NpgsqlDbType.Varchar}
        | t when t = typeof<Ulid option> -> { Sql = "varchar(26) null"; DbType = NpgsqlDbType.Varchar}
        | t when t = typeof<TimeZoneInfo> -> { Sql = "varchar(9) not null"; DbType = NpgsqlDbType.Varchar}
        | t when t = typeof<TimeZoneInfo option> -> { Sql = "varchar(9) null"; DbType = NpgsqlDbType.Varchar}
        | t when t = typeof< _ > -> { Sql = "varchar(10485760) not null"; DbType = NpgsqlDbType.Varchar}
        | t when t = typeof< _ option > -> { Sql = "varchar(10485760) null"; DbType = NpgsqlDbType.Varchar}
        | _ -> { Sql = "varchar(10485760) null"; DbType = NpgsqlDbType.Varchar}

    let inline basicErrorHandler f =
        try 
            Ok f
        with 
        | exn -> printfn "Exception found!\n"; Error exn

    //DB connection manager
    type Primary< ^T when ^T :> IDataTransferObject > = 
        | Type

        static member inline Connection = 
            new NpgsqlConnection( Connections.Primary ) 

        member inline _.Table = 
            OrmReflectedType< ^T >.TableName PSQL
        
        member inline _.Mapping = 
            OrmReflectedType< ^T >.ColumnMapping PSQL

        member inline this.Columns = 
            this.Mapping
            |> Array.map ( fun x -> x.SqlName )
            
        member inline this.Fields = 
            this.Mapping
            |> Array.map ( fun x -> x.FSharpName )
        
        member inline _.Execute sql =
            use connection = Primary< ^T >.Connection
            connection.Open()
            use cmd = new NpgsqlCommand( sql, connection )
            cmd.ExecuteNonQuery()
        
        member inline this.SelectQuery = 
            let cols = this.Mapping |> Array.map ( fun x -> x.QuotedSqlName )
            let namedCols = commaSeparate cols
            let table = this.Table
            $"select {namedCols} from {table}"
        
        member inline this.SearchQuery col thing =
            this.SelectQuery + $" where \"{col}\"  = '{thing}'"

        member inline this.CreateTable = 
            let cols = 
                this.Mapping
                |> Array.map ( fun x -> 
                    let name = 
                        x.QuotedSqlName
                    let dataTypeAndModifiers = 
                        typeSwitch x.Type
                        
                    $"{name} {dataTypeAndModifiers}"
                ) // ["col1 bigint not null", "col2"]
                |> String.concat ", \n\t"
            let table = this.Table
            $"create table {table}({cols});"

        member inline this.Select = 
            use connection = Primary< ^T >.Connection
            connection.Open()
            use cmd = new NpgsqlCommand( this.SelectQuery, connection )
            use reader = cmd.ExecuteReader(CommandBehavior.CloseConnection)

            basicErrorHandler ( generateReader< ^T > PSQL ( reader ) )
        
        member inline this.Search col thing = 
            use connection = Primary< ^T >.Connection
            connection.Open()
            use cmd = new NpgsqlCommand( (this.SearchQuery col thing), connection )
            printfn "%A" cmd.CommandText
            printfn "%A" cmd
            use reader = cmd.ExecuteReader(CommandBehavior.CloseConnection)

            basicErrorHandler ( generateReader< ^T > PSQL ( reader ) )

        member inline this.InsertQuery = 
            let cols = this.Mapping |> Array.map ( fun x -> x.QuotedSqlName )
            let namedCols = commaSeparate cols
            let table = this.Table
            $"insert into {table} ( {namedCols} )"

        member inline this.Insert instance () = 
            printfn "Entering insertion function...\n"
            use connection = Primary< ^T >.Connection
            printfn "Opening connection...\n"
            connection.Open()
            let query = 
                this.Mapping
                |> Array.map  ( fun x -> 
                    let value = 
                        try 
                            x.PropertyInfo.GetValue instance 
                            |> fun y -> y.ToString()
                        with 
                        | _ -> "null"

                    if x.Type = typeof<string> 
                    then $"'{value}'"
                    else value
                )
                |> String.concat ","
                |> fun x -> 
                    this.InsertQuery + $" values ({x})"
                    |> fun y -> y.Replace("delete", "")
                    |> fun y -> y.Replace("drop", "")
                    |> fun y -> y.Replace("table", "")
                    |> fun y -> y.Replace("update", "")
                    |> fun y -> y.Replace("truncate", "")
            printfn "Running query...\n"
            use cmd = new NpgsqlCommand( query , connection )

            printfn "%A" cmd.CommandText
            printfn "%A" cmd
            basicErrorHandler ( cmd.ExecuteNonQuery() )

        // member inline this.InsertQuery = 
        //     let cols = this.Mapping |> Array.map ( fun x -> x.QuotedSqlName )
        //     let namedCols = commaSeparate cols
        //     let table = this.Table
        //     let placeHolders = 
        //         this.Mapping
        //         |> Seq.mapi ( fun i x -> $"@{i+1}" )
        //         |> commaSeparate
        //     $"insert into {table} ( {namedCols} ) values ( {placeHolders} )"

        // member inline this.Insert instance () = 
        //     use connection = Primary< ^T >.Connection
        //     connection.Open()
        //     use cmd = new NpgsqlCommand( this.InsertQuery, connection )

        //     this.Mapping
        //     |> Seq.iter ( fun x -> 
        //         let name = x.SqlName 
        //         let dbType = (typeSwitch x.Type).DbType
        //         if x.Type = typeof<Ulid>
        //         then
        //             let value =  
        //                 (x.PropertyInfo.GetValue instance) :?> Ulid 
        //                 |> fun x -> x.ToString()
        //             cmd.Parameters.AddWithValue( name, dbType, value ) 
        //         else cmd.Parameters.AddWithValue( name, dbType, x.PropertyInfo.GetValue instance ) 
        //         |> ignore 
        //     )

        //     printfn "%A" cmd.CommandText
        //     printfn "%A" cmd
        //     basicErrorHandler ( cmd.ExecuteNonQuery() )

    // TODO: Copy primary for a secondary connection
    // type Secondary< ^T > = 
    //     static member inline Execute sql =
    //         use connection = new MySqlConnection(Config.Get "Secondary")
    //         connection.Open()
    //         use cmd = new MySqlCommand( sql, connection )
    //         generateReader< ^T > SecondaryContext ( cmd.ExecuteReader() )

    //     static member inline Columns = 
    //         OrmReflectedType< ^T >.ColumnMapping SecondaryContext
    //         |> Array.map ( fun x -> x.SqlName )
            
    //     static member inline Table = 
    //         OrmReflectedType< ^T >.TableName SecondaryContext
module Query = 
    type ComparisonOperator = 
        | Like 
        | Equals
        | Greater
        | Less 
        | GreaterThan 
        | LessThan
        | Not

    type WhereCondition = 
        { None: unit option
        }

    type QueryType = 
        | Select of int option //Limit modifier
        | Insert 
        | Update 
        | Delete

    type Query = 
        { Type: QueryType
          Tables: System.Type list 
          Columns: Orm.SqlMapping list 
          Where: WhereCondition list option
        }