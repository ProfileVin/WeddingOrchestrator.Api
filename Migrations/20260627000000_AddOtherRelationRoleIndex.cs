using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WeddingOrchestrator.Api.Data;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260627000000_AddOtherRelationRoleIndex")]
    public partial class AddOtherRelationRoleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old unfiltered unique index
            migrationBuilder.DropIndex(
                name: "IX_WeddingRoles_WeddingId_RoleType",
                table: "WeddingRoles");

            // Standard roles (1-15): one per wedding per type
            migrationBuilder.CreateIndex(
                name: "IX_WeddingRoles_WeddingId_RoleType",
                table: "WeddingRoles",
                columns: new[] { "WeddingId", "RoleType" },
                unique: true,
                filter: "[RoleType] <> 16");

            // OtherRelation roles (16): one per person per wedding
            migrationBuilder.CreateIndex(
                name: "IX_WeddingRoles_WeddingId_PersonId_OtherRelation",
                table: "WeddingRoles",
                columns: new[] { "WeddingId", "PersonId" },
                unique: true,
                filter: "[RoleType] = 16 AND [PersonId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeddingRoles_WeddingId_PersonId_OtherRelation",
                table: "WeddingRoles");

            migrationBuilder.DropIndex(
                name: "IX_WeddingRoles_WeddingId_RoleType",
                table: "WeddingRoles");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingRoles_WeddingId_RoleType",
                table: "WeddingRoles",
                columns: new[] { "WeddingId", "RoleType" },
                unique: true);
        }
    }
}
