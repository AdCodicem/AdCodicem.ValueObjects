using System.Security.Cryptography;

namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// The source of the random part of a new entity identifier.
/// </summary>
/// <remarks>
/// <para>
/// This exists so tests can make identifier generation deterministic without reaching for a seeded
/// <see cref="Random"/> in production by mistake. The default, <see cref="System"/>, is a cryptographic
/// generator, and that is not a detail: identifiers appear in URLs and logs, and a
/// <see cref="Random.Shared"/>-derived body is recoverable from a handful of samples, which would turn every
/// identifier into a guessable one.
/// </para>
/// <para>
/// An identifier is still not a secret. Unguessability is defence in depth; authorization is the control.
/// </para>
/// </remarks>
public abstract class IdEntropySource
{
    /// <summary>
    /// Gets the cryptographic source used unless a caller substitutes one.
    /// </summary>
    public static IdEntropySource System { get; } = new CryptographicEntropySource();

    /// <summary>
    /// Fills a buffer with uniformly distributed bytes.
    /// </summary>
    /// <param name="destination">Buffer to fill entirely.</param>
    public abstract void Fill(Span<byte> destination);

    private sealed class CryptographicEntropySource : IdEntropySource
    {
        public override void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
    }
}
