using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.AspNetCore;

/// <summary>
/// Where a refused value object finds a message in the application's language: a resource read through
/// <c>IStringLocalizerFactory</c>, under the key of the rule's code.
/// </summary>
/// <remarks>
/// <para>
/// The options of <c>WithValueObjectProblemDetails(messages =&gt; …)</c> on a minimal API, and of
/// <c>AddValueObjects(messages =&gt; …)</c> on MVC. Without a resource, a refusal keeps the English message of the rule.
/// With one, the message is looked up under the keys of <see cref="ValueObjectMessages.KeysFor"/>: the key of the code,
/// <c>ValueObject.value_object.too_short</c>, after the same key followed by <c>.minimum</c> or <c>.maximum</c> for a
/// range of one bound. It is formatted with the member the refusal is listed under as <c>{0}</c>, followed by the arguments
/// <see cref="ValueObjectMessages.ArgumentsFor(ValueObjectDescriptor, string, IFormatProvider?)"/> reads from the type's
/// rules, from <c>{1}</c>. A key with no entry keeps the English message.
/// </para>
/// <para>
/// The current UI culture chooses the resource, and the current culture formats the arguments: with
/// <c>UseRequestLocalization()</c>, both follow the request. The localizer comes from the request's services, which
/// <c>builder.Services.AddLocalization()</c> registers; without it, a refusal throws an
/// <see cref="InvalidOperationException"/> naming the call. A template quoting an argument its code does not supply is
/// the application's error, and its <see cref="FormatException"/> is not caught.
/// </para>
/// <para>
/// The type lives in <c>AdCodicem.ValueObjects.AspNetCore.Http</c>, beside <see cref="ValueObjectProblemDetails"/> and
/// under the same namespace, so that the minimal API package and the MVC one, which references it, share it.
/// </para>
/// </remarks>
public sealed class ValueObjectMessageOptions
{
    /// <summary>
    /// Gets the type whose resources hold the messages, or <see langword="null"/> to keep the English messages.
    /// </summary>
    public Type? ResourceType { get; private set; }

    /// <summary>
    /// Reads the messages from the resources of <typeparamref name="TResource"/>.
    /// </summary>
    /// <typeparam name="TResource">
    /// The type the resources are named after, as <c>IStringLocalizer&lt;T&gt;</c> names them: an empty class of the
    /// application, beside a <c>.resx</c> file of the same name for each culture.
    /// </typeparam>
    /// <returns>These options, so calls can be chained.</returns>
    public ValueObjectMessageOptions UseResource<TResource>() => UseResource(typeof(TResource));

    /// <summary>
    /// Reads the messages from the resources of a type.
    /// </summary>
    /// <param name="resourceType">The type the resources are named after.</param>
    /// <returns>These options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resourceType"/> is <see langword="null"/>.</exception>
    public ValueObjectMessageOptions UseResource(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        ResourceType = resourceType;

        return this;
    }
}
