using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.TestData;

namespace AdCodicem.ValueObjects.UnitTests.Functions;

/// <summary>
/// A ticket written by hand, ASCII letters only, which only the converter's tests read: the converter describes it by
/// reflection, which registers it for the process, so no other test may hold assumptions about it.
/// </summary>
public sealed class ConverterTicket : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 8 };

    public static ValidationResult Validate(string value) => FunctionsTickets.Validate(value);

    public static bool TryRead(string text, out string value)
    {
        value = text;
        return true;
    }
}

/// <summary>The same ticket, which only the middleware's tests read, for the same reason.</summary>
public sealed class MiddlewareTicket : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 8 };

    public static ValidationResult Validate(string value) => FunctionsTickets.Validate(value);

    public static bool TryRead(string text, out string value)
    {
        value = text;
        return true;
    }
}

/// <summary>The rule both tickets enforce.</summary>
internal static class FunctionsTickets
{
    public static ValidationResult Validate(string value)
        => string.IsNullOrEmpty(value)
            ? ValidationResult.Required("A ticket is required.")
            : value.All(char.IsAsciiLetter)
                ? ValidationResult.Success
                : ValidationResult.InvalidFormat("A ticket holds ASCII letters only.");
}

/// <summary>
/// An identifier written by hand whose parser refuses everything without a reason, which a parser written by hand can
/// forget to give; the Functions tests own it, so that its registration by reflection reaches no other test.
/// </summary>
public sealed class SilentFunctionsProfile : IHandWrittenIdProfile
{
    public static string Prefix => "fnz";

    public static bool RefusesSilently => true;
}
