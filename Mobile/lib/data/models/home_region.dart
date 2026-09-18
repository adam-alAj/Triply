/// A featured destination card on Home's "Curated Regions" section.
/// Mirrors the subset of `GET /api/destinations`'s response the card needs —
/// no "badge"/"guides count" concept exists on the backend, so those are
/// deliberately not modeled here (the card shows the country instead).
class HomeRegion {
  const HomeRegion({
    required this.name,
    required this.country,
    required this.description,
    required this.imageAsset,
  });

  final String name;
  final String country;
  final String description;
  final String imageAsset;
}
