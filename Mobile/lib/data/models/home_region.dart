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
    this.imageUrl,
  });

  final String name;
  final String country;
  final String description;

  /// Local-asset fallback, used when [imageUrl] is null or fails to load.
  final String imageAsset;

  /// Real cover photo from `GET /api/destinations/assets`, keyed by
  /// destination name — preferred over [imageAsset] when present.
  final String? imageUrl;
}
