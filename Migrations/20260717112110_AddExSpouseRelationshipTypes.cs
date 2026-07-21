using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExSpouseRelationshipTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RelationshipTypes",
                columns: new[] { "Id", "Category", "GenerationDelta", "IsActive", "TypeCode", "TypeLabel" },
                values: new object[,]
                {
                    { 44, "DIRECT", 0, true, "EX_WIFE", "Ex Wife" },
                    { 45, "DIRECT", 0, true, "EX_HUSBAND", "Ex Husband" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RelationshipTypes",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "RelationshipTypes",
                keyColumn: "Id",
                keyValue: 45);
        }
    }
}
