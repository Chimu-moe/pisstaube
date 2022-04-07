using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.CacheDb.Migrations
{
    public partial class FileHashes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                "Hash",
                "CacheBeatmapSet",
                nullable: true);

            migrationBuilder.CreateTable(
                "CacheBeatmaps",
                table => new
                {
                    BeatmapId = table.Column<int>()
                        .Annotation("Sqlite:Autoincrement", true),
                    Hash = table.Column<string>(nullable: true),
                    FileMd5 = table.Column<string>(nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_CacheBeatmaps", x => x.BeatmapId); });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                "CacheBeatmaps");

            migrationBuilder.DropColumn(
                "Hash",
                "CacheBeatmapSet");
        }
    }
}