using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace AdCodicem.ValueObjects.CommandLine;

/// <summary>
/// Reads the rule codes the options and arguments of value objects attach to their parse errors.
/// </summary>
/// <remarks>
/// A <see cref="ParseError"/> holds a message and the symbol result it is about, nothing else, and cannot be extended:
/// the class is sealed and its constructor internal. An option or an argument reading a value object keeps the code of
/// the rule that refused a value beside the error it reports, for as long as the error lives, so that an application maps
/// a rule to an exit code, or reports it, without reading the English message:
/// <c>foreach (var error in parseResult.Errors) if (error.TryGetValueObjectErrorCode(out var code)) …</c>. An error
/// System.CommandLine reports itself, an option <c>Required</c> and left out or a token it does not recognise, carries no
/// code.
/// </remarks>
public static class ValueObjectParseErrorExtensions
{
    /// <summary>The refusal of each error a value object reported, kept for as long as the error lives.</summary>
    private static readonly ConditionalWeakTable<ParseError, ValueObjectRefusal> Refusals = new();

    /// <summary>
    /// Gets the code of the rule that refused a value, when a value object reported the error.
    /// </summary>
    /// <param name="error">A parse error, from <see cref="ParseResult.Errors"/>.</param>
    /// <param name="code">The code: one of <see cref="ValueObjectErrorCodes"/>, or a code of the application's own rule.</param>
    /// <returns><see langword="true"/> when an option or an argument of a value object reported the error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static bool TryGetValueObjectErrorCode(this ParseError error, [NotNullWhen(true)] out string? code)
    {
        ArgumentNullException.ThrowIfNull(error);

        code = Refusals.TryGetValue(error, out var refusal) ? refusal.Code : null;
        return code is not null;
    }

    /// <summary>
    /// Gets the value object type that refused a value, when a value object reported the error.
    /// </summary>
    /// <param name="error">A parse error, from <see cref="ParseResult.Errors"/>.</param>
    /// <param name="valueObjectType">The value object type, never its nullable type nor a collection of it.</param>
    /// <returns><see langword="true"/> when an option or an argument of a value object reported the error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static bool TryGetValueObjectType(this ParseError error, [NotNullWhen(true)] out Type? valueObjectType)
    {
        ArgumentNullException.ThrowIfNull(error);

        valueObjectType = Refusals.TryGetValue(error, out var refusal) ? refusal.ValueObjectType : null;
        return valueObjectType is not null;
    }

    /// <summary>
    /// Keeps the refusal of the error a symbol result last reported, the one an option or an argument just added.
    /// </summary>
    /// <param name="result">
    /// The symbol result System.CommandLine lists the error under: an option's, for the argument of an option, or the
    /// argument's own.
    /// </param>
    /// <param name="code">The code of the rule.</param>
    /// <param name="valueObjectType">The value object type that refused the value.</param>
    internal static void Record(SymbolResult result, string code, Type valueObjectType)
        => Refusals.AddOrUpdate(result.Errors.Last(), new ValueObjectRefusal(code, valueObjectType));

    /// <summary>A refusal kept beside the parse error that words it.</summary>
    /// <param name="Code">The code of the rule.</param>
    /// <param name="ValueObjectType">The value object type that refused the value.</param>
    private sealed record ValueObjectRefusal(string Code, Type ValueObjectType);
}
