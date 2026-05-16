using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class updatingtransactiontable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ToWalletId",
                table: "Transactionss",
                newName: "ToEmail");

            migrationBuilder.RenameColumn(
                name: "FromWalletId",
                table: "Transactionss",
                newName: "FromEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ToEmail",
                table: "Transactionss",
                newName: "ToWalletId");

            migrationBuilder.RenameColumn(
                name: "FromEmail",
                table: "Transactionss",
                newName: "FromWalletId");
        }
    }
}
