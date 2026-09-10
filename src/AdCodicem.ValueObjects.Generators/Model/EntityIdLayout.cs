namespace AdCodicem.ValueObjects.Generators.Model;

/// <summary>
/// The entity identifier layout, as the generator needs to know it at compile time.
/// </summary>
/// <remarks>
/// <para>
/// These numbers restate what <c>AdCodicem.ValueObjects.Identifiers.EntityIdFormat</c> and
/// <c>EntityIdPrefix</c> hold at run time. The duplication is forced: an analyzer targets
/// <c>netstandard2.0</c> and cannot reference the <c>net10.0</c> runtime package it generates calls into.
/// </para>
/// <para>
/// <c>EntityIdLayoutTests</c> asserts the two agree, so the pair cannot drift apart silently — which matters
/// because a mismatch would emit a column width the runtime then refuses to fill.
/// </para>
/// </remarks>
internal static class EntityIdLayout
{
    public const int RandomLength = 16;

    public const int ChecksumLength = 1;

    public const int MaxPrefixLength = 16;

    public const int MaxSegmentLength = 8;

    public const char Separator = '_';

    public const string DefaultGranularity = "Hour";

    /// <summary>
    /// Gets the number of characters the time bucket occupies.
    /// </summary>
    /// <param name="granularity">Name of the <c>IdGranularity</c> member.</param>
    /// <returns>The number of characters.</returns>
    public static int TimestampLength(string granularity) => granularity switch
    {
        "Minute" => 6,
        "Day" => 3,
        _ => 4,
    };

    /// <summary>
    /// Gets the number of characters after the prefix separator.
    /// </summary>
    /// <param name="granularity">Name of the <c>IdGranularity</c> member.</param>
    /// <returns>The body length.</returns>
    public static int BodyLength(string granularity)
        => TimestampLength(granularity) + RandomLength + ChecksumLength;

    /// <summary>
    /// Gets the exact length of an identifier, which is also the width of its database column.
    /// </summary>
    /// <param name="prefix">Declared prefix, without its trailing separator.</param>
    /// <param name="granularity">Name of the <c>IdGranularity</c> member.</param>
    /// <returns>The total length.</returns>
    public static int TotalLength(string prefix, string granularity)
        => prefix.Length + 1 + BodyLength(granularity);

    /// <summary>
    /// Determines whether a prefix is usable, and says which rule it broke when it is not.
    /// </summary>
    /// <param name="prefix">Declared prefix, without its trailing separator.</param>
    /// <param name="error">The rule that was broken, or <see langword="null"/> when the prefix is usable.</param>
    /// <returns><see langword="true"/> when the prefix is usable.</returns>
    public static bool IsValidPrefix(string? prefix, out string error)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            error = "it is empty";
            return false;
        }

        if (prefix!.Length > MaxPrefixLength)
        {
            error = $"it is longer than {MaxPrefixLength} characters";
            return false;
        }

        var segmentLength = 0;
        foreach (var character in prefix)
        {
            if (character == Separator)
            {
                if (segmentLength == 0)
                {
                    error = "one of its segments is empty";
                    return false;
                }

                segmentLength = 0;
                continue;
            }

            // A segment opening on a digit reads as part of the body at a glance, which is the very confusion
            // the prefix exists to prevent.
            if (segmentLength == 0 && (character < 'a' || character > 'z'))
            {
                error = "one of its segments does not start with a lowercase letter";
                return false;
            }

            if ((character < 'a' || character > 'z') && (character < '0' || character > '9'))
            {
                error = "it holds a character outside 'a'-'z', '0'-'9' and '_'";
                return false;
            }

            if (++segmentLength > MaxSegmentLength)
            {
                error = $"one of its segments is longer than {MaxSegmentLength} characters";
                return false;
            }
        }

        if (segmentLength == 0)
        {
            error = "it ends with a separator";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
