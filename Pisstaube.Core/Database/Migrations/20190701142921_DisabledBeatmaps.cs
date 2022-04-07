using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.Core.Database.Migrations
{
    public partial class DisabledBeatmaps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                "Disabled",
                "BeatmapSet",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                "Disabled",
                "BeatmapSet");
        }
    }
}