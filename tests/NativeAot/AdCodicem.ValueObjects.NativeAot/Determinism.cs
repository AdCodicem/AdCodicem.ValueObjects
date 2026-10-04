namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>A clock stopped at one instant, so that an identifier minted under the JIT and natively is the same.</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
}

/// <summary>Bytes counted up from zero, so that the random part of an identifier is the same in both runs.</summary>
internal sealed class CountingEntropySource : IdEntropySource
{
    private byte _next;

    public override void Fill(Span<byte> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = _next++;
        }
    }
}
