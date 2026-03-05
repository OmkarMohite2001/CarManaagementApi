using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarManaagementApi.Migrations.RentX
{
    /// <inheritdoc />
    public partial class EmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                schema: "rentx",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE rentx.Users SET IsEmailVerified = 1;");

            migrationBuilder.CreateTable(
                name: "UserEmailVerifications",
                schema: "rentx",
                columns: table => new
                {
                    VerificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    VerificationCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEmailVerifications", x => x.VerificationId);
                    table.ForeignKey(
                        name: "FK_UserEmailVerifications_Users",
                        column: x => x.UserId,
                        principalSchema: "rentx",
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserEmailVerifications_User_Code",
                schema: "rentx",
                table: "UserEmailVerifications",
                columns: new[] { "UserId", "VerificationCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEmailVerifications",
                schema: "rentx");

            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                schema: "rentx",
                table: "Users");
        }
    }
}
