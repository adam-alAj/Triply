using FluentValidation;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Modules.Trip.Validators;

public class CreateTripRequestValidator : AbstractValidator<CreateTripRequest>
{
    public CreateTripRequestValidator()
    {
        RuleFor(x => x.PlanningMode)
            .NotEmpty()
            .Must(mode =>
                mode == "DESTINATION_FIRST" ||
                mode == "BUDGET_FIRST")
            .WithMessage(
                "PlanningMode must be DESTINATION_FIRST or BUDGET_FIRST.");

        RuleFor(x => x.TravelerCount)
            .GreaterThan(0)
            .WithMessage("TravelerCount must be greater than 0.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("EndDate must be on or after StartDate.");

        RuleFor(x => x.BudgetAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.BudgetAmount.HasValue)
            .WithMessage("BudgetAmount cannot be negative.");

        RuleFor(x => x.DestinationId)
            .NotNull()
            .When(x => x.PlanningMode == "DESTINATION_FIRST")
            .WithMessage(
                "DestinationId is required for DESTINATION_FIRST.");

        RuleFor(x => x.BudgetAmount)
            .NotNull()
            .When(x => x.PlanningMode == "BUDGET_FIRST")
            .WithMessage(
                "BudgetAmount is required for BUDGET_FIRST.");

        // Security Task 2 — InterestCategoryIds had no upper bound: a client could send
        // an arbitrarily large list in one request. The reference catalog only has a
        // handful of categories, so 50 is generous headroom while still being bounded.
        RuleFor(x => x.InterestCategoryIds)
            .Must(ids => ids.Count <= 50)
            .WithMessage("InterestCategoryIds cannot contain more than 50 items.");
    }
}

public class UpdateTripRequestValidator : AbstractValidator<UpdateTripRequest>
{
    public UpdateTripRequestValidator()
    {
        RuleFor(x => x.TravelerCount)
            .GreaterThan(0)
            .WithMessage("TravelerCount must be greater than 0.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("EndDate must be on or after StartDate.");

        RuleFor(x => x.BudgetAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.BudgetAmount.HasValue)
            .WithMessage("BudgetAmount cannot be negative.");

        // Security Task 2 — same unbounded-list issue on update.
        RuleFor(x => x.InterestCategoryIds)
            .Must(ids => ids.Count <= 50)
            .WithMessage("InterestCategoryIds cannot contain more than 50 items.");
    }
}