namespace Triply.Api.Tests;

/// <summary>
/// xUnit collection for every test class that runs the fake Gemini pipeline and
/// therefore reads/writes the shared test place-name state
/// (PartialRegenerationTestPlaceNames). Classes in one collection never run in
/// parallel with each other, which prevents the static state from racing across
/// test classes while still allowing other test classes to run in parallel.
/// </summary>
[CollectionDefinition("GeminiFake")]
public sealed class GeminiFakeCollection
{
}
