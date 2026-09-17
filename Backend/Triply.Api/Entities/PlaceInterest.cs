namespace Triply.Api.Entities;

// Interest tags attached to places for interest-aware destination suggestions.
public class PlaceInterest
{
    public long PlaceId { get; set; }
    public Place Place { get; set; } = default!;

    public long InterestCategoryId { get; set; }
    public InterestCategory InterestCategory { get; set; } = default!;
}
