import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/presentation/widgets/app_bottom_sheet.dart';
import 'package:triply_project/presentation/widgets/app_bottom_navigation.dart';
import 'package:triply_project/presentation/widgets/app_scaffold.dart';
import 'package:triply_project/presentation/widgets/app_text_field.dart';
import 'package:triply_project/presentation/widgets/auth/auth_info_card.dart';
import 'package:triply_project/presentation/widgets/auth/social_auth_button.dart';
import 'package:triply_project/presentation/widgets/interest_chip.dart';
import 'package:triply_project/presentation/widgets/itinerary_item_card.dart';
import 'package:triply_project/presentation/widgets/loading_skeleton.dart';
import 'package:triply_project/presentation/widgets/primary_button.dart';
import 'package:triply_project/presentation/widgets/secondary_button.dart';
import 'package:triply_project/presentation/widgets/selection_card.dart';
import 'package:triply_project/presentation/widgets/status_badge.dart';
import 'package:triply_project/presentation/widgets/trip_card.dart';

void main() {
  testWidgets('PrimaryButton invokes callback and supports loading state', (
    tester,
  ) async {
    var taps = 0;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: PrimaryButton(label: 'Continue', onPressed: () => taps++),
      ),
    ));

    expect(find.text('Continue'), findsOneWidget);
    await tester.tap(find.byType(ElevatedButton));
    expect(taps, 1);

    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        body: PrimaryButton(
          label: 'Continue',
          onPressed: null,
          isLoading: true,
        ),
      ),
    ));
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(tester.widget<ElevatedButton>(find.byType(ElevatedButton)).onPressed,
        isNull);
  });

  testWidgets('SecondaryButton renders an optional icon and invokes callback',
      (tester) async {
    var taps = 0;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: SecondaryButton(
          label: 'Back',
          icon: Icons.arrow_back,
          onPressed: () => taps++,
        ),
      ),
    ));

    expect(find.text('Back'), findsOneWidget);
    expect(find.byIcon(Icons.arrow_back), findsOneWidget);
    await tester.tap(find.byType(ElevatedButton));
    expect(taps, 1);
  });

  testWidgets('AppTextField displays errors and toggles password visibility',
      (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        body: AppTextField(
          label: 'Password',
          obscureText: true,
          errorText: 'Required',
        ),
      ),
    ));

    expect(find.text('Password'), findsOneWidget);
    expect(find.text('Required'), findsOneWidget);
    expect(tester.widget<TextField>(find.byType(TextField)).obscureText, isTrue);
    await tester.tap(find.byIcon(Icons.visibility_outlined));
    await tester.pump();
    expect(tester.widget<TextField>(find.byType(TextField)).obscureText, isFalse);
  });

  testWidgets('SelectionCard displays selected affordance and handles taps', (
    tester,
  ) async {
    var taps = 0;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: SelectionCard(
          title: 'Budget first',
          description: 'Plan around a budget',
          icon: Icons.savings,
          selected: true,
          onTap: () => taps++,
        ),
      ),
    ));

    expect(find.text('Budget first'), findsOneWidget);
    expect(find.byIcon(Icons.check_circle), findsOneWidget);
    await tester.tap(find.text('Budget first'));
    expect(taps, 1);
  });

  testWidgets('InterestChip shows selection and invokes callback', (tester) async {
    var taps = 0;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: InterestChip(
          label: 'Museums',
          selected: true,
          icon: Icons.museum,
          onTap: () => taps++,
        ),
      ),
    ));

    expect(find.text('Museums'), findsOneWidget);
    expect(find.byIcon(Icons.check), findsOneWidget);
    await tester.tap(find.text('Museums'));
    expect(taps, 1);
  });

  testWidgets('AppScaffold displays title, body, and actions', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: AppScaffold(
        title: 'My trips',
        actions: const [Icon(Icons.search)],
        body: const Text('Trip content'),
      ),
    ));

    expect(find.text('My trips'), findsOneWidget);
    expect(find.text('Trip content'), findsOneWidget);
    expect(find.byIcon(Icons.search), findsOneWidget);
  });

  testWidgets('showAppBottomSheet presents its title and content',
      (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: Builder(
        builder: (context) => Scaffold(
          body: TextButton(
            onPressed: () => showAppBottomSheet<void>(
              context: context,
              title: 'Options',
              child: const Text('Sheet content'),
            ),
            child: const Text('Open'),
          ),
        ),
      ),
    ));

    await tester.tap(find.text('Open'));
    await tester.pumpAndSettle();
    expect(find.text('Options'), findsOneWidget);
    expect(find.text('Sheet content'), findsOneWidget);
  });

  testWidgets('AppBottomNavigation renders all root destinations',
      (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        bottomNavigationBar:
            AppBottomNavigation(selected: AppNavTab.myTrips),
      ),
    ));

    expect(find.text('Home'), findsOneWidget);
    expect(find.text('My Trips'), findsOneWidget);
    expect(find.text('Profile'), findsOneWidget);
    expect(find.byIcon(Icons.add), findsOneWidget);
  });

  testWidgets('LoadingSkeleton renders the requested size', (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        body: LoadingSkeleton(width: 120, height: 24, borderRadius: 12),
      ),
    ));

    expect(tester.getSize(find.byType(LoadingSkeleton)), const Size(120, 24));
  });

  testWidgets('TripCard presents destination, dates, status, and estimate',
      (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        body: TripCard(
          destinationName: 'Amman',
          dateRangeLabel: 'Jun 1–4',
          statusLabel: 'Planned',
          statusVariant: StatusBadgeVariant.neutral,
          estimatedCostLabel: 'JOD 200',
        ),
      ),
    ));

    expect(find.text('Amman'), findsOneWidget);
    expect(find.text('Jun 1–4'), findsOneWidget);
    expect(find.text('Planned'), findsOneWidget);
    expect(find.text('JOD 200'), findsOneWidget);
  });

  testWidgets('ItineraryItemCard shows item details and edit actions',
      (tester) async {
    var edits = 0;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: ItineraryItemCard(
          time: '09:00 AM',
          title: 'Citadel',
          subtitle: 'Historic site',
          estimatedCostLabel: 'Est. JOD 5',
          tipText: 'Arrive early',
          onEdit: () => edits++,
        ),
      ),
    ));

    expect(find.text('Citadel'), findsOneWidget);
    expect(find.text('Historic site'), findsOneWidget);
    expect(find.text('Arrive early'), findsOneWidget);
    await tester.tap(find.byIcon(Icons.edit_outlined));
    expect(edits, 1);
  });

  testWidgets('AuthInfoCard and SocialAuthButton show content and handle taps',
      (tester) async {
    var taps = 0;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: Column(
          children: [
            const AuthInfoCard(text: 'Your trips are private'),
            Row(
              children: [
                SocialAuthButton(
                  label: 'Google',
                  icon: const Icon(Icons.login),
                  onPressed: () => taps++,
                ),
              ],
            ),
          ],
        ),
      ),
    ));

    expect(find.text('Your trips are private'), findsOneWidget);
    await tester.tap(find.text('Google'));
    expect(taps, 1);
  });
}
