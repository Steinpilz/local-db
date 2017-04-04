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

namespace Steinpilz.LocalDb
{
    public class DbWrapper
    {
        readonly DbParams @params;

        string MasterConnectionString { get; }
        public string ConnectionString { get; private set; }

        public DbWrapper(DbParams @params)
        {
            this.@params = @params;

            MasterConnectionString = (string)@params.ConnectionString.ForDatabase("master");
            ConnectionString = (string)@params.ConnectionString.ForDatabase(@params.DatabaseName);
        }

        public void DeploySchema()
        {
            var script = this.@params.DatabaseSchema.SqlScript.WithDatabaseName(this.@params.DatabaseName);

            var hash = HashSum((string)script);
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
            for (;;)
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
                        // silence
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
    [ToString]
    public class DbParams
    {
        public ConnectionString ConnectionString { get; }
        public DbSchema DatabaseSchema { get; }
        public string DatabaseName { get; }
        public bool UseSchemaHashSuffix { get; }

        public DbParams(
            ConnectionString masterConnectionString,
            DbSchema dbSchema,
            string dbName,
            bool addSchemaHashToDbName
            )
        {
            ConnectionString = masterConnectionString;
            DatabaseSchema = dbSchema;
            DatabaseName = dbName;
            UseSchemaHashSuffix = addSchemaHashToDbName;
        }

        public DbParams WithConnectionString(ConnectionString connString) => this;
        public DbParams WithDatabaseSchema(DbSchema dbSchema) => this;
        public DbParams WithDatabaseName(string databaseName) => this;
        public DbParams WithUseSchemaHashSuffix(bool useSchemaHashSuffix) => this;
    }

    public class ConnectionString : NewType<ConnectionString, string>
    {
        private ConnectionString(string value) : base(value)
        {
        }

        public static ConnectionString Custom(string value)
            => new ConnectionString(value);

        public static ConnectionString LocalDb(LocalDbVersion version)
            => new ConnectionString($@"DataSource=(LocalDb)\{(string)version};Integrated Security=SSPI;");

        public ConnectionString ForDatabase(string dbName)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(Value)
            {
                InitialCatalog = dbName
            };

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
    [ToString]
    public class DbSchema
    {
        public SqlSchemaScript SqlScript { get; private set; }

        private DbSchema() { }

        public static DbSchema FromSqlScript(SqlSchemaScript sql)
            => new DbSchema { SqlScript = sql };

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
