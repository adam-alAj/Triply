/// Mirrors the backend's seeded `InterestCategory` rows exactly
/// (Backend/Triply.Api/Data/ApplicationDbContext.cs — the 8-value list is
/// intentionally fixed per the SRS/Database Design docs, not curated
/// content), so it's safe to keep a static copy client-side instead of
/// requiring a lookup endpoint just for these 8 constants.
class InterestCategoryRef {
  InterestCategoryRef._();

  static const Map<String, int> idsByTitle = {
    'Nature': 1,
    'History': 2,
    'Food': 3,
    'Shopping': 4,
    'Adventure': 5,
    'Culture': 6,
    'Relaxation': 7,
    'Other': 8,
  };
}
