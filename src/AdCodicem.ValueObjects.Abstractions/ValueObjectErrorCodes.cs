using System.Text;

namespace AdCodicem.ValueObjects;

/// <summary>
/// Well-known validation error codes shared by the framework and its integrations.
/// </summary>
/// <remarks>
/// Codes are stable strings so they can safely cross process boundaries: they are surfaced in
/// <c>ProblemDetails</c> responses and can be mapped to localized messages by the consumer.
/// Consumers are free to define their own codes; these are only the ones the framework itself emits.
/// </remarks>
public static class ValueObjectErrorCodes
{
    /// <summary>A value was expected but none was supplied.</summary>
    public const string Required = "value_object.required";

    /// <summary>The value does not match the expected shape.</summary>
    public const string InvalidFormat = "value_object.invalid_format";

    /// <summary>The value falls outside the accepted range.</summary>
    public const string OutOfRange = "value_object.out_of_range";

    /// <summary>The value is longer than allowed.</summary>
    public const string TooLong = "value_object.too_long";

    /// <summary>The value is shorter than allowed.</summary>
    public const string TooShort = "value_object.too_short";

    /// <summary>The value does not belong to the closed set of values accepted by the type.</summary>
    public const string NotAKnownValue = "value_object.not_a_known_value";

    /// <summary>The supplied text could not be converted to the underlying type.</summary>
    public const string NotParsable = "value_object.not_parsable";

    /// <summary>
    /// The longest reason gRPC's <c>google.rpc.ErrorInfo</c> takes.
    /// </summary>
    private const int MaxReasonLength = 63;

    /// <summary>
    /// Maps a code to the upper snake case gRPC's error model takes as a reason: <c>value_object.too_long</c> becomes
    /// <c>VALUE_OBJECT_TOO_LONG</c>, and <c>iban.check_digits</c> becomes <c>IBAN_CHECK_DIGITS</c>.
    /// </summary>
    /// <param name="errorCode">The code, one of the framework's or one of your own.</param>
    /// <returns>The code in upper snake case.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errorCode"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The code, once mapped, is no valid reason: it does not start with a letter, holds fewer than three letters,
    /// digits and underscores, or more than 63.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <c>google.rpc.ErrorInfo.reason</c> and <c>google.rpc.BadRequest.FieldViolation.reason</c> must match
    /// <c>[A-Z][A-Z0-9_]+[A-Z0-9]</c>, and an <c>ErrorInfo</c> reason is at most 63 characters long, so a code such as
    /// <c>value_object.too_long</c> cannot be used as it is. Every service that maps a code through this method maps it
    /// the same way, and a client in another language branches on the result.
    /// </para>
    /// <para>
    /// ASCII letters are upper-cased and ASCII digits kept. Any run of other characters, the dot and the underscore
    /// included, becomes one underscore, and an underscore left at either end is dropped. A code that cannot become a
    /// valid reason is refused rather than truncated or given a prefix, which would make two codes one.
    /// </para>
    /// </remarks>
    public static string ToUpperSnakeCase(string errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);

        var reason = new StringBuilder(errorCode.Length);
        foreach (var character in errorCode)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                reason.Append(char.ToUpperInvariant(character));
            }
            else if (reason.Length > 0 && reason[^1] != '_')
            {
                reason.Append('_');
            }
        }

        if (reason.Length > 0 && reason[^1] == '_')
        {
            reason.Length--;
        }

        if (reason.Length < 3 || reason.Length > MaxReasonLength || !char.IsAsciiLetter(reason[0]))
        {
            throw new ArgumentException(
                $"'{errorCode}' maps to '{reason}', which is no gRPC reason: a reason starts with a letter and holds "
                + $"from 3 to {MaxReasonLength} letters, digits and underscores.",
                nameof(errorCode));
        }

        return reason.ToString();
    }
}
