namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// The layout of an entity identifier: <c>prefix _ bucket random check</c>.
/// </summary>
/// <remarks>
/// <para>
/// Generated identifier types call into this rather than carrying their own copy of the layout, so a change to
/// the format is a change in one place. Every member is public because generated code lives in the consumer's
/// assembly and cannot reach anything internal here.
/// </para>
/// <para>
/// Validation is a span scan, not a regular expression. At fixed length over a fixed alphabet a scan is both
/// faster and simpler, and it spares an entity identifier the compiled <c>Regex</c> that a
/// <c>Pattern</c>-constrained value object has to pay for at start-up — source generators cannot feed
/// <c>[GeneratedRegex]</c>, so that cost is unavoidable there and avoidable here.
/// </para>
/// </remarks>
public static class EntityIdFormat
{
    /// <summary>
    /// The number of characters carrying randomness, at every granularity.
    /// </summary>
    /// <remarks>
    /// 21 symbols of five bits each: 105 bits. The birthday bound is roughly 6.4 × 10¹⁵ identifiers within one
    /// time bucket, so a collision is not a thing that happens.
    /// </remarks>
    public const int RandomLength = 21;

    /// <summary>
    /// The number of trailing check characters.
    /// </summary>
    public const int ChecksumLength = 1;

    /// <summary>
    /// The greatest total length any profile can produce, which bounds a stack buffer.
    /// </summary>
    public const int MaxTotalLength = EntityIdPrefix.MaxLength + 1 + MaxBodyLength;

    private const int MaxBodyLength = 6 + RandomLength + ChecksumLength;

    /// <summary>
    /// Gets the instant the time bucket counts from.
    /// </summary>
    /// <remarks>
    /// 2020, not 1970. Half a century of elapsed time spent before the first identifier is issued is half the
    /// bucket space burned for nothing, and moving the epoch forward buys that back as horizon.
    /// </remarks>
    public static DateTimeOffset Epoch { get; } = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Gets the number of characters the time bucket occupies at a granularity.
    /// </summary>
    /// <param name="granularity">Bucket width.</param>
    /// <returns>The number of characters.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="granularity"/> is not a declared value.</exception>
    public static int TimestampLength(IdGranularity granularity) => granularity switch
    {
        // Sized so that each granularity keeps roughly a century of horizon from the epoch: 30 bits of minutes
        // reach the year 4062, 20 bits of hours the year 2139, and 15 bits of days the year 2109.
        IdGranularity.Minute => 6,
        IdGranularity.Hour => 4,
        IdGranularity.Day => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(granularity)),
    };

    /// <summary>
    /// Gets the number of characters after the prefix separator.
    /// </summary>
    /// <param name="granularity">Bucket width.</param>
    /// <returns>The body length.</returns>
    public static int BodyLength(IdGranularity granularity)
        => TimestampLength(granularity) + RandomLength + ChecksumLength;

    /// <summary>
    /// Gets the exact length of an identifier, which is also the width of its database column.
    /// </summary>
    /// <param name="prefix">Declared prefix, without its trailing separator.</param>
    /// <param name="granularity">Bucket width.</param>
    /// <returns>The total length.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
    public static int TotalLength(string prefix, IdGranularity granularity)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return prefix.Length + 1 + BodyLength(granularity);
    }

    /// <summary>
    /// Gets the number of entropy bytes <see cref="Create"/> consumes.
    /// </summary>
    /// <remarks>
    /// One byte per random character. Taking the low five bits of a uniform byte is itself uniform, which
    /// reducing a smaller buffer modulo 32 would not be.
    /// </remarks>
    public static int EntropyByteCount => RandomLength;

    /// <summary>
    /// Builds a new identifier.
    /// </summary>
    /// <param name="prefix">Declared prefix, without its trailing separator.</param>
    /// <param name="granularity">Bucket width.</param>
    /// <param name="timeProvider">Clock supplying the bucket.</param>
    /// <param name="entropy">Source of the random part.</param>
    /// <returns>The identifier, canonical and valid by construction.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="prefix"/> breaks the prefix rules.</exception>
    public static string Create(string prefix, IdGranularity granularity, TimeProvider timeProvider, IdEntropySource entropy)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(entropy);
        EntityIdPrefix.ThrowIfInvalid(prefix, nameof(prefix));

        Span<byte> bytes = stackalloc byte[EntropyByteCount];
        entropy.Fill(bytes);

        Span<char> buffer = stackalloc char[MaxTotalLength];
        var written = Write(prefix, granularity, timeProvider.GetUtcNow(), bytes, buffer);

        return new string(buffer[..written]);
    }

    /// <summary>
    /// Writes an identifier into a destination buffer.
    /// </summary>
    /// <param name="prefix">Declared prefix, without its trailing separator.</param>
    /// <param name="granularity">Bucket width.</param>
    /// <param name="timestamp">Instant the bucket encodes.</param>
    /// <param name="entropy">At least <see cref="EntropyByteCount"/> bytes of randomness.</param>
    /// <param name="destination">Buffer receiving the identifier.</param>
    /// <returns>The number of characters written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="entropy"/> or <paramref name="destination"/> is too short.</exception>
    public static int Write(
        string prefix,
        IdGranularity granularity,
        DateTimeOffset timestamp,
        ReadOnlySpan<byte> entropy,
        Span<char> destination)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        var total = TotalLength(prefix, granularity);
        if (destination.Length < total)
        {
            throw new ArgumentException($"The destination must hold at least {total} characters.", nameof(destination));
        }

        if (entropy.Length < EntropyByteCount)
        {
            throw new ArgumentException($"At least {EntropyByteCount} bytes of entropy are required.", nameof(entropy));
        }

        prefix.AsSpan().CopyTo(destination);
        destination[prefix.Length] = EntityIdPrefix.Separator;

        var body = destination[(prefix.Length + 1)..total];
        var timestampLength = TimestampLength(granularity);

        WriteBucket(timestamp, granularity, body[..timestampLength]);

        for (var i = 0; i < RandomLength; i++)
        {
            body[timestampLength + i] = CrockfordBase32.Encode(entropy[i] & 31);
        }

        body[^1] = IdCheckCharacter.Compute(prefix.AsSpan(), body[..^ChecksumLength]);

        return total;
    }

    /// <summary>
    /// Rewrites a candidate into its canonical spelling.
    /// </summary>
    /// <param name="text">Candidate text.</param>
    /// <param name="prefix">Declared prefix, without its trailing separator.</param>
    /// <returns>
    /// The canonical identifier, or the trimmed input when it does not carry the declared prefix. Normalization
    /// never rejects: a text this could not make sense of is handed to <see cref="Validate"/> to refuse.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Trims surrounding whitespace, drops the hyphens Crockford allows for readability, folds the body to its
    /// canonical symbols, and restores the declared casing of the prefix. Idempotent, as the contract of a
    /// normalizer requires.
    /// </remarks>
    public static string Normalize(ReadOnlySpan<char> text, string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        var trimmed = text.Trim();

        if (!CarriesPrefix(trimmed, prefix))
        {
            return trimmed.ToString();
        }

        var length = trimmed.Length;
        Span<char> buffer = length <= MaxTotalLength ? stackalloc char[MaxTotalLength] : new char[length];

        prefix.AsSpan().CopyTo(buffer);
        buffer[prefix.Length] = EntityIdPrefix.Separator;
        var written = prefix.Length + 1;

        foreach (var character in trimmed[(prefix.Length + 1)..])
        {
            if (character == CrockfordBase32.Separator)
            {
                continue;
            }

            buffer[written++] = CrockfordBase32.Canonicalize(character);
        }

        return new string(buffer[..written]);
    }

    /// <summary>
    /// Validates an already normalized candidate.
    /// </summary>
    /// <param name="value">Normalized candidate.</param>
    /// <param name="prefix">Declared prefix, without its trailing separator.</param>
    /// <param name="granularity">Bucket width.</param>
    /// <returns>The first rule the candidate breaks, or success.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
    public static ValidationResult Validate(ReadOnlySpan<char> value, string prefix, IdGranularity granularity)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        var total = TotalLength(prefix, granularity);
        if (value.Length != total)
        {
            return ValidationResult.Failure(
                IdentifierErrorCodes.InvalidLength,
                $"A {prefix}{EntityIdPrefix.Separator} identifier is exactly {total} characters long.");
        }

        if (!value[..prefix.Length].SequenceEqual(prefix.AsSpan()) || value[prefix.Length] != EntityIdPrefix.Separator)
        {
            return ValidationResult.Failure(
                IdentifierErrorCodes.InvalidPrefix,
                $"The identifier must start with '{prefix}{EntityIdPrefix.Separator}'.");
        }

        var body = value[(prefix.Length + 1)..];

        foreach (var character in body)
        {
            if (!IsCanonicalSymbol(character))
            {
                return ValidationResult.Failure(
                    IdentifierErrorCodes.InvalidCharacter,
                    $"'{character}' is not a character of the Crockford Base32 alphabet.");
            }
        }

        if (body[^1] != IdCheckCharacter.Compute(prefix.AsSpan(), body[..^ChecksumLength]))
        {
            return ValidationResult.Failure(
                IdentifierErrorCodes.InvalidChecksum,
                "The identifier is corrupt: its check character does not match its body.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Determines whether a text opens with a prefix and its separator, ignoring case.
    /// </summary>
    /// <param name="text">Text to test.</param>
    /// <param name="prefix">Declared prefix, without its trailing separator.</param>
    /// <returns><see langword="true"/> when the prefix and its separator are present.</returns>
    public static bool CarriesPrefix(ReadOnlySpan<char> text, string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return text.Length > prefix.Length
               && text[prefix.Length] == EntityIdPrefix.Separator
               && text[..prefix.Length].Equals(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the regular expression describing a profile, for publication in an OpenAPI schema.
    /// </summary>
    /// <param name="prefix">Declared prefix, without its trailing separator.</param>
    /// <param name="granularity">Bucket width.</param>
    /// <returns>An anchored pattern.</returns>
    /// <remarks>
    /// Published as schema text and never compiled: the running validation is <see cref="Validate"/>, a span
    /// scan. The pattern exists so that a client generated from the document rejects the same texts.
    /// </remarks>
    public static string SchemaPattern(string prefix, IdGranularity granularity)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return $"^{prefix}{EntityIdPrefix.Separator}[{CrockfordBase32.Alphabet}]{{{BodyLength(granularity)}}}$";
    }

    /// <summary>
    /// Writes the time bucket, most significant symbol first.
    /// </summary>
    /// <remarks>
    /// Big-endian on purpose: the alphabet is increasing in ASCII, so writing the high symbols first makes the
    /// ordinal comparison of two identifiers reproduce the chronological order of their buckets, which is what
    /// gives the database index its monotonic head.
    /// </remarks>
    /// <param name="timestamp">Instant to encode.</param>
    /// <param name="granularity">Bucket width.</param>
    /// <param name="destination">Exactly <see cref="TimestampLength"/> characters.</param>
    private static void WriteBucket(DateTimeOffset timestamp, IdGranularity granularity, Span<char> destination)
    {
        var ticksPerBucket = granularity switch
        {
            IdGranularity.Minute => TimeSpan.TicksPerMinute,
            IdGranularity.Hour => TimeSpan.TicksPerHour,
            IdGranularity.Day => TimeSpan.TicksPerDay,
            _ => throw new ArgumentOutOfRangeException(nameof(granularity)),
        };

        // Clamped rather than wrapped at both ends. A clock dragged before the epoch, or an application still
        // running past the horizon, then loses index locality and nothing else; wrapping would break the
        // ordering itself, which is the one property the bucket exists to provide.
        var elapsed = timestamp.UtcDateTime.Ticks - Epoch.UtcDateTime.Ticks;
        var bucket = elapsed <= 0 ? 0 : elapsed / ticksPerBucket;
        var ceiling = (1L << (destination.Length * CrockfordBase32.BitsPerSymbol)) - 1;

        if (bucket > ceiling)
        {
            bucket = ceiling;
        }

        for (var i = destination.Length - 1; i >= 0; i--)
        {
            destination[i] = CrockfordBase32.Encode((int)(bucket & 31));
            bucket >>= CrockfordBase32.BitsPerSymbol;
        }
    }

    /// <summary>
    /// Determines whether a character is a symbol in its canonical spelling.
    /// </summary>
    /// <remarks>
    /// Stricter than <see cref="CrockfordBase32.IsSymbol"/>, which accepts the aliases and either case. By the
    /// time <see cref="Validate"/> runs, normalization has folded those, so anything left is a genuine defect.
    /// </remarks>
    /// <param name="character">Character to test.</param>
    /// <returns><see langword="true"/> when the character is a canonical symbol.</returns>
    private static bool IsCanonicalSymbol(char character)
        => CrockfordBase32.IsSymbol(character) && CrockfordBase32.Canonicalize(character) == character;
}
