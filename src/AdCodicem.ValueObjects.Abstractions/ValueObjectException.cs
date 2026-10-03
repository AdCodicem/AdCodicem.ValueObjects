namespace AdCodicem.ValueObjects;

/// <summary>
/// Thrown when a value object is constructed from a value that violates one of its rules.
/// </summary>
/// <remarks>
/// <para>
/// The integrations that take outside input (JSON, model binding, FluentValidation, Dapper) use the
/// <c>TryCreate</c> and <c>TryParse</c> family and report a refusal in their own terms. This exception comes from
/// the explicit <c>Create</c> and <c>Parse</c> entry points and an explicit conversion, for code that treats a
/// rejected value as a bug, and from a strict EF Core read, which goes through <c>Create</c> and fails the query.
/// </para>
/// <para>
/// It is a <see cref="FormatException"/>, the exception <see cref="IParsable{TSelf}.Parse"/> documents for text
/// it refuses, so code written against that contract catches a value object's rejection, and a framework that
/// recognizes a <see cref="FormatException"/> as bad input, such as ASP.NET Core MVC binding through a type
/// converter, reports it as such. Every path throws this one type, including <c>Create</c> and the other paths
/// that take a value rather than text. A <c>catch (FormatException)</c> therefore catches it too, and placed
/// before a <c>catch (ValueObjectException)</c> of the same <c>try</c>, makes that clause unreachable (CS0160).
/// </para>
/// <para>
/// The message names the value object and the rule, never the rejected value, so a log recording it records no IBAN,
/// email address or telephone number. <see cref="AttemptedValue"/> does hold the value, except on a value object its
/// author classifies as sensitive data: see that property.
/// </para>
/// </remarks>
[Serializable]
public class ValueObjectException : FormatException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectException"/> class.
    /// </summary>
    public ValueObjectException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectException"/> class.
    /// </summary>
    /// <param name="message">Message describing the violated rule.</param>
    public ValueObjectException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectException"/> class.
    /// </summary>
    /// <param name="message">Message describing the violated rule.</param>
    /// <param name="innerException">Cause of this exception.</param>
    public ValueObjectException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectException"/> class.
    /// </summary>
    /// <param name="message">Message describing the violated rule.</param>
    /// <param name="valueObjectType">Value object type that rejected the value.</param>
    /// <param name="errorCode">Stable, machine-readable code of the violated rule.</param>
    /// <param name="attemptedValue">Value that was rejected.</param>
    public ValueObjectException(string message, Type? valueObjectType, string? errorCode, object? attemptedValue)
        : base(message)
    {
        ValueObjectType = valueObjectType;
        ErrorCode = errorCode;
        AttemptedValue = attemptedValue;
    }

    /// <summary>
    /// Gets the value object type that rejected the value.
    /// </summary>
    public Type? ValueObjectType { get; }

    /// <summary>
    /// Gets the stable, machine-readable code of the violated rule.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets the value that was rejected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value as it was passed in, before normalization, and the text for <c>Parse</c>. Unlike the message, it holds
    /// the value itself: a logger that records the public properties of an exception, as Serilog.Exceptions does,
    /// records it in clear.
    /// </para>
    /// <para>
    /// The generated <c>Create</c> and <c>Parse</c> leave it <see langword="null"/> on a value object that carries an
    /// attribute derived from <c>Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute</c>, other
    /// than <c>NoDataClassificationAttribute</c>, and so does the type converter of a generic one: classify a value
    /// object that holds personal data.
    /// </para>
    /// </remarks>
    public object? AttemptedValue { get; }

    /// <summary>
    /// Throws a <see cref="ValueObjectException"/> describing a rejected value.
    /// </summary>
    /// <param name="valueObjectType">Value object type that rejected the value.</param>
    /// <param name="errorCode">Stable, machine-readable code of the violated rule.</param>
    /// <param name="errorMessage">Human-readable description of the violated rule.</param>
    /// <param name="attemptedValue">Value that was rejected.</param>
    /// <exception cref="ValueObjectException">Always.</exception>
    [DoesNotReturn]
    public static void Throw(Type valueObjectType, string errorCode, string errorMessage, object? attemptedValue)
    {
        ArgumentNullException.ThrowIfNull(valueObjectType);

        throw new ValueObjectException(
            $"'{valueObjectType.Name}' rejected the supplied value: {errorMessage}",
            valueObjectType,
            errorCode,
            attemptedValue);
    }
}
