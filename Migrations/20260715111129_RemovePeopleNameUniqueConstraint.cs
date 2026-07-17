using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemovePeopleNameUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_People_FirstName_LastName",
                table: "People");

            migrationBuilder.CreateIndex(
                name: "IX_People_FirstName_LastName",
                table: "People",
                columns: new[] { "FirstName", "LastName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_People_FirstName_LastName",
                table: "People");

            migrationBuilder.CreateIndex(
                name: "IX_People_FirstName_LastName",
                table: "People",
                columns: new[] { "FirstName", "LastName" },
                unique: true);
        }
    }
}
