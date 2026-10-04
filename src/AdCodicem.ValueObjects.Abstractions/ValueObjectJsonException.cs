using System.Text.Json;

namespace AdCodicem.ValueObjects;

/// <summary>
/// A value object refused the value System.Text.Json was reading or writing.
/// </summary>
/// <remarks>
/// <para>
/// The converter the generator emits throws it, and so do the converter for a value object written by hand and the
/// converter of <c>AnyEntityId</c>, wherever they refuse a value: a value or a dictionary key a rule rejects, a token
/// of the wrong kind or that the underlying type cannot hold, and a value to write that its type rejects. It is a
/// <see cref="JsonException"/>, so a <c>catch (JsonException)</c> still catches it, and System.Text.Json treats it as
/// any exception a converter throws: it sets <see cref="JsonException.Path"/> and rethrows the same instance.
/// </para>
/// <para>
/// It carries the code of the rule, which it also stores in <see cref="Exception.Data"/> under
/// <see cref="ValueObjectErrors.ErrorCodeKey"/>, so that a reader that only knows the data of an exception, such as a
/// logger writing it out, finds it too. <see cref="ValueObjectErrors.TryGetCode"/> reads it from the exception or from
/// one that wraps it. Like every exception of the library, it never carries the value it refused.
/// </para>
/// </remarks>
[Serializable]
public sealed class ValueObjectJsonException : JsonException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectJsonException"/> class.
    /// </summary>
    /// <param name="message">
    /// Message naming the value object and the rule, or <see langword="null"/> for System.Text.Json to write its own,
    /// "The JSON value could not be converted to …", followed by the path, as it does for a token it cannot read.
    /// </param>
    /// <param name="valueObjectType">Value object type that refused the value.</param>
    /// <param name="errorCode">Stable, machine-readable code of the violated rule.</param>
    /// <exception cref="ArgumentNullException"><paramref name="valueObjectType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is <see langword="null"/> or empty.</exception>
    public ValueObjectJsonException(string? message, Type valueObjectType, string errorCode)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(valueObjectType);
        ArgumentException.ThrowIfNullOrEmpty(errorCode);

        ValueObjectType = valueObjectType;
        ErrorCode = errorCode;
        Data[ValueObjectErrors.ErrorCodeKey] = errorCode;
    }

    /// <summary>
    /// Gets the value object type that refused the value.
    /// </summary>
    public Type ValueObjectType { get; }

    /// <summary>
    /// Gets the stable, machine-readable code of the violated rule.
    /// </summary>
    /// <remarks>
    /// The code of the rule that fired, or <see cref="ValueObjectErrorCodes.NotParsable"/> for a token that is not of
    /// the underlying type at all, and <see cref="ValueObjectErrorCodes.Required"/> for a JSON <c>null</c>, which only a
    /// value object that cannot be <see langword="null"/> is handed.
    /// </remarks>
    public string ErrorCode { get; }
}
