import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:triply_project/presentation/screens/trip_creation/planning_mode_screen.dart';
import 'package:triply_project/presentation/screens/trip_creation/review_screen.dart';
import 'package:triply_project/presentation/screens/trip_creation/trip_details_screen.dart';

import '../../../core/network/api_client.dart';
import '../../../data/repositories/api_trip_creation_repository.dart';
import '../../providers/trip_creation_provider.dart';
import 'budget_destination_screen.dart';
import 'destination_suggestions_screen.dart';
import 'generating_screen.dart';
import 'interests_screen.dart';

class TripCreationWizard extends StatelessWidget {
  const TripCreationWizard({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => TripCreationProvider(
        repository: ApiTripCreationRepository(
          apiClient: context.read<ApiClient>(),
        ),
      ),
      child: const _TripCreationWizardView(),
    );
  }
}

class _TripCreationWizardView extends StatelessWidget {
  const _TripCreationWizardView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<TripCreationProvider>();
    final step = provider.currentStep;

    if (step == 0) return const PlanningModeScreen();
    if (step == 1) return const BudgetDestinationScreen();
    if (step == provider.interestsStepIndex) return const InterestsScreen();
    if (step == provider.suggestionsStepIndex) {
      return const DestinationSuggestionsScreen();
    }
    if (step == provider.tripDetailsStepIndex) return const TripDetailsScreen();
    if (step == 5) return const ReviewScreen();
    if (step == 6) return const GeneratingScreen();

    return const PlanningModeScreen();
  }
}