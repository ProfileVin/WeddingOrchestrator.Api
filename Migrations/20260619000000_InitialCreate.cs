using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    FamilyGroup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FatherId = table.Column<int>(type: "int", nullable: true),
                    MotherId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                    table.ForeignKey(
                        name: "FK_People_People_FatherId",
                        column: x => x.FatherId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_People_People_MotherId",
                        column: x => x.MotherId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RelationshipTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TypeLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GenerationDelta = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelationshipTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SongCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Weddings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DateOfWedding = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsFinalized = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weddings", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "PersonRelationships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromPersonId = table.Column<int>(type: "int", nullable: false),
                    ToPersonId = table.Column<int>(type: "int", nullable: false),
                    RelationshipTypeId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonRelationships_People_FromPersonId",
                        column: x => x.FromPersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonRelationships_People_ToPersonId",
                        column: x => x.ToPersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonRelationships_RelationshipTypes_RelationshipTypeId",
                        column: x => x.RelationshipTypeId,
                        principalTable: "RelationshipTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Songs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SongCategoryId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RelativeFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Songs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Songs_SongCategories_SongCategoryId",
                        column: x => x.SongCategoryId,
                        principalTable: "SongCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeddingRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeddingId = table.Column<int>(type: "int", nullable: false),
                    RoleType = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingRoles_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WeddingRoles_Weddings_WeddingId",
                        column: x => x.WeddingId,
                        principalTable: "Weddings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeddingRoleSongAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeddingRoleId = table.Column<int>(type: "int", nullable: false),
                    SongId = table.Column<int>(type: "int", nullable: false),
                    AssignmentSlot = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingRoleSongAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingRoleSongAssignments_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingRoleSongAssignments_WeddingRoles_WeddingRoleId",
                        column: x => x.WeddingRoleId,
                        principalTable: "WeddingRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "RelationshipTypes",
                columns: new[] { "Id", "Category", "GenerationDelta", "IsActive", "TypeCode", "TypeLabel" },
                values: new object[,]
                {
                    { 1,  "DIRECT",   1,  true, "FATHER",               "Father" },
                    { 2,  "DIRECT",   1,  true, "MOTHER",               "Mother" },
                    { 3,  "DIRECT",  -1,  true, "SON",                  "Son" },
                    { 4,  "DIRECT",  -1,  true, "DAUGHTER",             "Daughter" },
                    { 5,  "DIRECT",   0,  true, "HUSBAND",              "Husband" },
                    { 6,  "DIRECT",   0,  true, "WIFE",                 "Wife" },
                    { 7,  "DIRECT",   0,  true, "BROTHER",              "Brother" },
                    { 8,  "DIRECT",   0,  true, "SISTER",               "Sister" },
                    { 9,  "EXTENDED", 2,  true, "GRANDFATHER",          "Grandfather" },
                    { 10, "EXTENDED", 2,  true, "GRANDMOTHER",          "Grandmother" },
                    { 11, "EXTENDED", -2, true, "GRANDSON",             "Grandson" },
                    { 12, "EXTENDED", -2, true, "GRANDDAUGHTER",        "Granddaughter" },
                    { 13, "EXTENDED", 1,  true, "UNCLE",                "Uncle" },
                    { 14, "EXTENDED", 1,  true, "AUNT",                 "Aunt" },
                    { 15, "EXTENDED", -1, true, "NEPHEW",               "Nephew" },
                    { 16, "EXTENDED", -1, true, "NIECE",                "Niece" },
                    { 17, "EXTENDED", 0,  true, "COUSIN",               "Cousin" },
                    { 18, "INLAW",    1,  true, "FATHER_IN_LAW",        "Father-in-law" },
                    { 19, "INLAW",    1,  true, "MOTHER_IN_LAW",        "Mother-in-law" },
                    { 20, "INLAW",   -1,  true, "SON_IN_LAW",           "Son-in-law" },
                    { 21, "INLAW",   -1,  true, "DAUGHTER_IN_LAW",      "Daughter-in-law" },
                    { 22, "INLAW",    0,  true, "BROTHER_IN_LAW",       "Brother-in-law" },
                    { 23, "INLAW",    0,  true, "SISTER_IN_LAW",        "Sister-in-law" },
                    { 24, "STEP",     1,  true, "STEP_FATHER",          "Step-father" },
                    { 25, "STEP",     1,  true, "STEP_MOTHER",          "Step-mother" },
                    { 26, "STEP",    -1,  true, "STEP_SON",             "Step-son" },
                    { 27, "STEP",    -1,  true, "STEP_DAUGHTER",        "Step-daughter" },
                    { 28, "STEP",     0,  true, "STEP_BROTHER",         "Step-brother" },
                    { 29, "STEP",     0,  true, "STEP_SISTER",          "Step-sister" },
                    { 30, "ADOPTED",  1,  true, "ADOPTIVE_FATHER",      "Adoptive Father" },
                    { 31, "ADOPTED",  1,  true, "ADOPTIVE_MOTHER",      "Adoptive Mother" },
                    { 32, "ADOPTED", -1,  true, "ADOPTED_SON",          "Adopted Son" },
                    { 33, "ADOPTED", -1,  true, "ADOPTED_DAUGHTER",     "Adopted Daughter" },
                    { 34, "HALF",     0,  true, "HALF_BROTHER",         "Half-brother" },
                    { 35, "HALF",     0,  true, "HALF_SISTER",          "Half-sister" },
                    { 36, "STEP",     2,  true, "STEP_GRANDFATHER",     "Step-grandfather" },
                    { 37, "STEP",     2,  true, "STEP_GRANDMOTHER",     "Step-grandmother" },
                    { 38, "STEP",    -2,  true, "STEP_GRANDSON",        "Step-grandson" },
                    { 39, "STEP",    -2,  true, "STEP_GRANDDAUGHTER",   "Step-granddaughter" },
                    { 40, "INLAW",   -2,  true, "GRANDSON_IN_LAW",      "Grandson-in-law" },
                    { 41, "INLAW",   -2,  true, "GRANDDAUGHTER_IN_LAW", "Granddaughter-in-law" },
                    { 42, "INLAW",    2,  true, "GRANDFATHER_IN_LAW",   "Grandfather-in-law" },
                    { 43, "INLAW",    2,  true, "GRANDMOTHER_IN_LAW",   "Grandmother-in-law" }
                });

            migrationBuilder.InsertData(
                table: "SongCategories",
                columns: new[] { "Id", "DisplayOrder", "Name" },
                values: new object[,]
                {
                    { 1, 1, "Groom Songs" },
                    { 2, 2, "Bride Songs" },
                    { 3, 3, "Father Songs" },
                    { 4, 4, "Mother Songs" },
                    { 5, 5, "Grandmother Songs" },
                    { 6, 6, "Grandfather Songs" },
                    { 7, 7, "Intro for Father/Mother" },
                    { 8, 8, "Wedding Intros" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_People_FatherId",
                table: "People",
                column: "FatherId");

            migrationBuilder.CreateIndex(
                name: "IX_People_FirstName_LastName",
                table: "People",
                columns: new[] { "FirstName", "LastName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_People_MotherId",
                table: "People",
                column: "MotherId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonNotes_PersonId",
                table: "PersonNotes",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonNotes_WeddingId",
                table: "PersonNotes",
                column: "WeddingId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonRelationships_FromPersonId_ToPersonId_RelationshipTypeId",
                table: "PersonRelationships",
                columns: new[] { "FromPersonId", "ToPersonId", "RelationshipTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonRelationships_RelationshipTypeId",
                table: "PersonRelationships",
                column: "RelationshipTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonRelationships_ToPersonId",
                table: "PersonRelationships",
                column: "ToPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RelationshipTypes_TypeCode",
                table: "RelationshipTypes",
                column: "TypeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_SongCategoryId",
                table: "Songs",
                column: "SongCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingRoles_PersonId",
                table: "WeddingRoles",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingRoles_WeddingId_RoleType",
                table: "WeddingRoles",
                columns: new[] { "WeddingId", "RoleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingRoleSongAssignments_SongId",
                table: "WeddingRoleSongAssignments",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingRoleSongAssignments_WeddingRoleId_AssignmentSlot",
                table: "WeddingRoleSongAssignments",
                columns: new[] { "WeddingRoleId", "AssignmentSlot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PersonNotes");
            migrationBuilder.DropTable(name: "PersonRelationships");
            migrationBuilder.DropTable(name: "WeddingRoleSongAssignments");
            migrationBuilder.DropTable(name: "RelationshipTypes");
            migrationBuilder.DropTable(name: "Songs");
            migrationBuilder.DropTable(name: "WeddingRoles");
            migrationBuilder.DropTable(name: "SongCategories");
            migrationBuilder.DropTable(name: "People");
            migrationBuilder.DropTable(name: "Weddings");
        }
    }
}
