using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Triply.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedReferenceCategoriesViaHasData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CostCategories",
                columns: new[] { "Id", "Code", "Label" },
                values: new object[,]
                {
                    { 1L, "ACCOMMODATION", "Accommodation" },
                    { 2L, "TRANSPORTATION", "Transportation" },
                    { 3L, "FOOD", "Food" },
                    { 4L, "ACTIVITIES", "Activities" },
                    { 5L, "OTHER", "Other" }
                });

            migrationBuilder.InsertData(
                table: "InterestCategories",
                columns: new[] { "Id", "Code", "Label" },
                values: new object[,]
                {
                    { 1L, "NATURE", "Nature" },
                    { 2L, "HISTORY", "History" },
                    { 3L, "FOOD", "Food" },
                    { 4L, "SHOPPING", "Shopping" },
                    { 5L, "ADVENTURE", "Adventure" },
                    { 6L, "CULTURE", "Culture" },
                    { 7L, "RELAXATION", "Relaxation" },
                    { 8L, "OTHER", "Other" }
                });

            migrationBuilder.InsertData(
                table: "PlaceCategories",
                columns: new[] { "Id", "Code", "Label" },
                values: new object[,]
                {
                    { 1L, "ATTRACTION", "Attraction" },
                    { 2L, "RESTAURANT", "Restaurant" },
                    { 3L, "ACTIVITY", "Activity" },
                    { 4L, "ACCOMMODATION", "Accommodation" },
                    { 5L, "TRANSPORT", "Transport" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CostCategories",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "CostCategories",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "CostCategories",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "CostCategories",
                keyColumn: "Id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "CostCategories",
                keyColumn: "Id",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                table: "InterestCategories",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "InterestCategories",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "InterestCategories",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "InterestCategories",
                keyColumn: "Id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "InterestCategories",
                keyColumn: "Id",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                table: "InterestCategories",
                keyColumn: "Id",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                table: "InterestCategories",
                keyColumn: "Id",
                keyValue: 7L);

            migrationBuilder.DeleteData(
                table: "InterestCategories",
                keyColumn: "Id",
                keyValue: 8L);

            migrationBuilder.DeleteData(
                table: "PlaceCategories",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "PlaceCategories",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "PlaceCategories",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "PlaceCategories",
                keyColumn: "Id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "PlaceCategories",
                keyColumn: "Id",
                keyValue: 5L);
        }
    }
}
