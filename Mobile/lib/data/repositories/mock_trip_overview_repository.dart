import '../models/trip_overview_data.dart';
import 'trip_overview_repository.dart';

/// PENDING BACKEND: real data comes from `GET /api/trips/{id}` (already
/// implemented — see TripsController.GetById) for the trip/itinerary shape;
/// the richer cost-breakdown fields here (percent-of-total, itemized
/// lines, budget health, stay highlight) have no backend equivalent yet
/// and would need new response fields once this screen goes live.
class MockTripOverviewRepository implements TripOverviewRepository {
  @override
  Future<TripOverviewData> getTrip(String tripId) async {
    await Future.delayed(const Duration(milliseconds: 400));

    return TripOverviewData(
      id: tripId,
      tripTitle: 'Kyoto & Tokyo Discovery',
      regionLabel: 'Japan • Honshu Region',
      dateRangeLabel: 'Oct 14 – 21',
      totalDays: 7,
      travelerCount: 2,
      status: 'SAVED',
      totalEstimatedCostUsd: 2450,
      avgPerDayPerTravelerUsd: 175,
      isOnTarget: true,
      budgetHealth: const BudgetHealthData(
        targetCapUsd: 2500,
        spentUsd: 2450,
      ),
      stayHighlight: const StayHighlightData(
        imageAsset: 'assets/images/trip_creation/kyoto_tokyo.jpg',
        title: 'Kyoto Machiya Ryokan',
        subtitle: '4 Nights • Est. \$680',
      ),
      costCategories: const [
        CostCategoryEstimateData(
          code: 'ACCOMMODATION',
          label: 'Accommodation',
          amountUsd: 1120,
          percentOfTotal: 46,
          accuracy: CostAccuracy.estimated,
          contextLabel: '7 Nights',
          items: [
            CostLineItemData(
              label: '4 nights Kyoto Machiya Ryokan',
              amountUsd: 680,
            ),
            CostLineItemData(
              label: '3 nights Shinjuku Boutique Hotel',
              amountUsd: 440,
            ),
          ],
        ),
        CostCategoryEstimateData(
          code: 'TRANSPORTATION',
          label: 'Transportation',
          amountUsd: 480,
          percentOfTotal: 20,
          accuracy: CostAccuracy.estimated,
          contextLabel: '2 Travelers',
          items: [
            CostLineItemData(
              label: '7-Day Regional Shinkansen Pass',
              amountUsd: 320,
            ),
            CostLineItemData(
              label: 'Tokyo Metro & Kyoto Bus IC Cards',
              amountUsd: 160,
            ),
          ],
        ),
        CostCategoryEstimateData(
          code: 'FOOD',
          label: 'Food & Dining',
          amountUsd: 530,
          percentOfTotal: 21,
          accuracy: CostAccuracy.estimated,
          contextLabel: '~\$38/day pp',
          items: [
            CostLineItemData(
              label: 'Gion Kaiseki dinner (Kyoto)',
              amountUsd: 150,
            ),
            CostLineItemData(
              label: 'Pontocho street food & ramen',
              amountUsd: 180,
            ),
            CostLineItemData(
              label: 'Daily breakfasts, matcha & teas',
              amountUsd: 200,
            ),
          ],
        ),
        CostCategoryEstimateData(
          code: 'ACTIVITIES',
          label: 'Activities & Entry',
          amountUsd: 220,
          percentOfTotal: 9,
          accuracy: CostAccuracy.verifiedAiMatched,
          items: [
            CostLineItemData(label: 'Fushimi Inari guided tour', amountUsd: 30),
            CostLineItemData(
              label: 'Kiyomizu-dera tea ceremony',
              amountUsd: 50,
            ),
            CostLineItemData(
              label: 'Shibuya Sky observatory tickets',
              amountUsd: 40,
            ),
            CostLineItemData(
              label: 'Mori Art & National Museum passes',
              amountUsd: 100,
            ),
          ],
        ),
        CostCategoryEstimateData(
          code: 'OTHER',
          label: 'Reserve / Contingency',
          amountUsd: 100,
          percentOfTotal: 4,
          accuracy: CostAccuracy.estimated,
          contextLabel: 'Safety Net',
          items: [
            CostLineItemData(
              label: 'Luggage courier (Takkyubin) & sim',
              amountUsd: 100,
            ),
          ],
        ),
      ],
      days: [
        ItineraryDayData(
          dayNumber: 1,
          dateLabel: 'Oct 14',
          items: const [
            ItineraryItemData(
              timeSlot: 'MORNING',
              orderIndex: 1,
              placeName: 'Old City Walking Tour',
              subtitle: 'Historic quarter · 2.5 hrs',
              estimatedCostLabel: 'Est. \$25',
              isAiGenerated: true,
              tipText: 'Wear comfortable shoes',
            ),
            ItineraryItemData(
              timeSlot: 'AFTERNOON',
              orderIndex: 1,
              placeName: 'Local Market Lunch',
              subtitle: 'Traditional cuisine',
              estimatedCostLabel: 'Est. \$15',
              isAiGenerated: true,
            ),
            ItineraryItemData(
              timeSlot: 'EVENING',
              orderIndex: 1,
              placeName: 'Sunset at the Ramparts',
              subtitle: 'Scenic viewpoint',
              estimatedCostLabel: 'Est. \$0',
              isAiGenerated: true,
            ),
          ],
        ),
        ItineraryDayData(
          dayNumber: 2,
          dateLabel: 'Oct 15',
          items: const [
            ItineraryItemData(
              timeSlot: 'MORNING',
              orderIndex: 1,
              placeName: 'Museum Visit',
              subtitle: 'Art & history · 3 hrs',
              estimatedCostLabel: 'Est. \$18',
              isAiGenerated: false,
              tipText: 'Edited by you',
            ),
            ItineraryItemData(
              timeSlot: 'AFTERNOON',
              orderIndex: 1,
              placeName: 'Botanical Gardens',
              subtitle: 'Nature & relaxation',
              estimatedCostLabel: 'Est. \$10',
              isAiGenerated: true,
            ),
          ],
        ),
        ItineraryDayData(
          dayNumber: 3,
          dateLabel: 'Oct 16',
          items: const [
            ItineraryItemData(
              timeSlot: 'MORNING',
              orderIndex: 1,
              placeName: 'Day Trip Departure',
              subtitle: 'Coastal excursion',
              estimatedCostLabel: 'Est. \$60',
              isAiGenerated: true,
            ),
          ],
        ),
      ],
    );
  }
}
