import 'package:flutter/material.dart' show Icons;

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
      version: 1,
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
              id: 'mock-item-1',
              placeId: 1,
              timeSlot: 'MORNING',
              orderIndex: 1,
              placeName: 'Tenryu-ji Bamboo Sanctuary & Zen Gardens',
              subtitle: 'Arashiyama District · 2 hrs',
              estimatedCostLabel: 'Est. \$12',
              isAiGenerated: true,
              tipText: 'Arrive early to beat the crowds',
              heroImageUrl:
                  'https://images.unsplash.com/photo-1522383225653-ed111181a951?w=800&q=80',
              locationLabel: 'Arashiyama District • West Kyoto',
              startTimeMinutes: 9 * 60 + 30,
              durationMinutes: 120,
              priceUsdLabel: '~\$12',
              priceLocalLabel: '¥1,800',
              priceContextLabel: 'Verified Entry Fee (Gardens + Hodo)',
              heroBadges: [
                HeroBadgeData(icon: Icons.eco, label: 'UNESCO Sanctuary'),
                HeroBadgeData(icon: Icons.verified, label: 'Official Partner'),
              ],
              crowdCadence: CrowdCadenceData(
                level: CrowdCadenceLevel.low,
                moodLabel: 'SERENE',
                currentTimeLabel: '09:30 AM (Current: 18% capacity)',
                currentCapacityPercent: 18,
                peakTimeLabel: '12:00 PM Peak (85%)',
                peakCapacityPercent: 85,
              ),
            ),
            ItineraryItemData(
              id: 'mock-item-2',
              placeId: 2,
              timeSlot: 'AFTERNOON',
              orderIndex: 1,
              placeName: 'Local Market Lunch',
              subtitle: 'Traditional cuisine',
              estimatedCostLabel: 'Est. \$15',
              isAiGenerated: true,
            ),
            ItineraryItemData(
              id: 'mock-item-3',
              placeId: 3,
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
              id: 'mock-item-4',
              placeId: 4,
              timeSlot: 'MORNING',
              orderIndex: 1,
              placeName: 'Museum Visit',
              subtitle: 'Art & history · 3 hrs',
              estimatedCostLabel: 'Est. \$18',
              isAiGenerated: false,
              tipText: 'Edited by you',
              startTimeMinutes: 9 * 60,
              durationMinutes: 180,
            ),
            ItineraryItemData(
              id: 'mock-item-5',
              placeId: 5,
              timeSlot: 'AFTERNOON',
              orderIndex: 1,
              placeName: 'Traditional Kaiseki in Gion',
              subtitle: 'Gion District',
              estimatedCostLabel: 'Est. \$45',
              isAiGenerated: false,
              locationLabel: 'Gion District • Kyoto',
              startTimeMinutes: 12 * 60 + 30,
              durationMinutes: 90,
            ),
            ItineraryItemData(
              id: 'mock-item-6',
              placeId: 6,
              timeSlot: 'AFTERNOON',
              orderIndex: 2,
              placeName: 'Botanical Gardens',
              subtitle: 'Nature & relaxation',
              estimatedCostLabel: 'Est. \$10',
              isAiGenerated: true,
              startTimeMinutes: 15 * 60,
              durationMinutes: 90,
            ),
            ItineraryItemData(
              id: 'mock-item-7',
              placeId: 7,
              timeSlot: 'EVENING',
              orderIndex: 1,
              placeName: 'Gion Evening Stroll',
              subtitle: 'Lantern-lit streets',
              estimatedCostLabel: 'Est. \$0',
              isAiGenerated: true,
              startTimeMinutes: 19 * 60,
              durationMinutes: 60,
            ),
          ],
        ),
        ItineraryDayData(
          dayNumber: 3,
          dateLabel: 'Oct 16',
          items: const [
            ItineraryItemData(
              id: 'mock-item-8',
              placeId: 8,
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
