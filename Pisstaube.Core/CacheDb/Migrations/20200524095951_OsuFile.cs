using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.CacheDb.Migrations
{
    public partial class OsuFile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "CacheBeatmaps",
                nullable: true);

            migrationBuilder.CreateIndex("tCacheBeatmaps_File", "CacheBeatmaps", "File");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "File",
                table: "CacheBeatmaps");
        }
    }
}
