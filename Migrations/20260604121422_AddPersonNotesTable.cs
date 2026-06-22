using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonNotesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "People");

            migrationBuilder.CreateTable(
                name: "PersonNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    WeddingId = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonNotes_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonNotes_Weddings_WeddingId",
                        column: x => x.WeddingId,
                        principalTable: "Weddings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonNotes_PersonId",
                table: "PersonNotes",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonNotes_WeddingId",
                table: "PersonNotes",
                column: "WeddingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonNotes");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "People",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
