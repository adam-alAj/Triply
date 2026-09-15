using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Triply.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedCountriesCurrenciesDestinations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "Id", "IsoCode", "Name" },
                values: new object[,]
                {
                    { 1L, "PS", "Palestine" },
                    { 2L, "JO", "Jordan" }
                });

            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Id", "IsoCode", "Symbol" },
                values: new object[,]
                {
                    { 1L, "USD", "$" },
                    { 2L, "JOD", "JD" }
                });

            migrationBuilder.InsertData(
                table: "Destinations",
                columns: new[] { "Id", "CountryId", "Description", "IsSupported", "Latitude", "Longitude", "Name" },
                values: new object[,]
                {
                    { 1L, 1L, "Historic and cultural destination", true, 31.7683m, 35.2137m, "Jerusalem" },
                    { 2L, 2L, "Capital city of Jordan", true, 31.9539m, 35.9106m, "Amman" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 2L);
        }
    }
}
