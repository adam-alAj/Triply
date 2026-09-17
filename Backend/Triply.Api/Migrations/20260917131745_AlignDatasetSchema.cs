using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triply.Api.Migrations
{
    /// <inheritdoc />
    public partial class AlignDatasetSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Places",
                type: "varchar(200)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Longitude",
                table: "Destinations",
                type: "decimal(9,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Latitude",
                table: "Destinations",
                type: "decimal(9,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Places_DestinationId_Name",
                table: "Places",
                columns: new[] { "DestinationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Places_DestinationId_Name",
                table: "Places");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Places",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Longitude",
                table: "Destinations",
                type: "decimal(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Latitude",
                table: "Destinations",
                type: "decimal(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldNullable: true);
        }
    }
}
