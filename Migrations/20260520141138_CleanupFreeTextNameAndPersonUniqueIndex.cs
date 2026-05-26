using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class CleanupFreeTextNameAndPersonUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeTextName",
                table: "WeddingRoles");

            migrationBuilder.CreateIndex(
                name: "IX_People_FirstName_LastName",
                table: "People",
                columns: new[] { "FirstName", "LastName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_People_FirstName_LastName",
                table: "People");

            migrationBuilder.AddColumn<string>(
                name: "FreeTextName",
                table: "WeddingRoles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
