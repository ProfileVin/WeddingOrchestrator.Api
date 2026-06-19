using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGrandInLawRelationshipTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [RelationshipTypes] ON;
MERGE [RelationshipTypes] AS t
USING (VALUES
    (40,'INLAW',-2,1,'GRANDSON_IN_LAW',      'Grandson-in-law'),
    (41,'INLAW',-2,1,'GRANDDAUGHTER_IN_LAW', 'Granddaughter-in-law')
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
DELETE FROM [RelationshipTypes] WHERE Id IN (40, 41);");
        }
    }
}
