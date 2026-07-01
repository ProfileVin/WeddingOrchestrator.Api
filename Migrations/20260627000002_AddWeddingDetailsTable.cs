using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WeddingOrchestrator.Api.Data;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627000002_AddWeddingDetailsTable")]
    public partial class AddWeddingDetailsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeddingDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeddingId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    RoleType = table.Column<int>(type: "int", nullable: false),
                    InWeddingRelation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SongId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingDetails", x => x.Id);

                    table.ForeignKey(
                        name: "FK_WeddingDetails_Weddings_WeddingId",
                        column: x => x.WeddingId,
                        principalTable: "Weddings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_WeddingDetails_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);

                    table.ForeignKey(
                        name: "FK_WeddingDetails_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingDetails_WeddingId",
                table: "WeddingDetails",
                column: "WeddingId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingDetails_PersonId",
                table: "WeddingDetails",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingDetails_SongId",
                table: "WeddingDetails",
                column: "SongId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WeddingDetails");
        }
    }
}
