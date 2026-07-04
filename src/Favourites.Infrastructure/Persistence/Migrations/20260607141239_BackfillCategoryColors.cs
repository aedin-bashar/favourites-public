using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Favourites.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCategoryColors : Migration
    {
        private static readonly string[] Palette =
        [
            "#0d6efd", "#6610f2", "#d63384", "#dc3545",
            "#fd7e14", "#198754", "#0dcaf0", "#6f42c1",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Assign each existing category a deterministic palette color based on
            // its name using SQL Server CHECKSUM() so the result matches the
            // domain logic in Category.Create().
            for (int i = 0; i < Palette.Length; i++)
            {
                migrationBuilder.Sql($"""
                    UPDATE Categories
                    SET Color = '{Palette[i]}'
                    WHERE ABS(CHECKSUM(Name)) % 8 = {i}
                      AND Color = '#0d6efd';
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Categories SET Color = '#0d6efd';");
        }
    }
}
