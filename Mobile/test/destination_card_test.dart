import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:triply_project/core/theme/app_colors.dart';
import 'package:triply_project/presentation/widgets/destination_card.dart';

import 'test_helpers.dart';

void main() {
  group('DestinationCard', () {
    testWidgets('renders the destination name', (tester) async {
      await pumpApp(
        tester,
        const DestinationCard(
          name: 'Lisbon',
          supportingAttribute: 'From \$450',
        ),
      );

      expect(find.text('Lisbon'), findsOneWidget);
    });

    testWidgets('renders the supporting attribute', (tester) async {
      await pumpApp(
        tester,
        const DestinationCard(
          name: 'Lisbon',
          supportingAttribute: 'From \$450',
        ),
      );

      expect(find.text('From \$450'), findsOneWidget);
    });

    testWidgets('invokes onTap when tapped', (tester) async {
      var tapCount = 0;
      await pumpApp(
        tester,
        DestinationCard(
          name: 'Lisbon',
          supportingAttribute: 'From \$450',
          onTap: () => tapCount++,
        ),
      );

      await tester.tap(find.byType(DestinationCard));
      await tester.pump();

      expect(tapCount, 1);
    });

    testWidgets(
      'does not throw when onTap is not provided and the card is tapped',
      (tester) async {
        await pumpApp(
          tester,
          const DestinationCard(
            name: 'Lisbon',
            supportingAttribute: 'From \$450',
          ),
        );

        await tester.tap(find.byType(DestinationCard));
        await tester.pump();

        expect(find.text('Lisbon'), findsOneWidget);
      },
    );

    testWidgets(
      'shows an image placeholder container when no imageUrl is given',
      (tester) async {
        await pumpApp(
          tester,
          const DestinationCard(
            name: 'Lisbon',
            supportingAttribute: 'From \$450',
          ),
        );

        expect(find.byType(Image), findsNothing);
      },
    );

    testWidgets('renders a network Image when imageUrl is given', (
      tester,
    ) async {
      const imageUrl = 'https://example.com/lisbon.jpg';

      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: DestinationCard(
              name: 'Lisbon',
              supportingAttribute: 'From \$450',
              imageUrl: imageUrl,
            ),
          ),
        ),
      );

      final imageFinder = find.byType(Image);

      expect(imageFinder, findsOneWidget);

      final image = tester.widget<Image>(imageFinder);

      expect(image.image, isA<NetworkImage>());

      expect((image.image as NetworkImage).url, equals(imageUrl));
    });

    testWidgets('shows a colored border when selected is true', (tester) async {
      await pumpApp(
        tester,
        const DestinationCard(
          name: 'Lisbon',
          supportingAttribute: 'From \$450',
          selected: true,
        ),
      );

      final container = tester.widget<Container>(find.byType(Container).first);
      final decoration = container.decoration as BoxDecoration;
      expect(decoration.border, isNotNull);
      expect(decoration.border!.top.color, AppColors.primary);
    });

    testWidgets('has no border when selected is false', (tester) async {
      await pumpApp(
        tester,
        const DestinationCard(
          name: 'Lisbon',
          supportingAttribute: 'From \$450',
          selected: false,
        ),
      );

      final container = tester.widget<Container>(find.byType(Container).first);
      final decoration = container.decoration as BoxDecoration;
      expect(decoration.border, isNull);
    });
  });
}
