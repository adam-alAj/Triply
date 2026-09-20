using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Triply.Api.Migrations
{
    public partial class RemoveBackendStaticSeed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the legacy Backend static reference dataset on a fresh/empty
            // reference database. The AI dataset seeder is now the source of truth.
            // Existing databases that already contain Places/Trips are left intact;
            // their AI-curated rows must not be deleted by this migration.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Places])
   AND NOT EXISTS (SELECT 1 FROM [Trips])
   AND NOT EXISTS (SELECT 1 FROM [UserPreferences])
BEGIN
    DELETE FROM [Destinations];
    DELETE FROM [Currencies];
    DELETE FROM [Countries];
    DELETE FROM [PlaceCategories];
    DELETE FROM [CostCategories];
    DELETE FROM [InterestCategories];

    -- DELETE does not reset IDENTITY on its own. Without this, the next
    -- row inserted into these tables (by CustomWebApplicationFactory's
    -- test seeding, or by the AI dataset seeder) would continue from
    -- wherever the counter was left by the now-removed HasData rows,
    -- instead of restarting at 1 as every consumer of a fresh database
    -- (tests included) expects.
    DBCC CHECKIDENT ('[Destinations]', RESEED, 0);
    DBCC CHECKIDENT ('[Currencies]', RESEED, 0);
    DBCC CHECKIDENT ('[Countries]', RESEED, 0);
    DBCC CHECKIDENT ('[PlaceCategories]', RESEED, 0);
    DBCC CHECKIDENT ('[CostCategories]', RESEED, 0);
    DBCC CHECKIDENT ('[InterestCategories]', RESEED, 0);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Static reference data is owned by the AI dataset track.
            // Rolling this migration back does not restore application seed data.
        }
    }
}
