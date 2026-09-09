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
}
