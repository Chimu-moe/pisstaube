using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.Core.Database.Migrations
{
    public partial class IpfsHash : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpfsHash",
                table: "BeatmapSet",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpfsHash",
                table: "BeatmapSet");
        }
    }
}
