using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triply.Api.Migrations
{
    /// <inheritdoc />
    public partial class AlignReferenceSeedWithAIDataset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the legacy Destination rows introduced by
            // SeedCountriesCurrenciesDestinations. Destination/Place data is
            // now owned by AI/01-Dataset/curated-data and the dataset seeder.
            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 1L);

            // Keep stable reference data aligned with the curated dataset.
            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "IsoCode", "Name" },
                values: new object[] { "FR", "France" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "IsoCode", "Name" },
                values: new object[] { "JO", "Jordan" });

            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "Id", "IsoCode", "Name" },
                values: new object[] { 3L, "US", "United States" });

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "IsoCode", "Symbol" },
                values: new object[] { "EUR", "€" });

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "IsoCode", "Symbol" },
                values: new object[] { "JOD", "د.أ" });

            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Id", "IsoCode", "Symbol" },
                values: new object[] { 3L, "USD", "$" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "IsoCode", "Symbol" },
                values: new object[] { "JOD", "JD" });

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "IsoCode", "Symbol" },
                values: new object[] { "USD", "$" });

            migrationBuilder.DeleteData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "IsoCode", "Name" },
                values: new object[] { "JO", "Jordan" });

            migrationBuilder.UpdateData(
                table: "Countries",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "IsoCode", "Name" },
                values: new object[] { "PS", "Palestine" });

            migrationBuilder.InsertData(
                table: "Destinations",
                columns: new[] { "Id", "CountryId", "Description", "IsSupported", "Latitude", "Longitude", "Name" },
                values: new object[,]
                {
                    { 1L, 1L, "Historic and cultural destination", true, 31.7683m, 35.2137m, "Jerusalem" },
                    { 2L, 2L, "Capital city of Jordan", true, 31.9539m, 35.9106m, "Amman" }
                });
        }
    }
}
