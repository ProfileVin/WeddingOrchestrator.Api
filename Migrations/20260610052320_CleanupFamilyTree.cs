using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeddingOrchestrator.Api.Migrations
{
    /// <inheritdoc />
    public partial class CleanupFamilyTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop any FK constraints on child tables that reference Families
            migrationBuilder.Sql(@"
DECLARE @sql NVARCHAR(MAX) = '';
SELECT @sql += 'ALTER TABLE [' + OBJECT_NAME(fk.parent_object_id) + '] DROP CONSTRAINT [' + fk.name + '];'
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.referenced_object_id) = 'Families';
EXEC sp_executesql @sql;");

            // 2. Drop legacy tables in dependency order
            migrationBuilder.Sql("IF OBJECT_ID('FamilyMembers', 'U') IS NOT NULL DROP TABLE [FamilyMembers];");
            migrationBuilder.Sql("IF OBJECT_ID('Marriages',     'U') IS NOT NULL DROP TABLE [Marriages];");
            migrationBuilder.Sql("IF OBJECT_ID('Families',      'U') IS NOT NULL DROP TABLE [Families];");

            // 3. Create RelationshipTypes if not present
            migrationBuilder.Sql(@"
IF OBJECT_ID('RelationshipTypes', 'U') IS NULL
BEGIN
    CREATE TABLE [RelationshipTypes] (
        [Id]              int           NOT NULL IDENTITY,
        [TypeCode]        nvarchar(50)  NOT NULL,
        [TypeLabel]       nvarchar(100) NOT NULL,
        [Category]        nvarchar(20)  NOT NULL,
        [GenerationDelta] int           NOT NULL,
        [IsActive]        bit           NOT NULL,
        CONSTRAINT [PK_RelationshipTypes] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_RelationshipTypes_TypeCode] ON [RelationshipTypes] ([TypeCode]);
END");

            // 4. Create PersonRelationships if not present
            migrationBuilder.Sql(@"
IF OBJECT_ID('PersonRelationships', 'U') IS NULL
BEGIN
    CREATE TABLE [PersonRelationships] (
        [Id]                 int       NOT NULL IDENTITY,
        [FromPersonId]       int       NOT NULL,
        [ToPersonId]         int       NOT NULL,
        [RelationshipTypeId] int       NOT NULL,
        [IsActive]           bit       NOT NULL,
        [CreatedAt]          datetime2 NOT NULL,
        CONSTRAINT [PK_PersonRelationships] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PersonRelationships_People_FromPersonId]
            FOREIGN KEY ([FromPersonId]) REFERENCES [People]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PersonRelationships_People_ToPersonId]
            FOREIGN KEY ([ToPersonId]) REFERENCES [People]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PersonRelationships_RelationshipTypes_RelationshipTypeId]
            FOREIGN KEY ([RelationshipTypeId]) REFERENCES [RelationshipTypes]([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_PersonRelationships_FromPersonId_ToPersonId_RelationshipTypeId]
        ON [PersonRelationships] ([FromPersonId], [ToPersonId], [RelationshipTypeId]);
    CREATE INDEX [IX_PersonRelationships_RelationshipTypeId] ON [PersonRelationships] ([RelationshipTypeId]);
    CREATE INDEX [IX_PersonRelationships_ToPersonId]         ON [PersonRelationships] ([ToPersonId]);
END");

            // 5. Seed RelationshipTypes (idempotent MERGE)
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [RelationshipTypes] ON;
MERGE [RelationshipTypes] AS t
USING (VALUES
    (1,'DIRECT', 1,1,'FATHER',          'Father'),
    (2,'DIRECT', 1,1,'MOTHER',          'Mother'),
    (3,'DIRECT',-1,1,'SON',             'Son'),
    (4,'DIRECT',-1,1,'DAUGHTER',        'Daughter'),
    (5,'DIRECT', 0,1,'HUSBAND',         'Husband'),
    (6,'DIRECT', 0,1,'WIFE',            'Wife'),
    (7,'DIRECT', 0,1,'BROTHER',         'Brother'),
    (8,'DIRECT', 0,1,'SISTER',          'Sister'),
    (9,'EXTENDED', 2,1,'GRANDFATHER',   'Grandfather'),
    (10,'EXTENDED', 2,1,'GRANDMOTHER',  'Grandmother'),
    (11,'EXTENDED',-2,1,'GRANDSON',     'Grandson'),
    (12,'EXTENDED',-2,1,'GRANDDAUGHTER','Granddaughter'),
    (13,'EXTENDED', 1,1,'UNCLE',        'Uncle'),
    (14,'EXTENDED', 1,1,'AUNT',         'Aunt'),
    (15,'EXTENDED',-1,1,'NEPHEW',       'Nephew'),
    (16,'EXTENDED',-1,1,'NIECE',        'Niece'),
    (17,'EXTENDED', 0,1,'COUSIN',       'Cousin'),
    (18,'INLAW', 1,1,'FATHER_IN_LAW',   'Father-in-law'),
    (19,'INLAW', 1,1,'MOTHER_IN_LAW',   'Mother-in-law'),
    (20,'INLAW',-1,1,'SON_IN_LAW',      'Son-in-law'),
    (21,'INLAW',-1,1,'DAUGHTER_IN_LAW', 'Daughter-in-law'),
    (22,'INLAW', 0,1,'BROTHER_IN_LAW',  'Brother-in-law'),
    (23,'INLAW', 0,1,'SISTER_IN_LAW',   'Sister-in-law'),
    (24,'STEP', 1,1,'STEP_FATHER',      'Step-father'),
    (25,'STEP', 1,1,'STEP_MOTHER',      'Step-mother'),
    (26,'STEP',-1,1,'STEP_SON',         'Step-son'),
    (27,'STEP',-1,1,'STEP_DAUGHTER',    'Step-daughter'),
    (28,'STEP', 0,1,'STEP_BROTHER',     'Step-brother'),
    (29,'STEP', 0,1,'STEP_SISTER',      'Step-sister'),
    (30,'ADOPTED', 1,1,'ADOPTIVE_FATHER',  'Adoptive Father'),
    (31,'ADOPTED', 1,1,'ADOPTIVE_MOTHER',  'Adoptive Mother'),
    (32,'ADOPTED',-1,1,'ADOPTED_SON',      'Adopted Son'),
    (33,'ADOPTED',-1,1,'ADOPTED_DAUGHTER', 'Adopted Daughter'),
    (34,'HALF', 0,1,'HALF_BROTHER',     'Half-brother'),
    (35,'HALF', 0,1,'HALF_SISTER',      'Half-sister')
) AS s(Id,Category,GenerationDelta,IsActive,TypeCode,TypeLabel)
ON t.Id = s.Id
WHEN NOT MATCHED THEN
    INSERT (Id,Category,GenerationDelta,IsActive,TypeCode,TypeLabel)
    VALUES (s.Id,s.Category,s.GenerationDelta,s.IsActive,s.TypeCode,s.TypeLabel);
SET IDENTITY_INSERT [RelationshipTypes] OFF;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('PersonRelationships', 'U') IS NOT NULL DROP TABLE [PersonRelationships];");
            migrationBuilder.Sql("IF OBJECT_ID('RelationshipTypes',   'U') IS NOT NULL DROP TABLE [RelationshipTypes];");
        }
    }
}
