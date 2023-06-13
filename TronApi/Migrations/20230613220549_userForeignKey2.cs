using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TronApi.Migrations
{
    public partial class userForeignKey2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UsersStats_UserId",
                table: "UsersStats",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInventories_ItemId",
                table: "UserInventories",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInventories_UserId",
                table: "UserInventories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_UserId",
                table: "Profiles",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Profiles_Users_UserId",
                table: "Profiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserInventories_MasterItemsTables_ItemId",
                table: "UserInventories",
                column: "ItemId",
                principalTable: "MasterItemsTables",
                principalColumn: "ItemId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserInventories_Users_UserId",
                table: "UserInventories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UsersStats_Users_UserId",
                table: "UsersStats",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Profiles_Users_UserId",
                table: "Profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserInventories_MasterItemsTables_ItemId",
                table: "UserInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_UserInventories_Users_UserId",
                table: "UserInventories");

            migrationBuilder.DropForeignKey(
                name: "FK_UsersStats_Users_UserId",
                table: "UsersStats");

            migrationBuilder.DropIndex(
                name: "IX_UsersStats_UserId",
                table: "UsersStats");

            migrationBuilder.DropIndex(
                name: "IX_UserInventories_ItemId",
                table: "UserInventories");

            migrationBuilder.DropIndex(
                name: "IX_UserInventories_UserId",
                table: "UserInventories");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_UserId",
                table: "Profiles");
        }
    }
}
