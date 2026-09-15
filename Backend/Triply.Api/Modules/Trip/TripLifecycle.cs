namespace Triply.Api.Modules.Trip;

public static class TripLifecycle
{
    public const string Draft = "DRAFT";
    public const string Generating = "GENERATING";
    public const string Generated = "GENERATED";
    public const string Modified = "MODIFIED";
    public const string Saved = "SAVED";
    public const string Archived = "ARCHIVED";

    public static bool CanTransition(string from, string to) => (from, to) switch
    {
        (Draft, Generating) => true,
        (Generating, Generated) => true,
        (Generating, Draft) => true,
        (Generated, Modified) => true,
        (Modified, Modified) => true,
        (Modified, Saved) => true,
        (Generated, Saved) => true,
        (Saved, Modified) => true,
        (Saved, Archived) => true,
        (Archived, Saved) => true,
        _ => false
    };

    public static void Transition(Triply.Api.Entities.Trip trip, string target)
    {
        if (!CanTransition(trip.Status, target))
            throw new InvalidOperationException(
                $"Trip status cannot transition from {trip.Status} to {target}.");

        trip.Status = target;
    }
}
