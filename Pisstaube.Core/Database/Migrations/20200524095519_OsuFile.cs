using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.Core.Database.Migrations
{
    public partial class OsuFile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "File",
                table: "Beatmaps",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "File",
                table: "Beatmaps");
        }
    }
}
