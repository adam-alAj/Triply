using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triply.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTripSchemaCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Trip_PlanningMode",
                table: "Trips",
                sql: "[PlanningMode] IN ('DESTINATION_FIRST','BUDGET_FIRST')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trip_Status",
                table: "Trips",
                sql: "[Status] IN ('DRAFT','GENERATING','GENERATED','MODIFIED','SAVED','ARCHIVED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trip_TravelerCount",
                table: "Trips",
                sql: "[TravelerCount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItineraryItem_TimeSlot",
                table: "ItineraryItems",
                sql: "[TimeSlot] IN ('MORNING','AFTERNOON','EVENING')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItineraryDay_DayNumber",
                table: "ItineraryDays",
                sql: "[DayNumber] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CostEstimate_Amount",
                table: "CostEstimates",
                sql: "[Amount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AIGeneration_Status",
                table: "AIGenerations",
                sql: "[Status] IN ('PENDING','SUCCEEDED','FAILED_VALIDATION','FAILED_ERROR')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trip_PlanningMode",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trip_Status",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trip_TravelerCount",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItineraryItem_TimeSlot",
                table: "ItineraryItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItineraryDay_DayNumber",
                table: "ItineraryDays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CostEstimate_Amount",
                table: "CostEstimates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AIGeneration_Status",
                table: "AIGenerations");
        }
    }
}
