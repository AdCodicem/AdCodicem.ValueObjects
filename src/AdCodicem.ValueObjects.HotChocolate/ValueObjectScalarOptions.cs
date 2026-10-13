using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.HotChocolate;

/// <summary>
/// Names, specifications and messages of the scalars <c>AddValueObjects()</c> gives value objects in one schema.
/// </summary>
/// <remarks>
/// <para>
/// A scalar is named by the schema's naming conventions, as HotChocolate names the type it would otherwise infer:
/// <c>Iban</c>, <c>ReferenceOfOrder</c> for <c>Reference&lt;Order&gt;</c>, or the name a <c>[GraphQLName]</c> on the
/// struct gives. Two value objects of one name fail the schema at start, naming the second: <see cref="Name{TSelf}"/>
/// names one from the composition root, and the name passed to <c>AddValueObjectScalar</c> wins over both.
/// </para>
/// <para>
/// Every call of <c>AddValueObjects()</c> on one schema configures these same options, in the order of the calls.
/// </para>
/// </remarks>
public sealed class ValueObjectScalarOptions
{
    private readonly Dictionary<Type, string> _names = [];
    private readonly Dictionary<Type, string> _specifications = [];

    /// <summary>
    /// Gets the type whose resources hold the messages of refused values, or <see langword="null"/> to keep the English
    /// messages.
    /// </summary>
    public Type? ResourceType { get; private set; }

    /// <summary>
    /// Names the scalar of a value object, where two value objects share a name.
    /// </summary>
    /// <typeparam name="TSelf">The value object, or a construction of a generic one.</typeparam>
    /// <param name="name">The name of its scalar, a GraphQL name.</param>
    /// <returns>These options, so calls can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/>, empty or white space.</exception>
    /// <remarks>HotChocolate refuses a name that is not a GraphQL name when it builds the schema.</remarks>
    public ValueObjectScalarOptions Name<TSelf>(string name)
        where TSelf : struct, IValueObject
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _names[typeof(TSelf)] = name;
        return this;
    }

    /// <summary>
    /// Publishes the URL of the specification of a value object's format, as <c>@specifiedBy</c>.
    /// </summary>
    /// <typeparam name="TSelf">The value object, or a construction of a generic one.</typeparam>
    /// <param name="url">The absolute URL of a specification readers of the schema can follow.</param>
    /// <returns>These options, so calls can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="url"/> is not an absolute URL.</exception>
    /// <remarks>
    /// None is published by default: HotChocolate's own scalars point to specifications of their wire forms, and no
    /// built-in one describes a value object's.
    /// </remarks>
    public ValueObjectScalarOptions SpecifiedBy<TSelf>(string url)
        where TSelf : struct, IValueObject
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new ArgumentException($"'{url}' is not an absolute URL.", nameof(url));
        }

        _specifications[typeof(TSelf)] = url;
        return this;
    }

    /// <summary>
    /// Reads the messages of refused values from the resources of <typeparamref name="TResource"/>, by the key of the
    /// rule's code.
    /// </summary>
    /// <typeparam name="TResource">
    /// The type the resources are named after, as <c>IStringLocalizer&lt;T&gt;</c> names them: an empty class of the
    /// application, beside a <c>.resx</c> file of the same name for each culture.
    /// </typeparam>
    /// <returns>These options, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// An error filter of the schema looks each refusal up under the keys of <see cref="ValueObjectMessages.KeysFor"/>,
    /// in the current UI culture, and formats it in the current culture with the argument, the input field or the
    /// variable the refusal concerns as <c>{0}</c>, or the scalar's name for a variable's default value, which
    /// HotChocolate's error names no variable for, followed by the arguments
    /// <see cref="ValueObjectMessages.ArgumentsFor{TSelf, TValue}"/> reads from the type's rules. A key with no entry keeps
    /// the English message, and the code stays the rule's.
    /// </para>
    /// <para>
    /// The localizer comes from the application's services, which <c>services.AddLocalization()</c> registers; without
    /// it, the request executor fails to start, naming the call.
    /// </para>
    /// </remarks>
    public ValueObjectScalarOptions UseResource<TResource>() => UseResource(typeof(TResource));

    /// <summary>
    /// Reads the messages of refused values from the resources of a type.
    /// </summary>
    /// <param name="resourceType">The type the resources are named after.</param>
    /// <returns>These options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resourceType"/> is <see langword="null"/>.</exception>
    public ValueObjectScalarOptions UseResource(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        ResourceType = resourceType;
        return this;
    }

    /// <summary>
    /// Gets the name the application gave the scalar of a value object.
    /// </summary>
    /// <param name="valueObjectType">The value object type.</param>
    /// <returns>The name, or <see langword="null"/> to name it by the schema's conventions.</returns>
    internal string? NameOf(Type valueObjectType) => _names.GetValueOrDefault(valueObjectType);

    /// <summary>
    /// Gets the URL of the specification the application gave a value object.
    /// </summary>
    /// <param name="valueObjectType">The value object type.</param>
    /// <returns>The URL, or <see langword="null"/> for none.</returns>
    internal string? SpecificationOf(Type valueObjectType) => _specifications.GetValueOrDefault(valueObjectType);
}
