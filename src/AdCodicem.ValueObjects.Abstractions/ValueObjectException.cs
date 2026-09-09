namespace AdCodicem.ValueObjects;

/// <summary>
/// Thrown when a value object is constructed from a value that violates one of its rules.
/// </summary>
/// <remarks>
/// Every integration on a hot path (JSON, model binding, EF Core, Dapper) uses the <c>TryCreate</c> family
/// instead, so this exception is reserved for programmer errors and for the explicit <c>Create</c> entry point.
/// </remarks>
[Serializable]
public class ValueObjectException : Exception
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
