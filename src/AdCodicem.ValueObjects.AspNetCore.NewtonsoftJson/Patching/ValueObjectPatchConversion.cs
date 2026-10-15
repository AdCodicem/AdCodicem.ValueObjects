using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.NewtonsoftJson;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Patching;

/// <summary>
/// Reads the values and the dictionary keys of one JSON Patch document's operations into value objects, through their
/// rules, and keeps the code of a refusal for the error JSON Patch reports next.
/// </summary>
/// <remarks>
/// <para>
/// JSON Patch converts a value by writing it to text and reading it back under settings of its own, holding the
/// document's resolver alone, and catches every exception: the converter's message, its code, the digits of a
/// <see cref="decimal"/> and the text of a string that looks like a date are lost there. This reads the value as the
/// document holds it instead, through <see cref="ValueObjectConverter"/>, under the settings
/// <see cref="ValueObjectJsonSerializerSettingsExtensions.AddValueObjects"/> gives it and the document's resolver.
/// </para>
/// <para>
/// A refusal is kept until the error that reports it takes its code, and forgotten when an operation starts: every
/// refusal kept is followed by the failure of the step that kept it, which JSON Patch, or the object adapter, reports.
/// </para>
/// </remarks>
internal sealed class ValueObjectPatchConversion
{
    private readonly JsonSerializer _serializer;

    private (string Code, string Message)? _refusal;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectPatchConversion"/> class.
    /// </summary>
    /// <param name="resolver">The document's contract resolver.</param>
    public ValueObjectPatchConversion(IContractResolver resolver)
        => _serializer = JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = resolver }.AddValueObjects());

    /// <summary>
    /// Gets the message of the refusal no error has reported yet, if any.
    /// </summary>
    public string? Message => _refusal?.Message;

    /// <summary>
    /// Tells a value object, or its nullable type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for a value object, nullable or not.</returns>
    public static bool Handles(Type type) => ValueObjectRegistry.IsValueObject(type);

    /// <summary>
    /// Forgets a refusal, as an operation starts.
    /// </summary>
    public void Clear() => _refusal = null;

    /// <summary>
    /// Hands the code of the refusal an error reports, and forgets it.
    /// </summary>
    /// <returns>The code, or <see langword="null"/> for an error no refusal explains.</returns>
    public string? TakeCode()
    {
        var code = _refusal?.Code;
        _refusal = null;

        return code;
    }

    /// <summary>
    /// Keeps a refusal of the patch's own, rather than of a value's rules, and gives its message.
    /// </summary>
    /// <param name="code">The code of the refusal.</param>
    /// <param name="message">Its message.</param>
    /// <returns>The message.</returns>
    public string Refuse(string code, string message)
    {
        _refusal = (code, message);

        return message;
    }

    /// <summary>
    /// Reads a value into a value object as the document holds it: a string, a number, a boolean, a <c>null</c> or a
    /// <see cref="JToken"/>, never written to text and read again, so that its digits, its offset and its text are the
    /// client's.
    /// </summary>
    /// <remarks>
    /// An instance of the value object, which <c>copy</c>, <c>move</c> or a document built in code hands over, is kept as
    /// it is, but for a default instance its type refuses, which no writer writes either.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="type">The value object, or its nullable type.</param>
    /// <param name="converted">The value object, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value object accepted the value.</returns>
    public bool TryConvert(object? value, Type type, out object? converted)
    {
        _refusal = null;
        if (value is not null && type.IsInstanceOfType(value))
        {
            ValueObjectRegistry.TryResolve(value.GetType(), out var descriptor);
            if (descriptor!.ValidateWrite(value) is { IsValid: false } refusal)
            {
                _refusal = (refusal.ErrorCode, $"The value is not a valid {descriptor.ValueObjectType.Name}: {refusal.ErrorMessage}");
                converted = null;
                return false;
            }

            converted = value;
            return true;
        }

        try
        {
            var token = value as JToken ?? (value is null ? JValue.CreateNull() : JToken.FromObject(value, _serializer));
            converted = token.ToObject(type, _serializer);
            return true;
        }
        catch (JsonException exception)
        {
            // A value object written by hand over a type the generator does not support has its value read by
            // Newtonsoft.Json, whose refusal carries no code and quotes the value.
            _refusal = ValueObjectErrors.TryGetCode(exception, out var code)
                ? (code, exception.Message)
                : (ValueObjectErrorCodes.NotParsable, $"The value could not be read as {(Nullable.GetUnderlyingType(type) ?? type).Name}.");
            converted = null;
            return false;
        }
    }

    /// <summary>
    /// Reads a dictionary key into a value object, as a route value is read: its text, in the invariant culture.
    /// </summary>
    /// <param name="text">The key, as the path names it.</param>
    /// <param name="descriptor">The value object the dictionary is keyed by.</param>
    /// <param name="key">The value object, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value object accepted the key.</returns>
    public bool TryParseKey(string text, ValueObjectDescriptor descriptor, out object? key)
    {
        _refusal = null;
        if (descriptor.TryParse(text, CultureInfo.InvariantCulture, out key, out var validation))
        {
            return true;
        }

        // The descriptor gives every refusal a code, that of a value object written by hand which says nothing included.
        _refusal = (validation.ErrorCode!, $"The key is not a valid {descriptor.ValueObjectType.Name}: {validation.ErrorMessage}");
        return false;
    }

    /// <summary>
    /// Replaces JSON Patch's message, which quotes the value, with the message of the refusal that made a step fail.
    /// </summary>
    /// <param name="applied">Whether the step applied.</param>
    /// <param name="errorMessage">JSON Patch's message, replaced when a value object refused the value.</param>
    /// <returns><paramref name="applied"/>.</returns>
    public bool Explain(bool applied, ref string errorMessage)
    {
        if (!applied && Message is { } message)
        {
            errorMessage = message;
        }

        return applied;
    }
}
