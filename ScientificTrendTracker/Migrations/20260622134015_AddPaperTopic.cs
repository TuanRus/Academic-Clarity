using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScientificTrendTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperTopic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "ResearchPapers",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Topic",
                table: "ResearchPapers");
        }
    }
}
