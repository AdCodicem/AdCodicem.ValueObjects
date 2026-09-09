using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests.Identifiers;

/// <summary>
/// An entropy source that repeats a fixed pattern, so an assertion can name the identifier it expects.
/// </summary>
/// <remarks>
/// Deriving from <see cref="IdEntropySource"/> rather than seeding a <see cref="Random"/> is the point of the
/// abstraction: substituting entropy is a deliberate act confined to a test, and the production default stays
/// cryptographic with nothing to switch off.
/// </remarks>
internal sealed class DeterministicEntropy : IdEntropySource
{
    private readonly byte _seed;

    public DeterministicEntropy(byte seed = 0) => _seed = seed;

    public override void Fill(Span<byte> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = (byte)(_seed + i);
        }
    }
}
