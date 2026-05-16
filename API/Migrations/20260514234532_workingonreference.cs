using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class workingonreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "Wallets",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_Reference",
                table: "Wallets",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Wallets_Reference",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "Wallets");
        }
    }
}
