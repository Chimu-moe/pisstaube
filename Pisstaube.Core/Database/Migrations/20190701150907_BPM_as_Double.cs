using Microsoft.EntityFrameworkCore.Migrations;

namespace Pisstaube.Core.Database.Migrations
{
    public partial class BpmAsDouble : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                "Bpm",
                "Beatmaps",
                nullable: false,
                oldClrType: typeof(float));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<float>(
                "Bpm",
                "Beatmaps",
                nullable: false,
                oldClrType: typeof(double));
        }
    }
}