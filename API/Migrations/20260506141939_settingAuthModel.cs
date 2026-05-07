using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class settingAuthModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replace the auto-generated AlterColumn lines with this
            migrationBuilder.Sql(@"
        ALTER TABLE ""UserAuths"" 
        ALTER COLUMN ""ResetTokenExpiry"" TYPE timestamp with time zone 
        USING ""ResetTokenExpiry""::timestamp with time zone;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE ""UserAuths"" 
        ALTER COLUMN ""ResetTokenExpiry"" DROP NOT NULL;
    ");

            // Leave every other operation in the file untouched
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ResetTokenExpiry",
                table: "UserAuths",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ResetToken",
                table: "UserAuths",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
