using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triply.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceInterest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaceInterests",
                columns: table => new
                {
                    PlaceId = table.Column<long>(type: "bigint", nullable: false),
                    InterestCategoryId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaceInterests", x => new { x.PlaceId, x.InterestCategoryId });
                    table.ForeignKey(
                        name: "FK_PlaceInterests_InterestCategories_InterestCategoryId",
                        column: x => x.InterestCategoryId,
                        principalTable: "InterestCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlaceInterests_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaceInterests_InterestCategoryId",
                table: "PlaceInterests",
                column: "InterestCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaceInterests");
        }
    }
}
