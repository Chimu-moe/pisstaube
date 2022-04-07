using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.Core.Database.Migrations
{
    public partial class InitialMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"ALTER DATABASE CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");

            migrationBuilder.CreateTable(
                "BeatmapSet",
                table => new
                {
                    SetId = table.Column<int>()
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RankedStatus = table.Column<int>(),
                    ApprovedDate = table.Column<DateTime>(nullable: true),
                    LastUpdate = table.Column<DateTime>(nullable: true),
                    LastChecked = table.Column<DateTime>(nullable: true),
                    Artist = table.Column<string>(nullable: true),
                    Title = table.Column<string>(nullable: true),
                    Creator = table.Column<string>(nullable: true),
                    Source = table.Column<string>(nullable: true),
                    Tags = table.Column<string>(nullable: true),
                    HasVideo = table.Column<bool>(),
                    Genre = table.Column<int>(),
                    Language = table.Column<int>(),
                    Favourites = table.Column<long>()
                },
                constraints: table => { table.PrimaryKey("PK_BeatmapSet", x => x.SetId); });

            migrationBuilder.CreateTable(
                "Beatmaps",
                table => new
                {
                    BeatmapId = table.Column<int>()
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ParentSetId = table.Column<int>(),
                    DiffName = table.Column<string>(nullable: true),
                    FileMd5 = table.Column<string>(nullable: true),
                    Mode = table.Column<int>(),
                    Bpm = table.Column<float>(),
                    Ar = table.Column<float>(),
                    Od = table.Column<float>(),
                    Cs = table.Column<float>(),
                    Hp = table.Column<float>(),
                    TotalLength = table.Column<int>(),
                    HitLength = table.Column<long>(),
                    Playcount = table.Column<int>(),
                    Passcount = table.Column<int>(),
                    MaxCombo = table.Column<long>(),
                    DifficultyRating = table.Column<double>()
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beatmaps", x => x.BeatmapId);
                    table.ForeignKey(
                        "FK_Beatmaps_BeatmapSet_ParentSetId",
                        x => x.ParentSetId,
                        "BeatmapSet",
                        "SetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                "IX_Beatmaps_ParentSetId",
                "Beatmaps",
                "ParentSetId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                "Beatmaps");

            migrationBuilder.DropTable(
                "BeatmapSet");
        }
    }
}