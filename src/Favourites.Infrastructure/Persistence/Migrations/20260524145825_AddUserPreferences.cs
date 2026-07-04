using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Favourites.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Theme = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Density = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DefaultCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutoExtractTitle = table.Column<bool>(type: "bit", nullable: false),
                    ShowFavicon = table.Column<bool>(type: "bit", nullable: false),
                    OpenInNewTab = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmBeforeDelete = table.Column<bool>(type: "bit", nullable: false),
                    SuggestTagsAutomatically = table.Column<bool>(type: "bit", nullable: false),
                    ShowColorsOnTagChips = table.Column<bool>(type: "bit", nullable: false),
                    TagsDefaultSort = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CategoriesDefaultSort = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    WeeklySummaryEmail = table.Column<bool>(type: "bit", nullable: false),
                    SecurityAlerts = table.Column<bool>(type: "bit", nullable: false),
                    ProductUpdates = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Categories_DefaultCategoryId",
                        column: x => x.DefaultCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_DefaultCategoryId",
                table: "UserPreferences",
                column: "DefaultCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPreferences");
        }
    }
}
