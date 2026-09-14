using Triply.Api.Entities;

namespace Triply.Api.Data;

// Database Design §26 — fixed reference lists, seeded once via migration or startup.
public static class SeedData
{
    public static void Apply(ApplicationDbContext db)
    {
        if (!db.InterestCategories.Any())
        {
            db.InterestCategories.AddRange(
                new InterestCategory { Code = "NATURE", Label = "Nature" },
                new InterestCategory { Code = "HISTORY", Label = "History" },
                new InterestCategory { Code = "FOOD", Label = "Food" },
                new InterestCategory { Code = "SHOPPING", Label = "Shopping" },
                new InterestCategory { Code = "ADVENTURE", Label = "Adventure" },
                new InterestCategory { Code = "CULTURE", Label = "Culture" },
                new InterestCategory { Code = "RELAXATION", Label = "Relaxation" },
                new InterestCategory { Code = "OTHER", Label = "Other" }
            );
        }

        if (!db.CostCategories.Any())
        {
            db.CostCategories.AddRange(
                new CostCategory { Code = "ACCOMMODATION", Label = "Accommodation" },
                new CostCategory { Code = "TRANSPORTATION", Label = "Transportation" },
                new CostCategory { Code = "FOOD", Label = "Food" },
                new CostCategory { Code = "ACTIVITIES", Label = "Activities" },
                new CostCategory { Code = "OTHER", Label = "Other" }
            );
        }

        if (!db.PlaceCategories.Any())
        {
            db.PlaceCategories.AddRange(
                new PlaceCategory { Code = "ATTRACTION", Label = "Attraction" },
                new PlaceCategory { Code = "RESTAURANT", Label = "Restaurant" },
                new PlaceCategory { Code = "ACTIVITY", Label = "Activity" },
                new PlaceCategory { Code = "ACCOMMODATION", Label = "Accommodation" },
                new PlaceCategory { Code = "TRANSPORT", Label = "Transport" }
            );
        }

        if (!db.Currencies.Any())
        {
            db.Currencies.AddRange(
                new Currency { IsoCode = "USD", Symbol = "$" },
                new Currency { IsoCode = "JOD", Symbol = "JD" },
                new Currency { IsoCode = "EUR", Symbol = "€" }
            );
        }

        // Countries list: to be finalized with AI track per SRS D5 / DB-D4.
        // Add here once the supported-destinations decision is confirmed.

        db.SaveChanges();
    }
}
