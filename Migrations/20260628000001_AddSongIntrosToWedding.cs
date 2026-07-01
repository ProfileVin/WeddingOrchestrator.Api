using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WeddingOrchestrator.Api.Data;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260628000001_AddSongIntrosToWedding")]
    public partial class AddSongIntrosToWedding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeddingSongIntroId",
                table: "Weddings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FatherMotherWeddingSongIntroGroomId",
                table: "Weddings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FatherMotherWeddingSongIntroBrideId",
                table: "Weddings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Weddings_WeddingSongIntroId",
                table: "Weddings",
                column: "WeddingSongIntroId");

            migrationBuilder.CreateIndex(
                name: "IX_Weddings_FatherMotherWeddingSongIntroGroomId",
                table: "Weddings",
                column: "FatherMotherWeddingSongIntroGroomId");

            migrationBuilder.CreateIndex(
                name: "IX_Weddings_FatherMotherWeddingSongIntroBrideId",
                table: "Weddings",
                column: "FatherMotherWeddingSongIntroBrideId");

            migrationBuilder.AddForeignKey(
                name: "FK_Weddings_Songs_WeddingSongIntroId",
                table: "Weddings",
                column: "WeddingSongIntroId",
                principalTable: "Songs",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Weddings_Songs_FatherMotherWeddingSongIntroGroomId",
                table: "Weddings",
                column: "FatherMotherWeddingSongIntroGroomId",
                principalTable: "Songs",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Weddings_Songs_FatherMotherWeddingSongIntroBrideId",
                table: "Weddings",
                column: "FatherMotherWeddingSongIntroBrideId",
                principalTable: "Songs",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Weddings_Songs_WeddingSongIntroId",
                table: "Weddings");

            migrationBuilder.DropForeignKey(
                name: "FK_Weddings_Songs_FatherMotherWeddingSongIntroGroomId",
                table: "Weddings");

            migrationBuilder.DropForeignKey(
                name: "FK_Weddings_Songs_FatherMotherWeddingSongIntroBrideId",
                table: "Weddings");

            migrationBuilder.DropIndex(
                name: "IX_Weddings_WeddingSongIntroId",
                table: "Weddings");

            migrationBuilder.DropIndex(
                name: "IX_Weddings_FatherMotherWeddingSongIntroGroomId",
                table: "Weddings");

            migrationBuilder.DropIndex(
                name: "IX_Weddings_FatherMotherWeddingSongIntroBrideId",
                table: "Weddings");

            migrationBuilder.DropColumn(
                name: "WeddingSongIntroId",
                table: "Weddings");

            migrationBuilder.DropColumn(
                name: "FatherMotherWeddingSongIntroGroomId",
                table: "Weddings");

            migrationBuilder.DropColumn(
                name: "FatherMotherWeddingSongIntroBrideId",
                table: "Weddings");
        }
    }
}
