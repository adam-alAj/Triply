using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triply.Api.Migrations;

public partial class AddUserPreferencesAndTripMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Title",
            table: "Trips",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CoverImageUrl",
            table: "Trips",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "UserPreferences",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PreferredCurrencyId = table.Column<long>(type: "bigint", nullable: true),
                DistanceUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Pacing = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                table.ForeignKey(
                    name: "FK_UserPreferences_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserPreferences_Currencies_PreferredCurrencyId",
                    column: x => x.PreferredCurrencyId,
                    principalTable: "Currencies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserPreferences_PreferredCurrencyId",
            table: "UserPreferences",
            column: "PreferredCurrencyId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserPreferences");

        migrationBuilder.DropColumn(
            name: "CoverImageUrl",
            table: "Trips");

        migrationBuilder.DropColumn(
            name: "Title",
            table: "Trips");
    }
}
