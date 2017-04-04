using Shouldly;
using Steinpilz.LocalDb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static LanguageExt.Prelude;

namespace Steinpilz.LocalDb.Tests
{
    public class SqlSchemaScriptTests
    {
        [Fact]
        public void it_extracts_tables_from_script()
        {
            var sut = SqlSchemaScript.Create(@"

CREATE TABLE [Survey].[UIQRSUDE] (
    [SUDE_ID]               INT            IDENTITY (1, 1) NOT NULL,
    [SUDE_Name]             NVARCHAR (100) NULL,
    [SUDE_ConfigSerialized] NVARCHAR (MAX) NULL,
    [SUDE_CreatedDate]      SMALLDATETIME  NOT NULL,
    [SUDE_ModifiedDate]     DATETIME       NOT NULL,
    CONSTRAINT [PK_UIQRSUDE] PRIMARY KEY CLUSTERED ([SUDE_ID] ASC)
);


GO
PRINT N'Creating [Survey].[UIQRPAVE]...';


GO
CREATE TABLE [Survey].[UIQRPAVE] (
    [PAVE_ID]             INT            IDENTITY (1, 1) NOT NULL,
    [PAVE_Info_Fin]       NVARCHAR (17)  NULL,
    [PAVE_Info_Number]    NVARCHAR (20)  NULL,
    [PAVE_InfoSerialized] NVARCHAR (MAX) NULL,
    [PAVE_CreatedDate]    SMALLDATETIME  NOT NULL,
    [PAVE_ModifiedDate]   DATETIME       NOT NULL,
    CONSTRAINT [PK_UIQRPAVE] PRIMARY KEY CLUSTERED ([PAVE_ID] ASC)
);


GO
PRINT N'Creating [Survey].[UIQRPART]...';


GO
CREATE TABLE [Survey].[UIQRPART] (
    [PART_ID]                INT            IDENTITY (1, 1) NOT NULL,
    [PART_Profile_Email]     NVARCHAR (100) NULL,
    [PART_Profile_FirstName] NVARCHAR (100) NULL,
    [PART_Profile_LastName]  NVARCHAR (100) NULL,
    [PART_Profile_Kim]       NVARCHAR (100) NULL,
    [PART_ProfileSerialized] NVARCHAR (MAX) NULL,
    [PART_CreatedDate]       SMALLDATETIME  NOT NULL,
    [PART_ModifiedDate]      DATETIME       NOT NULL,
    CONSTRAINT [PK_UIQRPART] PRIMARY KEY CLUSTERED ([PART_ID] ASC)
);


GO
PRINT N'Creating [Survey].[UIQRNOSC]...';



");

            var tables = sut.ExtractCreatedTables();

            tables.ShouldBe(List(
                new SqlString("[Survey].[UIQRSUDE]"),
                new SqlString("[Survey].[UIQRPAVE]"),
                new SqlString("[Survey].[UIQRPART]")
                ));
        }
    }
}
