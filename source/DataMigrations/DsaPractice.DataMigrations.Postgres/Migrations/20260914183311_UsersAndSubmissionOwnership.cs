using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DsaPractice.DataMigrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class UsersAndSubmissionOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Submissions");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Submissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_OwnerUserId",
                table: "Submissions",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Issuer_Subject",
                table: "Users",
                columns: new[] { "Issuer", "Subject" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Users_OwnerUserId",
                table: "Submissions",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Users_OwnerUserId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_OwnerUserId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Submissions");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Submissions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
