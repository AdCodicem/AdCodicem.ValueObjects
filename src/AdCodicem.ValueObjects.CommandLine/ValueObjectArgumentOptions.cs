using System.Globalization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.CommandLine;

/// <summary>
/// How the options and arguments of value objects read text, and word a refusal: the culture the text is read in, and
/// the English message of the rule or one read from a resource of the application, under the key of the rule's code.
/// </summary>
/// <remarks>
/// <para>
/// The options of <c>AcceptValueObject(options =&gt; …)</c> and <c>AcceptValueObjects(options =&gt; …)</c>, read once,
/// when the call returns: changing them afterwards changes nothing.
/// </para>
/// <para>
/// The text is read in the invariant culture unless <see cref="Culture"/> names another, so that a script reads the same
/// on every machine, and the generated parsers refuse a group separator: <c>12,5</c> is refused as
/// <see cref="ValueObjectErrorCodes.NotParsable"/> rather than read as 125. System.CommandLine reads its own
/// <c>decimal</c>, <c>double</c> and date options in the current culture, group separators included.
/// </para>
/// <para>
/// Without a resource, a refusal keeps the English message of the rule, after the symbol it names:
/// <c>Option '--quantity': The value must be less than or equal to 100.</c> With one, the message is looked up under the
/// keys of <see cref="ValueObjectMessages.KeysFor"/>, in the current UI culture, through a
/// <see cref="System.Resources.ResourceManager"/> over the type, the base name MVC's and the minimal APIs'
/// <c>UseResource&lt;T&gt;()</c> read while <c>LocalizationOptions.ResourcesPath</c> is unset, so that one resource then
/// serves them all. It is formatted in the current culture with the alias of the option as it was typed, or the name of
/// the argument, as <c>{0}</c>, followed by the position of the token from zero where the symbol received several,
/// <c>--ibans[1]</c> for the second, the bare name for one, and with the arguments
/// <see cref="ValueObjectMessages.ArgumentsFor{TSelf, TValue}"/> reads from the type's rules from <c>{1}</c>. A key with
/// no entry, or a blank one, keeps the English message. A template quoting an argument its code does not supply is the
/// application's error, and its <see cref="FormatException"/> is not caught.
/// </para>
/// </remarks>
public sealed class ValueObjectArgumentOptions
{
    private CultureInfo _culture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Gets or sets the culture the text is read in, and the known values are completed in; the invariant culture unless
    /// set. A date is read in the Gregorian calendar whatever the culture's own.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _culture = value;
        }
    }

    /// <summary>
    /// Gets the type whose resources hold the messages, or <see langword="null"/> to keep the English messages.
    /// </summary>
    public Type? ResourceType { get; private set; }

    /// <summary>
    /// Reads the message of each refusal from the resources of <typeparamref name="TResource"/>, under the key of the
    /// rule's code.
    /// </summary>
    /// <typeparam name="TResource">
    /// The type the resources are named after: an empty class of the application, beside a <c>.resx</c> file of the same
    /// name for each culture.
    /// </typeparam>
    /// <returns>These options, so calls can be chained.</returns>
    public ValueObjectArgumentOptions UseResource<TResource>() => UseResource(typeof(TResource));

    /// <summary>
    /// Reads the message of each refusal from the resources of a type, under the key of the rule's code.
    /// </summary>
    /// <param name="resourceType">The type the resources are named after.</param>
    /// <returns>These options, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resourceType"/> is <see langword="null"/>.</exception>
    public ValueObjectArgumentOptions UseResource(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        ResourceType = resourceType;

        return this;
    }

    /// <summary>Gets the settings a configuration gives, read once.</summary>
    /// <param name="configure">Configures the options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The settings.</returns>
    internal static ValueObjectArgumentSettings Read(Action<ValueObjectArgumentOptions>? configure)
    {
        var options = new ValueObjectArgumentOptions();
        configure?.Invoke(options);

        return new ValueObjectArgumentSettings(options.Culture, options.ResourceType);
    }
}

/// <summary>The options of a call, as they stood when it returned.</summary>
/// <param name="Culture">The culture the text is read in.</param>
/// <param name="ResourceType">The type whose resources hold the messages, or <see langword="null"/>.</param>
internal sealed record ValueObjectArgumentSettings(CultureInfo Culture, Type? ResourceType);
