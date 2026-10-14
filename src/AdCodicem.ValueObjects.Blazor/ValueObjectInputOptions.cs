using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Blazor;

/// <summary>
/// The messages the inputs of value objects answer a refusal with: the English message of the rule, or one read from a
/// resource of the application, under the key of the rule's code.
/// </summary>
/// <remarks>
/// <para>
/// The options of <c>AddValueObjectInputs(options =&gt; …)</c>. Without a resource, a refusal keeps the English message of
/// the rule. With one, the message is looked up under the keys of <see cref="ValueObjectMessages.KeysFor"/>: the key of
/// the code, <c>ValueObject.value_object.too_short</c>, after the same key followed by <c>.minimum</c> or <c>.maximum</c>
/// for a range of one bound. It is formatted with the input's <c>DisplayName</c>, or else the name of its field, as
/// <c>{0}</c>, followed by the arguments <see cref="ValueObjectMessages.ArgumentsFor{TSelf, TValue}"/> reads from the
/// type's rules, from <c>{1}</c>. A key with no entry keeps the English message.
/// </para>
/// <para>
/// The current UI culture chooses the resource, and the current culture formats the arguments: the culture a Blazor
/// Server circuit or a Blazor WebAssembly application runs in. The localizer comes from the application's services,
/// which <c>AddValueObjectInputs</c> registers with <c>AddLocalization()</c>, keeping a registration the application made
/// first. A template quoting an argument its code does not supply is the application's error, and its
/// <see cref="FormatException"/> is not caught.
/// </para>
/// </remarks>
public sealed class ValueObjectInputOptions
{
    /// <summary>
    /// Gets the type whose resources hold the messages, or <see langword="null"/> to keep the English messages.
    /// </summary>
    public Type? ResourceType { get; private set; }

    /// <summary>
    /// Reads the messages from the resources of <typeparamref name="TResource"/>, under the key of the rule's code.
    /// </summary>
    /// <typeparam name="TResource">
    /// The type the resources are named after, as <c>IStringLocalizer&lt;T&gt;</c> names them: an empty class of the
    /// application, beside a <c>.resx</c> file of the same name for each culture.
    /// </typeparam>
    /// <returns>These options, so calls can be chained.</returns>
    public ValueObjectInputOptions UseResource<TResource>() => UseResource(typeof(TResource));

    /// <summary>
    /// Reads the messages from the resources of a type.
    /// </summary>
    /// <param name="resourceType">The type the resources are named after.</param>
    /// <returns>These options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resourceType"/> is <see langword="null"/>.</exception>
    public ValueObjectInputOptions UseResource(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        ResourceType = resourceType;

        return this;
    }
}
