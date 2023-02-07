using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using System.Data.SqlClient;
using System.Security.Cryptography;
using Dapper;
using static LanguageExt.Prelude;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Steinpilz.LocalDb
{
    public class DbWrapper
    {
        readonly DbParams @params;
        private readonly ILogger logger;

        string MasterConnectionString { get; }
        public string ConnectionString { get; private set; }

        string hash;
        string dbName;

        public DbWrapper(DbParams @params) : this(@params, NullLogger.Instance) { }

        public DbWrapper(DbParams @params, ILogger logger)
        {
            this.@params = @params;
            this.logger = logger;
            MasterConnectionString = (string)@params.ConnectionString.ForDatabase("master");

            hash = HashSum((string)@params.DatabaseSchema.SqlScript);
            dbName = @params.UseSchemaHashSuffix ? $"{@params.DatabaseName}-{hash}" : @params.DatabaseName;
            ConnectionString = (string)@params.ConnectionString.ForDatabase(dbName);
        }

        public void DeploySchema()
        {
            var script = this.@params.DatabaseSchema.SqlScript.WithDatabaseName(dbName);
            
            if (AlreadyDeployed(hash))
                return;

            var commands = script.ExtractValuableCommands();

            RunScripts(commands.Select(x => (string)x));

            SetDeployedMark(hash);
        }

        public void ClearTables(IEnumerable<string> tables)
        {
            RunScript(ClearTablesScript(tables));
        }

        protected virtual void SetDeployedMark(string hash)
        {
            for (var tr = 0; tr < 10; tr++)
            {
                try
                {
                    // wait for connection
                    const int tries = 10;

                    for (var i = 0; ; i++)
                    {
                        try
                        {
                            using (var conn = new SqlConnection(ConnectionString))
                            {
                                conn.Execute($"CREATE TABLE {MarkTableName(hash)} (Id INT)");
                                break;
                            }
                        }
                        catch (SqlException ex)
                        {
                            if (i >= tries)
                                throw;
                            Thread.Sleep(TimeSpan.FromMilliseconds(300));
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    this.logger.LogCritical(new EventId(), ex, "Error by setting deployed mark");
                }
            }
        }


        private void RunScripts(IEnumerable<string> commands)
        {
            using (var conn = new SqlConnection(MasterConnectionString))
            {
                conn.Open();
                foreach (var command in commands)
                {
                    try
                    {
                        conn.Execute(command);
                    }
                    catch (Exception ex)
                    {
                        //throw;
                        this.logger.LogWarning(new EventId(), ex, $"Error by running script: [{command}]");
                    }
                }
            }
        }

        protected string ClearTablesScript(IEnumerable<string> tables)
        {
            var sb = new StringBuilder();
            foreach (var table in tables)
            {
                sb.AppendLine($"ALTER TABLE {table} NOCHECK CONSTRAINT ALL ");
            }

            foreach (var table in tables)
            {
                sb.AppendLine($"DELETE {table}");
            }

            foreach (var table in tables)
            {
                sb.AppendLine($"ALTER TABLE {table} CHECK CONSTRAINT ALL ");
            }

            return sb.ToString();
        }

        protected void RunScript(string script)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                //conn.Open();
                conn.Execute(script);
            }
        }

        private bool AlreadyDeployed(string hash)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Execute($"SELECT * FROM {MarkTableName(hash)}");
                    return true;
                }
            }
            catch (SqlException ex)
            {
                return false;
            }
        }

        private string MarkTableName(string hash)
        {
            return $"dbo.SchemaHash_{hash}";
        }

        private string HashSum(string script)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(script));
                var sb = new StringBuilder();
                foreach (byte t in hash)
                {
                    sb.Append(t.ToString("x2"));
                }
                return sb.ToString();
            }
        }

    }

    [Equals]
    public class DbParams
    {
        public ConnectionString ConnectionString { get; }
        public DbSchema DatabaseSchema { get; }
        public string DatabaseName { get; }
        public bool UseSchemaHashSuffix { get; }

        public DbParams(
            ConnectionString connectionString,
            DbSchema databaseSchema,
            string databaseName,
            bool useSchemaHashSuffix
            )
        {
            ConnectionString = connectionString;
            DatabaseSchema = databaseSchema;
            DatabaseName = databaseName;
            UseSchemaHashSuffix = useSchemaHashSuffix;
        }

        public DbParams WithConnectionString(ConnectionString connString) =>
            new DbParams(connString, DatabaseSchema, DatabaseName, UseSchemaHashSuffix);

        public DbParams WithDatabaseSchema(DbSchema dbSchema) =>
            new DbParams(ConnectionString, dbSchema, DatabaseName, UseSchemaHashSuffix);

        public DbParams WithDatabaseName(string databaseName) =>
            new DbParams(ConnectionString, DatabaseSchema, databaseName, UseSchemaHashSuffix);

        public DbParams WithUseSchemaHashSuffix(bool useSchemaHashSuffix) =>
            new DbParams(ConnectionString, DatabaseSchema, DatabaseName, useSchemaHashSuffix);

        public static bool operator ==(DbParams left, DbParams right) => Operator.Weave(left, right);
        public static bool operator !=(DbParams left, DbParams right) => Operator.Weave(left, right);
    }

    public class ConnectionString : NewType<ConnectionString, string>
    {
        private ConnectionString(string value) : base(value)
        {
        }

        public static ConnectionString Custom(string value)
            => new ConnectionString(value);

        public static ConnectionString LocalDb(LocalDbVersion version)
            => new ConnectionString($@"Data Source=(LocalDb)\{(string)version};Integrated Security=SSPI;");

        public ConnectionString ForDatabase(string dbName)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(Value)
            {
                InitialCatalog = dbName
            };

            if(!string.IsNullOrEmpty(connectionStringBuilder.AttachDBFilename))
            {
                if (dbName == "master")
                    connectionStringBuilder.Remove("AttachDBFilename");
                else
                {
                    connectionStringBuilder.AttachDBFilename = connectionStringBuilder.AttachDBFilename
                        .Replace("{database}", dbName);
                    connectionStringBuilder.Remove("Initial Catalog");
                }
            }

            return new ConnectionString(connectionStringBuilder.ConnectionString);
        }
    }

    public class LocalDbVersion : NewType<LocalDbVersion, string>
    {
        public static LocalDbVersion V11 = new LocalDbVersion("v11.0");
        public static LocalDbVersion V12 = new LocalDbVersion("v12.0");

        public LocalDbVersion(string value) : base(value)
        {
        }
    }

    [Equals]
    public class DbSchema
    {
        public SqlSchemaScript SqlScript { get; private set; }

        private DbSchema() { }

        public static DbSchema FromSqlScript(SqlSchemaScript sql)
            => new DbSchema { SqlScript = sql };

        public static bool operator ==(DbSchema left, DbSchema right) => Operator.Weave(left, right);
        public static bool operator !=(DbSchema left, DbSchema right) => Operator.Weave(left, right);
    }

    public class SqlSchemaScript : NewType<SqlSchemaScript, string>
    {
        static readonly Regex CreateTableRegex = new Regex(@"CREATE\s+TABLE\s+([^\s]+)", RegexOptions.Singleline);
        private SqlSchemaScript(string value) : base(value)
        {
        }

        public static SqlSchemaScript Create(string sqlScript)
        {
            return new SqlSchemaScript(RemoveSetVars(sqlScript).Replace("$(__IsSqlCmdEnabled)", "True"));
        }

        public Lst<SqlCommand> ExtractValuableCommands()
        {
            return toList(Value.Split(new[] { "GO" + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => !x.StartsWith("PRINT") && !x.StartsWith("GRANT"))
                .Select(x => new SqlCommand(x)));
        }

        public SqlSchemaScript WithDatabaseName(string dbName)
        {
            return new SqlSchemaScript(Value.Replace("$(DatabaseName)", dbName));
        }

        private static string RemoveSetVars(string script)
        {
            return string.Join(Environment.NewLine, script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !x.StartsWith(":")));
        }

        public Lst<SqlString> ExtractCreatedTables()
        {
            var matches = CreateTableRegex.Matches(Value);
            
            return toList(matches.OfType<Match>()
                .Select(x => new SqlString(x.Groups[1].Value)));
        }
    }

    public class SqlString : NewType<SqlString, string>
    {
        public SqlString(string value) : base(value)
        {
        }
    }

    public class SqlCommand : NewType<SqlCommand, string>
    {
        public SqlCommand(string value) : base(value)
        {
        }
    }
}
