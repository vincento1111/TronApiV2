using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TronApi.Migrations
{
    public partial class AddEnergyToUserStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Energy",
                table: "UsersStats",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Energy",
                table: "UsersStats");
        }
    }
}
