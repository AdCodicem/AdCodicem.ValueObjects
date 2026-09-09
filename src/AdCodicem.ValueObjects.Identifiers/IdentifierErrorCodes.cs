namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// Validation error codes specific to entity identifiers.
/// </summary>
/// <remarks>
/// Every one of them is a rejection an <c>invalid_format</c> would have flattened. A boundary that tells the
/// caller the check character failed, rather than that something was wrong somewhere, is the difference
/// between a support ticket answered in one message and one answered in five.
/// </remarks>
public static class IdentifierErrorCodes
{
    /// <summary>The text is not the exact length the identifier profile requires.</summary>
    public const string InvalidLength = "value_object.id.invalid_length";

    /// <summary>The text does not carry the prefix declared by the identifier type.</summary>
    public const string InvalidPrefix = "value_object.id.invalid_prefix";

    /// <summary>The body holds a character outside the Crockford Base32 alphabet.</summary>
    public const string InvalidCharacter = "value_object.id.invalid_character";

    /// <summary>The trailing check character does not match the body — a typo, or a truncation.</summary>
    public const string InvalidChecksum = "value_object.id.invalid_checksum";

    /// <summary>The prefix carried by the text belongs to no registered identifier type.</summary>
    public const string UnknownPrefix = "value_object.id.unknown_prefix";
}
