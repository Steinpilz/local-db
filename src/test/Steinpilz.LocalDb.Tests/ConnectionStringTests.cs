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
    }
}
