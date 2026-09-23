/// Result of `POST /api/trips/{id}/generate`.
///
/// [isOverBudget] is set by the Backend only for `DESTINATION_FIRST` trips whose
/// deterministic cost exceeds the trip budget (AI validation rules V-002 §5.3).
/// The itinerary is still generated and persisted — the flag is a signal to
/// surface to the user, not a failure. `BUDGET_FIRST` never sets it, because a
/// plan that does not fit the budget is rejected instead of returned.
class GenerationOutcome {
  const GenerationOutcome({this.isOverBudget = false});

  final bool isOverBudget;

  /// Missing or unrecognised field means "not flagged"; the Backend only ever
  /// sends `true` when it has actually computed an over-budget plan.
  factory GenerationOutcome.fromJson(Map<String, dynamic> json) =>
      GenerationOutcome(isOverBudget: json['isOverBudget'] == true);

  static const GenerationOutcome unknown = GenerationOutcome();
}
