using System.Text;
using Xunit;

namespace Steinpilz.LocalDb.Tests
{
    public class SqlConnectionTests
    {
        [Fact]
        public void it_runs_queries_against_localdb()
        {
            var db = new DbWrapper(new DbParams(
                ConnectionString.Custom(@"Data Source=(LocalDB)\tests;AttachDbFileName=r:\local-db\{database}.mdf;Integrated Security=true;"),
                DbSchema.FromSqlScript(SqlSchemaScript.Create(DbScript())),
                "test-db",
                true));

            db.DeploySchema();
        }

        private static string DbScript()
        {
            return Encoding.UTF8.GetString(StreamUtil.ReadFully(
                typeof(SqlConnectionTests).Assembly.GetManifestResourceStream("Steinpilz.LocalDb.Tests.db.sql")));
        }
    }
}
