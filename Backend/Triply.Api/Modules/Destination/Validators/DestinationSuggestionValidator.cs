using FluentValidation;
using Triply.Api.Modules.Destination.Dtos;

namespace Triply.Api.Modules.Destination.Validators;

public class DestinationSuggestionRequestValidator
    : AbstractValidator<DestinationSuggestionRequest>
{
    public DestinationSuggestionRequestValidator()
    {
        RuleFor(x => x.BudgetAmount)
            .GreaterThan(0)
            .WithMessage("BudgetAmount must be greater than 0.");

        RuleFor(x => x.BudgetCurrencyId)
            .GreaterThan(0)
            .WithMessage("BudgetCurrencyId is required.");

        RuleFor(x => x.InterestCategoryIds)
            .NotEmpty()
            .WithMessage("At least one interest category is required.");

        RuleForEach(x => x.InterestCategoryIds)
            .GreaterThan(0)
            .WithMessage("Interest category IDs must be greater than 0.");
    }
}
