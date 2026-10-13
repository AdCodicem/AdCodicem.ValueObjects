using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// What the Azure Functions isolated worker hands a value-object parameter, and how it is read: the text of a route
/// value, a query value or other binding data, parsed through the value object's rules.
/// </summary>
/// <remarks>
/// <para>
/// This file is linked into the two packages that read a value object the worker binds, the converter and the middleware
/// that explains a refusal to an HTTP client, which references the first but sees none of its internals: each compiles
/// its own copy, as the AI and the Model Context Protocol packages each compile <c>ValueObjectArguments.cs</c>, so that
/// the middleware parses a parameter again exactly as the converter parsed it. No test names it: both copies are reached
/// through their package's API.
/// </para>
/// <para>
/// It closes no generic type at run time: each caller closes its own adapter through the descriptor's visitor.
/// </para>
/// </remarks>
internal static class ValueObjectBindingText
{
    /// <summary>
    /// The key under which the worker lists, in a parameter's properties, the input or trigger binding attribute the
    /// parameter carries: its payload belongs to the binding, and is left to the binding's converters and the worker's
    /// serializer.
    /// </summary>
    public const string BindingAttribute = "bindingAttribute";

    /// <summary>
    /// The key under which the worker lists, in a parameter's properties, the assembly-qualified name of the converter its
    /// <c>[InputConverter]</c> names, <c>[FromBody]</c> among them.
    /// </summary>
    public const string ConverterType = "converterType";

    private const string RegisteredOnly =
        "ValueObjectRegistry.TryResolve reflects only for a value object nothing registered: one written by hand, or a "
        + "construction of a generic one. A generated value object registers itself statically. Without dynamic code it is "
        + "not called, and such a parameter is left to the worker's own converters until it is registered.";

    /// <summary>
    /// Finds the descriptor of a value object, registered or, where the runtime supports dynamic code, described by
    /// reflection, as the minimal API filter finds it.
    /// </summary>
    /// <param name="type">The parameter's type, possibly nullable.</param>
    /// <param name="descriptor">Its descriptor, when one is found.</param>
    /// <returns>Whether a descriptor was found.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = RegisteredOnly)]
    public static bool TryDescribe(Type type, [NotNullWhen(true)] out ValueObjectDescriptor? descriptor)
        => ValueObjectRegistry.TryGet(type, out descriptor)
           || (RuntimeFeature.IsDynamicCodeSupported && ValueObjectRegistry.TryResolve(type, out descriptor));

    /// <summary>
    /// Tells whether the worker reads a parameter through the registered converters, the value object converter first:
    /// it carries no binding attribute, and the converter its <c>[InputConverter]</c> names, if any, is
    /// <paramref name="converter"/>.
    /// </summary>
    /// <param name="properties">The parameter's properties, as the worker lists them.</param>
    /// <param name="converter">The value object converter's type.</param>
    /// <returns>Whether the parameter is the value object converter's to read.</returns>
    public static bool IsReadAsText(IReadOnlyDictionary<string, object> properties, Type converter)
        => !properties.ContainsKey(BindingAttribute)
           && (!properties.TryGetValue(ConverterType, out var named)
               || string.Equals(named as string, converter.AssemblyQualifiedName, StringComparison.Ordinal));

    /// <summary>
    /// Parses the text the worker holds for a parameter: as it stands first, and, when that fails, as the string it
    /// encodes when it is a JSON string literal, or, for a value object over <see cref="decimal"/>, as the number it is
    /// when it is a JSON number, whose verdict then stands.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="text">The text, empty text included, which the value object's own rules refuse or accept.</param>
    /// <param name="result">The value object, when the text is accepted.</param>
    /// <param name="validation">The rule that refused it, otherwise.</param>
    /// <returns>Whether the text is accepted.</returns>
    /// <remarks>
    /// <para>
    /// The host sends binding data of a type other than <see cref="string"/> as JSON: a queue message's
    /// <c>InsertionTime</c> arrives as <c>"2026-10-13T10:13:54+00:00"</c>, quotes included, which the worker's own JSON
    /// conversion reads today, and which the value object reads once decoded.
    /// </para>
    /// <para>
    /// The host writes a message's number through a double, so <c>0.0000001</c> arrives as <c>1E-07</c>, and a route or
    /// query value may hold an exponent too: a decimal's <c>TryParse</c> takes none, where the worker's JSON conversion,
    /// through the generated JSON converter, reads the number with
    /// <see cref="Utf8JsonReader.TryGetDecimal(out decimal)"/> and hands it to <c>TryCreate</c>, as this does. Every other
    /// underlying type's <c>TryParse</c> reads every JSON number the type holds already.
    /// </para>
    /// </remarks>
    public static bool TryParse<TSelf, TValue>(string text, out TSelf result, out ValidationResult validation)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (TSelf.TryParse(text, CultureInfo.InvariantCulture, out result, out validation))
        {
            return true;
        }

        if (TryDecodeString(text, out var decoded))
        {
            return TSelf.TryParse(decoded, CultureInfo.InvariantCulture, out result, out validation);
        }

        return typeof(TValue) == typeof(decimal)
               && TryReadDecimal(text, out var number)
               && TSelf.TryCreate((TValue)(object)number, out result, out validation);
    }

    /// <summary>Reads text that is a JSON string literal, and nothing else, as the string it encodes.</summary>
    private static bool TryDecodeString(string text, [NotNullWhen(true)] out string? decoded)
    {
        decoded = null;
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"')
        {
            return false;
        }

        try
        {
            // Text that opens with a quote reads as a string or throws, and so does a second value after it: the reader
            // takes a single value, and the text ends with that value's closing quote.
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text));
            reader.Read();
            var value = reader.GetString()!;
            reader.Read();
            decoded = value;

            return true;
        }
        catch (JsonException)
        {
            // An escape the reader refuses, a string left open, or a second value after the first.
            return false;
        }
        catch (InvalidOperationException)
        {
            // An escape of half a surrogate pair, which no string holds.
            return false;
        }
    }

    /// <summary>Reads text that is a JSON number, and nothing else, as the decimal it holds, if it holds one.</summary>
    private static bool TryReadDecimal(string text, out decimal number)
    {
        number = default;
        try
        {
            // Text that holds no JSON value throws, and so does a second value after the number: the reader takes a
            // single value.
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text));
            reader.Read();
            if (reader.TokenType != JsonTokenType.Number || !reader.TryGetDecimal(out number))
            {
                return false;
            }

            reader.Read();

            return true;
        }
        catch (JsonException)
        {
            // No JSON value, a number cut short, or a second value after the first.
            return false;
        }
    }
}
