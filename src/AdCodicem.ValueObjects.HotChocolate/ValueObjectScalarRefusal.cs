using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.HotChocolate;

/// <summary>
/// The value object exception a scalar attaches to the GraphQL error of a value it refused: the type and the code, never
/// the value, and the typed lookup of the message in the application's language.
/// </summary>
/// <remarks>
/// HotChocolate never serializes it; an error filter reads it on <c>IError.Exception</c>, the package's own and an
/// application's alike.
/// </remarks>
internal abstract class ValueObjectScalarRefusal : ValueObjectException
{
    /// <summary>
    /// Initializes the exception.
    /// </summary>
    /// <param name="message">The message of the rule.</param>
    /// <param name="valueObjectType">The value object type.</param>
    /// <param name="errorCode">The code of the rule.</param>
    /// <param name="scalarName">The name of the scalar, quoted where no argument or variable names the value.</param>
    /// <param name="literal">Whether the value refused is a literal of the document, rather than JSON or a result.</param>
    protected ValueObjectScalarRefusal(string message, Type valueObjectType, string errorCode, string scalarName, bool literal)
        : base(message, valueObjectType, errorCode, attemptedValue: null)
    {
        ScalarName = scalarName;
        Literal = literal;
    }

    /// <summary>
    /// Gets the name of the scalar that refused the value.
    /// </summary>
    public string ScalarName { get; }

    /// <summary>
    /// Gets whether the value refused is a literal of the document: an argument's, an input field's, or a variable's
    /// default value, never a variable's JSON value or a result.
    /// </summary>
    public bool Literal { get; }

    /// <summary>
    /// Looks the message up in a resource of the application, by the key of the code.
    /// </summary>
    /// <param name="resource">The type whose resources hold the messages.</param>
    /// <param name="services">The application's services, which hold the localizer factory.</param>
    /// <param name="member">The argument or the variable the refusal concerns, quoted as <c>{0}</c>.</param>
    /// <returns>The message, or <see langword="null"/> to keep the English one.</returns>
    public abstract string? Localize(Type resource, IServiceProvider services, string member);
}

/// <summary>
/// The refusal of one value object type, which reads its rules through <typeparamref name="TSelf"/>.
/// </summary>
/// <typeparam name="TSelf">The value object type.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
internal sealed class ValueObjectScalarRefusal<TSelf, TValue> : ValueObjectScalarRefusal
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Initializes the exception.
    /// </summary>
    /// <param name="message">The message of the rule.</param>
    /// <param name="errorCode">The code of the rule.</param>
    /// <param name="scalarName">The name of the scalar.</param>
    /// <param name="literal">Whether the value refused is a literal of the document.</param>
    public ValueObjectScalarRefusal(string message, string errorCode, string scalarName, bool literal)
        : base(message, typeof(TSelf), errorCode, scalarName, literal)
    {
    }

    /// <inheritdoc />
    public override string? Localize(Type resource, IServiceProvider services, string member)
        => ValueObjectLocalizedMessages.Find<TSelf, TValue>(resource, services, ErrorCode!, member);
}
