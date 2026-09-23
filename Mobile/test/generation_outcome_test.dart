import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/data/models/generation_outcome.dart';

void main() {
  group('GenerationOutcome.fromJson', () {
    test('reads a true over-budget flag', () {
      final outcome = GenerationOutcome.fromJson(const {'isOverBudget': true});

      expect(outcome.isOverBudget, isTrue);
    });

    test('reads a false over-budget flag', () {
      final outcome = GenerationOutcome.fromJson(const {'isOverBudget': false});

      expect(outcome.isOverBudget, isFalse);
    });

    test('treats a missing field as not flagged', () {
      final outcome = GenerationOutcome.fromJson(const {'tripVersion': 3});

      expect(outcome.isOverBudget, isFalse);
    });

    test('only a real boolean true flags the trip', () {
      // The Backend sends a JSON boolean; a stringy "true" must not be trusted.
      final outcome = GenerationOutcome.fromJson(const {'isOverBudget': 'true'});

      expect(outcome.isOverBudget, isFalse);
    });

    test('unknown is not flagged', () {
      expect(GenerationOutcome.unknown.isOverBudget, isFalse);
    });
  });
}
