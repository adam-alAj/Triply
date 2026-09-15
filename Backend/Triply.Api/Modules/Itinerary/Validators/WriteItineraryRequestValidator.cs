using FluentValidation;
using Triply.Api.Modules.Itinerary.Dtos;

namespace Triply.Api.Modules.Itinerary.Validators;

public sealed class WriteItineraryRequestValidator : AbstractValidator<WriteItineraryRequest>
{
    public WriteItineraryRequestValidator()
    {
        RuleFor(x => x.Days)
            .NotEmpty()
            .WithMessage("At least one itinerary day is required.");

        RuleFor(x => x.Days)
            .Must(days => days.Select(x => x.DayNumber).Distinct().Count() == days.Count)
            .WithMessage("DayNumber values must be unique.");

        RuleForEach(x => x.Days)
            .SetValidator(new WriteItineraryDayRequestValidator());
    }
}

public sealed class WriteItineraryDayRequestValidator : AbstractValidator<WriteItineraryDayRequest>
{
    public WriteItineraryDayRequestValidator()
    {
        RuleFor(x => x.DayNumber)
            .GreaterThan(0)
            .WithMessage("DayNumber must be greater than 0.");

        RuleFor(x => x.Items)
            .Must(items => items
                .GroupBy(item => new { item.TimeSlot, item.OrderIndex })
                .All(group => group.Count() == 1))
            .WithMessage("TimeSlot and OrderIndex combinations must be unique within a day.");

        RuleForEach(x => x.Items)
            .SetValidator(new WriteItineraryItemRequestValidator());
    }
}

public sealed class WriteItineraryItemRequestValidator : AbstractValidator<WriteItineraryItemRequest>
{
    private static readonly string[] AllowedTimeSlots =
        ["MORNING", "AFTERNOON", "EVENING"];

    public WriteItineraryItemRequestValidator()
    {
        RuleFor(x => x.PlaceId)
            .GreaterThan(0)
            .WithMessage("PlaceId must be greater than 0.");

        RuleFor(x => x.TimeSlot)
            .NotEmpty()
            .Must(timeSlot =>
                AllowedTimeSlots.Contains(
                    timeSlot,
                    StringComparer.OrdinalIgnoreCase))
            .WithMessage("TimeSlot must be MORNING, AFTERNOON, or EVENING.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0)
            .WithMessage("OrderIndex cannot be negative.");

        RuleFor(x => x.EstimatedCost)
            .GreaterThanOrEqualTo(0)
            .WithMessage("EstimatedCost cannot be negative.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);
    }
}