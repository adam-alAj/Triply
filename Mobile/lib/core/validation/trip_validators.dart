import '../../data/models/trip_creation_data.dart';

/// Client-side mirror of the backend's FluentValidation rules for trip
/// creation (Backend/Triply.Api/Modules/Trip/Validators/TripValidators.cs
/// and Modules/Destination/Validators/DestinationSuggestionValidator.cs).
/// Messages are worded the same way so a client-side rejection and a
/// server-side rejection never say two different things for the same rule.
///
/// Every method returns `null` when the value is valid, or an error string
/// otherwise — the same contract `AppTextField.errorText` and form
/// validators already use elsewhere in the app.
class TripValidators {
  TripValidators._();

  static String? travelerCount(int count) {
    if (count <= 0) return 'Traveler count must be greater than 0.';
    return null;
  }

  static String? dateRange(DateTime? start, DateTime? end) {
    if (start == null || end == null) return null;
    if (end.isBefore(start)) {
      return 'End date must be on or after start date.';
    }
    return null;
  }

  /// [required] mirrors the backend: BudgetAmount is required for
  /// BUDGET_FIRST, optional (but non-negative if given) otherwise.
  static String? budgetAmount(double? value, {required bool required}) {
    if (value == null) {
      return required ? 'Budget is required.' : null;
    }
    if (required && value <= 0) {
      return 'Budget must be greater than \$0.';
    }
    if (value < 0) {
      return 'Budget cannot be negative.';
    }
    return null;
  }

  /// Raw-text version for the budget entry dialogs, which work with a
  /// [TextEditingController] before a value even exists yet.
  static String? budgetInput(String rawText, {required bool required}) {
    final trimmed = rawText.trim();
    if (trimmed.isEmpty) {
      return required ? 'Enter a budget amount.' : null;
    }
    final value = double.tryParse(trimmed);
    if (value == null) return 'Enter a valid number.';
    return budgetAmount(value, required: required);
  }

  static String? destination(TripCreationData data) {
    if (data.planningMode == PlanningMode.destinationFirst &&
        data.destinationId == null) {
      return 'Destination is required.';
    }
    return null;
  }

  static String? interests(List<String> interests) {
    if (interests.isEmpty) {
      return 'At least one interest is required.';
    }
    return null;
  }

  /// Full-data pass run right before submission (Review screen's
  /// "Generate my trip") — the acceptance criteria's "before submission"
  /// gate. Returns every problem found, not just the first, so the user
  /// sees the whole list at once instead of fixing issues one at a time.
  static List<String> validateAll(TripCreationData data) {
    final errors = <String>[];

    final travelerError = travelerCount(data.travelers);
    if (travelerError != null) errors.add(travelerError);

    final dateError = dateRange(data.startDate, data.endDate);
    if (dateError != null) errors.add(dateError);

    final budgetError = budgetAmount(
      data.budget,
      required: data.planningMode == PlanningMode.budgetFirst,
    );
    if (budgetError != null) errors.add(budgetError);

    final destinationError = destination(data);
    if (destinationError != null) errors.add(destinationError);

    final interestsError = interests(data.interests);
    if (interestsError != null) errors.add(interestsError);

    return errors;
  }
}
