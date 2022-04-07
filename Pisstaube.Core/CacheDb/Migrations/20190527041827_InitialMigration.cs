using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.CacheDb.Migrations
{
    public partial class InitialMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                "CacheBeatmapSet",
                table => new
                {
                    SetId = table.Column<int>()
                        .Annotation("Sqlite:Autoincrement", true),
                    DownloadCount = table.Column<long>(),
                    LastDownload = table.Column<DateTime>()
                },
                constraints: table => { table.PrimaryKey("PK_CacheBeatmapSet", x => x.SetId); });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                "CacheBeatmapSet");
        }
    }
}