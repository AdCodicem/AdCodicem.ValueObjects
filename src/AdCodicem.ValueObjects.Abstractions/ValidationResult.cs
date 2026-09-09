namespace AdCodicem.ValueObjects;

/// <summary>
/// Outcome of validating a candidate underlying value for a value object.
/// </summary>
/// <remarks>
/// This is a <see langword="readonly struct"/> whose successful state is <see langword="default"/>, so the
/// success path never allocates. Validation is fail-fast: the first violated rule wins and only that rule is
/// reported. Aggregating several errors across several members is the responsibility of a higher level
/// validator (see the FluentValidation integration package).
/// </remarks>
public readonly struct ValidationResult : IEquatable<ValidationResult>
{
    private readonly string? _errorCode;
    private readonly string? _errorMessage;

    private ValidationResult(string errorCode, string errorMessage)
    {
        _errorCode = errorCode;
        _errorMessage = errorMessage;
    }

    /// <summary>
    /// Gets the successful result. Equivalent to <see langword="default"/>.
    /// </summary>
    public static ValidationResult Success => default;

    /// <summary>
    /// Gets a value indicating whether the candidate value satisfies every rule.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorCode))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsValid => _errorCode is null;

    /// <summary>
    /// Gets the stable, machine-readable code of the violated rule, or <see langword="null"/> when valid.
    /// </summary>
    /// <remarks>Well-known codes are listed on <see cref="ValueObjectErrorCodes"/>.</remarks>
    public string? ErrorCode => _errorCode;

    /// <summary>
    /// Gets the human-readable description of the violated rule, or <see langword="null"/> when valid.
    /// </summary>
    public string? ErrorMessage => _errorMessage;

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorCode">Stable, machine-readable code of the violated rule.</param>
    /// <param name="errorMessage">Human-readable description of the violated rule.</param>
    /// <returns>A failed <see cref="ValidationResult"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is <see langword="null"/> or empty.</exception>
    public static ValidationResult Failure(string errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrEmpty(errorCode);
        ArgumentNullException.ThrowIfNull(errorMessage);

        return new ValidationResult(errorCode, errorMessage);
    }

    /// <summary>
    /// Creates a failed result carrying the <see cref="ValueObjectErrorCodes.Required"/> code.
    /// </summary>
    /// <param name="errorMessage">Human-readable description of the violated rule.</param>
    /// <returns>A failed <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Required(string errorMessage = "The value is required.")
        => Failure(ValueObjectErrorCodes.Required, errorMessage);

    /// <summary>
    /// Creates a failed result carrying the <see cref="ValueObjectErrorCodes.InvalidFormat"/> code.
    /// </summary>
    /// <param name="errorMessage">Human-readable description of the violated rule.</param>
    /// <returns>A failed <see cref="ValidationResult"/>.</returns>
    public static ValidationResult InvalidFormat(string errorMessage = "The value has an invalid format.")
        => Failure(ValueObjectErrorCodes.InvalidFormat, errorMessage);

    /// <summary>
    /// Creates a failed result carrying the <see cref="ValueObjectErrorCodes.OutOfRange"/> code.
    /// </summary>
    /// <param name="errorMessage">Human-readable description of the violated rule.</param>
    /// <returns>A failed <see cref="ValidationResult"/>.</returns>
    public static ValidationResult OutOfRange(string errorMessage = "The value is out of range.")
        => Failure(ValueObjectErrorCodes.OutOfRange, errorMessage);

    /// <summary>
    /// Throws a <see cref="ValueObjectException"/> when this result is a failure.
    /// </summary>
    /// <param name="valueObjectType">Value object type the validation was performed for.</param>
    /// <param name="attemptedValue">Value that was rejected, surfaced on the exception for diagnostics.</param>
    /// <exception cref="ValueObjectException">This result is a failure.</exception>
    public void ThrowIfInvalid(Type valueObjectType, object? attemptedValue)
    {
        if (!IsValid)
        {
            ValueObjectException.Throw(valueObjectType, ErrorCode, ErrorMessage, attemptedValue);
        }
    }

    /// <inheritdoc />
    public bool Equals(ValidationResult other)
        => string.Equals(_errorCode, other._errorCode, StringComparison.Ordinal)
           && string.Equals(_errorMessage, other._errorMessage, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValidationResult other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_errorCode, _errorMessage);

    /// <inheritdoc />
    public override string ToString() => IsValid ? "Valid" : $"{_errorCode}: {_errorMessage}";

    /// <summary>Determines whether two results are equal.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when both results are equal.</returns>
    public static bool operator ==(ValidationResult left, ValidationResult right) => left.Equals(right);

    /// <summary>Determines whether two results differ.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when both results differ.</returns>
    public static bool operator !=(ValidationResult left, ValidationResult right) => !left.Equals(right);
}
