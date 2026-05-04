using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Steinpilz.LocalDb.Tests
{
    public class DeploySchemaTests
    {
        [Fact]
        public void it_does_not_set_deployed_mark_when_run_scripts_reports_failure()
        {
            // Regression: a partial schema deploy must not poison the cache. If RunScripts had any
            // command failures, AlreadyDeployed() must return false on the next run so the schema
            // gets reapplied; otherwise tests crash with "Cannot find object X" on stale runners.
            var db = new TestableDbWrapper(BuildParams(), runScriptsResult: false);

            db.DeploySchema();

            db.SetDeployedMarkCalls.ShouldBe(0);
        }

        [Fact]
        public void it_sets_deployed_mark_when_run_scripts_reports_success()
        {
            var db = new TestableDbWrapper(BuildParams(), runScriptsResult: true);

            db.DeploySchema();

            db.SetDeployedMarkCalls.ShouldBe(1);
        }

        [Fact]
        public void it_skips_deployment_when_already_deployed()
        {
            var db = new TestableDbWrapper(BuildParams(), runScriptsResult: true, alreadyDeployed: true);

            db.DeploySchema();

            db.RunScriptsCalls.ShouldBe(0);
            db.SetDeployedMarkCalls.ShouldBe(0);
        }

        private static DbParams BuildParams() => new DbParams(
            ConnectionString.Custom(@"Data Source=(LocalDb)\unused;Integrated Security=SSPI;"),
            DbSchema.FromSqlScript(SqlSchemaScript.Create("CREATE TABLE foo (id int) GO\r\n")),
            "stub-db",
            useSchemaHashSuffix: true);

        private class TestableDbWrapper : DbWrapper
        {
            private readonly bool runScriptsResult;
            private readonly bool alreadyDeployed;

            public int RunScriptsCalls { get; private set; }
            public int SetDeployedMarkCalls { get; private set; }

            public TestableDbWrapper(DbParams @params, bool runScriptsResult, bool alreadyDeployed = false)
                : base(@params)
            {
                this.runScriptsResult = runScriptsResult;
                this.alreadyDeployed = alreadyDeployed;
            }

            protected override bool RunScripts(IEnumerable<string> commands)
            {
                RunScriptsCalls++;
                return runScriptsResult;
            }

            protected override bool AlreadyDeployed(string hash) => alreadyDeployed;

            protected override void SetDeployedMark(string hash) => SetDeployedMarkCalls++;
        }
    }
}
