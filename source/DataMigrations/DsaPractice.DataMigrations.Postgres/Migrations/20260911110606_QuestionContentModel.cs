using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DsaPractice.DataMigrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class QuestionContentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestCases_QuestionId",
                table: "TestCases");

            migrationBuilder.AddColumn<int>(
                name: "Ordinal",
                table: "TestCases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Submissions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Verdict",
                table: "Submissions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Questions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Difficulty",
                table: "Questions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "MemoryLimitMb",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Questions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<List<string>>(
                name: "Tags",
                table: "Questions",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitMs",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_QuestionId_Ordinal",
                table: "TestCases",
                columns: new[] { "QuestionId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_QuestionId",
                table: "Submissions",
                column: "QuestionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Submissions_Verdict_OnlyWhenCompleted",
                table: "Submissions",
                sql: "(\"Status\" = 'Completed') = (\"Verdict\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Slug",
                table: "Questions",
                column: "Slug",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_MemoryLimitMb_Positive",
                table: "Questions",
                sql: "\"MemoryLimitMb\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Slug_Format",
                table: "Questions",
                sql: "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_TimeLimitMs_Positive",
                table: "Questions",
                sql: "\"TimeLimitMs\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Questions_QuestionId",
                table: "Submissions",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Questions_QuestionId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_QuestionId_Ordinal",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_QuestionId",
                table: "Submissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Submissions_Verdict_OnlyWhenCompleted",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_Slug",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_MemoryLimitMb_Positive",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_Slug_Format",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_TimeLimitMs_Positive",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Ordinal",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "Verdict",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "MemoryLimitMb",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TimeLimitMs",
                table: "Questions");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Submissions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Questions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Difficulty",
                table: "Questions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_QuestionId",
                table: "TestCases",
                column: "QuestionId");
        }
    }
}
