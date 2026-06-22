using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonFamilyGroupAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamilyGroup",
                table: "People",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "People",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FamilyGroup",
                table: "People");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "People");
        }
    }
}
