namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// The result DataAnnotations reports a refusal of <see cref="ParsesAsAttribute"/> or <see cref="NotDefaultAttribute"/>
/// with, carrying the code of the rule and the value object type beside the message.
/// </summary>
/// <remarks>
/// <para>
/// A host that hands the result back keeps the code: <c>Validator.TryValidateObject</c>, <c>TryValidateProperty</c> and
/// <c>TryValidateValue</c> list it as it is, and so does CommunityToolkit.Mvvm's <c>ObservableValidator.GetErrors</c>. A
/// host that keeps the message alone loses it: MVC's model state, the errors of <c>AddValidation()</c>, the failures of
/// options validation. The message never carries the code, nor the value refused.
/// </para>
/// <para>
/// It is the <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/> of DataAnnotations, not the
/// <see cref="ValidationResult"/> a value object's rules return, which is a struct and allocates nothing.
/// </para>
/// </remarks>
public sealed class ValueObjectValidationResult : System.ComponentModel.DataAnnotations.ValidationResult
{
    /// <summary>
    /// Creates the result of a refusal.
    /// </summary>
    /// <param name="errorMessage">The message, as the host shows it.</param>
    /// <param name="memberNames">The members the refusal is reported under, or <see langword="null"/> for none.</param>
    /// <param name="valueObjectType">The value object whose rule refused the value.</param>
    /// <param name="errorCode">The code of the rule, one of <see cref="ValueObjectErrorCodes"/> or an application's own.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="valueObjectType"/> or <paramref name="errorCode"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is empty.</exception>
    public ValueObjectValidationResult(string errorMessage, IEnumerable<string>? memberNames, Type valueObjectType, string errorCode)
        : base(errorMessage, memberNames)
    {
        ArgumentNullException.ThrowIfNull(valueObjectType);
        ArgumentException.ThrowIfNullOrEmpty(errorCode);

        ValueObjectType = valueObjectType;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the value object whose rule refused the value.
    /// </summary>
    public Type ValueObjectType { get; }

    /// <summary>
    /// Gets the code of the rule that refused the value, one of <see cref="ValueObjectErrorCodes"/> or an application's
    /// own.
    /// </summary>
    public string ErrorCode { get; }
}
