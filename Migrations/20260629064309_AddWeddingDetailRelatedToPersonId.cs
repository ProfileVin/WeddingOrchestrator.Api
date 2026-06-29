using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWeddingDetailRelatedToPersonId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RelatedToPersonId",
                table: "WeddingDetails",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingDetails_RelatedToPersonId",
                table: "WeddingDetails",
                column: "RelatedToPersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeddingDetails_People_RelatedToPersonId",
                table: "WeddingDetails",
                column: "RelatedToPersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeddingDetails_People_RelatedToPersonId",
                table: "WeddingDetails");

            migrationBuilder.DropIndex(
                name: "IX_WeddingDetails_RelatedToPersonId",
                table: "WeddingDetails");

            migrationBuilder.DropColumn(
                name: "RelatedToPersonId",
                table: "WeddingDetails");
        }
    }
}
