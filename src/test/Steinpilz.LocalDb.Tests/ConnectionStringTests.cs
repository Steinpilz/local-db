using Shouldly;
using Xunit;

namespace Steinpilz.LocalDb.Tests
{
    public class ConnectionStringTests
    {
        [Fact]
        public void it_generates_connection_string_for_database_name()
        {
            var connectionString = ConnectionString.Custom("Data Source=db.stein-pilz.com,1434;User Id=testrunner;Password=testrunner;Connection Timeout=600;");

            var namedConnectionString = connectionString.ForDatabase("some-db-name");

            var raw = (string)namedConnectionString;
            raw.ShouldContain("some-db-name");
        }

        [Fact]
        public void it_supports_attached_file_name_with_database_token()
        {
            var connectionString = ConnectionString.Custom(
                "Data Source=db.stein-pilz.com,1434;User Id=testrunner;Password=testrunner;Connection Timeout=600;AttachDbFilename=R:\\local-db\\{database}.mdf");

            var namedConnectionString = connectionString.ForDatabase("test-db");

            var raw = (string)namedConnectionString;
            raw.ShouldContain(@"Data Source=db.stein-pilz.com,1434;AttachDbFilename=R:\local-db\test-db.mdf;Initial Catalog=test-db;User ID=testrunner;Password=testrunner;Connect Timeout=600");
        }
    }
}
