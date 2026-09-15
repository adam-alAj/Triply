using Triply.Api.Modules.Destination.Dtos;

namespace Triply.Api.Modules.Destination;

public interface IDestinationSuggestionService
{
    Task<IReadOnlyList<DestinationSuggestionResponse>> SuggestAsync(
        DestinationSuggestionRequest request,
        CancellationToken cancellationToken = default);
}
