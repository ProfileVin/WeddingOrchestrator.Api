using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStepGrandRelationshipTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [RelationshipTypes] ON;
MERGE [RelationshipTypes] AS t
USING (VALUES
    (36,'STEP', 2,1,'STEP_GRANDFATHER',   'Step-grandfather'),
    (37,'STEP', 2,1,'STEP_GRANDMOTHER',   'Step-grandmother'),
    (38,'STEP',-2,1,'STEP_GRANDSON',      'Step-grandson'),
    (39,'STEP',-2,1,'STEP_GRANDDAUGHTER', 'Step-granddaughter')
) AS s(Id,Category,GenerationDelta,IsActive,TypeCode,TypeLabel)
ON t.Id = s.Id
WHEN NOT MATCHED THEN
    INSERT (Id,Category,GenerationDelta,IsActive,TypeCode,TypeLabel)
    VALUES (s.Id,s.Category,s.GenerationDelta,s.IsActive,s.TypeCode,s.TypeLabel);
SET IDENTITY_INSERT [RelationshipTypes] OFF;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [RelationshipTypes] WHERE Id IN (36, 37, 38, 39);");
        }
    }
}
