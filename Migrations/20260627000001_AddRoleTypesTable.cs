using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WeddingOrchestrator.Api.Data;

#nullable disable

#pragma warning disable CA1814

namespace WeddingOrchestrator.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627000001_AddRoleTypesTable")]
    public partial class AddRoleTypesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RoleTypes",
                columns: new[] { "Id", "Name" },
                columnTypes: new[] { "int", "nvarchar(200)" },
                values: new object[,]
                {
                    { 1,  "Groom" },
                    { 2,  "Bride" },
                    { 3,  "Father of the Groom" },
                    { 4,  "Mother of the Groom" },
                    { 5,  "Paternal Grandfather of the Groom" },
                    { 6,  "Paternal Grandmother of the Groom" },
                    { 7,  "Maternal Grandfather of the Groom" },
                    { 8,  "Maternal Grandmother of the Groom" },
                    { 9,  "Father of the Bride" },
                    { 10, "Mother of the Bride" },
                    { 11, "Paternal Grandfather of the Bride" },
                    { 12, "Paternal Grandmother of the Bride" },
                    { 13, "Maternal Grandfather of the Bride" },
                    { 14, "Maternal Grandmother of the Bride" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RoleTypes");
        }
    }
}
