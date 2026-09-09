namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// The rules a declared prefix must satisfy.
/// </summary>
/// <remarks>
/// The generator enforces these at compile time through <c>VO0015</c>, so a malformed prefix never reaches a
/// running application. They are restated here because the runtime factories are public and a hand-written
/// identifier type can reach them without passing through the generator.
/// </remarks>
public static class EntityIdPrefix
{
    /// <summary>
    /// The separator between the prefix and the body, and between the segments of a multi-segment prefix.
    /// </summary>
    public const char Separator = '_';

    /// <summary>
    /// The greatest total length of a prefix, separators included.
    /// </summary>
    public const int MaxLength = 16;

    /// <summary>
    /// The greatest length of one segment.
    /// </summary>
    public const int MaxSegmentLength = 8;

    /// <summary>
    /// Determines whether a prefix is usable, and says why when it is not.
    /// </summary>
    /// <param name="prefix">Prefix to check, without its trailing separator.</param>
    /// <param name="error">The rule that was broken, or <see langword="null"/> when the prefix is usable.</param>
    /// <returns><see langword="true"/> when <paramref name="prefix"/> is usable.</returns>
    public static bool IsValid(string? prefix, out string? error)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            error = "the prefix is empty";
            return false;
        }

        if (prefix!.Length > MaxLength)
        {
            error = $"the prefix is longer than {MaxLength} characters";
            return false;
        }

        var segmentLength = 0;
        for (var i = 0; i < prefix.Length; i++)
        {
            var character = prefix[i];

            if (character == Separator)
            {
                if (segmentLength == 0)
                {
                    error = "a segment is empty";
                    return false;
                }

                segmentLength = 0;
                continue;
            }

            // A segment opening on a digit would read as part of the body at a glance, which is exactly the
            // confusion the prefix exists to prevent.
            if (segmentLength == 0 && character is not (>= 'a' and <= 'z'))
            {
                error = "a segment does not start with a lowercase letter";
                return false;
            }

            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9')))
            {
                error = "the prefix holds a character outside 'a'-'z', '0'-'9' and '_'";
                return false;
            }

            if (++segmentLength > MaxSegmentLength)
            {
                error = $"a segment is longer than {MaxSegmentLength} characters";
                return false;
            }
        }

        if (segmentLength == 0)
        {
            error = "the prefix ends with a separator";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Throws when a prefix is unusable.
    /// </summary>
    /// <param name="prefix">Prefix to check.</param>
    /// <param name="parameterName">Name of the argument being validated.</param>
    /// <exception cref="ArgumentException"><paramref name="prefix"/> breaks one of the rules.</exception>
    public static void ThrowIfInvalid(string? prefix, string parameterName)
    {
        if (!IsValid(prefix, out var error))
        {
            throw new ArgumentException($"'{prefix}' is not a valid entity identifier prefix: {error}.", parameterName);
        }
    }
}
