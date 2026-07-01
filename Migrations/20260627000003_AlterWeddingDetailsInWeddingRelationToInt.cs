using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WeddingOrchestrator.Api.Data;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627000003_AlterWeddingDetailsInWeddingRelationToInt")]
    public partial class AlterWeddingDetailsInWeddingRelationToInt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InWeddingRelation",
                table: "WeddingDetails");

            migrationBuilder.AddColumn<int>(
                name: "InWeddingRelationTypeId",
                table: "WeddingDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeddingSide",
                table: "WeddingDetails",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingDetails_InWeddingRelationTypeId",
                table: "WeddingDetails",
                column: "InWeddingRelationTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingDetails_RelationshipTypes_InWeddingRelationTypeId",
                table: "WeddingDetails",
                column: "InWeddingRelationTypeId",
                principalTable: "RelationshipTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeddingDetails_RelationshipTypes_InWeddingRelationTypeId",
                table: "WeddingDetails");

            migrationBuilder.DropIndex(
                name: "IX_WeddingDetails_InWeddingRelationTypeId",
                table: "WeddingDetails");

            migrationBuilder.DropColumn(
                name: "InWeddingRelationTypeId",
                table: "WeddingDetails");

            migrationBuilder.DropColumn(
                name: "WeddingSide",
                table: "WeddingDetails");

            migrationBuilder.AddColumn<string>(
                name: "InWeddingRelation",
                table: "WeddingDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
