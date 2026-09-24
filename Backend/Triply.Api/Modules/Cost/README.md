# Cost Module

Calculates trip costs with normal backend code (not the AI).

- `GET /api/trips/{tripId}/cost-estimate`
- Groups the estimates by category (accommodation, transportation, food, activities, other). Categories with no cost are returned as `0.00`.
- Total = sum of categories. Accommodation = price × nights.
- One currency per trip.
- Every figure is marked `isEstimated: true`.
- The total is also saved on `Trip.TotalEstimatedCost`.

Files: `CostsController.cs`, `CostAggregationService.cs`, `Dtos/CostEstimateDtos.cs`.
