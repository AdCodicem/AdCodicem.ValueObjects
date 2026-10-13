using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// What <see cref="ParsesAsAttribute"/> and <see cref="NotDefaultAttribute"/> share: finding the descriptor of a value
/// object they name or check, and the message of a refusal in the resource an attribute names.
/// </summary>
internal static class ValueObjectValidationAttributes
{
    /// <summary>
    /// <see cref="RequiredAttribute"/>'s message, which <see cref="NotDefaultAttribute"/> refuses with unless it declares
    /// another.
    /// </summary>
    internal const string RequiredMessage = "The {0} field is required.";

    /// <summary>DataAnnotations' default message, for a refusal no other message is at hand for, <c>{0}</c> the member.</summary>
    private static readonly CompositeFormat Invalid = CompositeFormat.Parse("The field {0} is invalid.");

    /// <summary><see cref="RequiredMessage"/>, parsed once.</summary>
    private static readonly CompositeFormat Required = CompositeFormat.Parse(RequiredMessage);

    /// <summary>The resource managers of the resource types attributes named, one per type, shared by every attribute.</summary>
    private static readonly ConcurrentDictionary<Type, ResourceManager> Managers = new();

    /// <summary>
    /// Finds the descriptor of a value object: the one registered, or, where the runtime supports dynamic code, one the
    /// registry describes by reflection.
    /// </summary>
    /// <param name="valueObjectType">The value object type.</param>
    /// <param name="attribute">The attribute asking, as the message names it: <c>[ParsesAs] names</c>, <c>[NotDefault] checks</c>.</param>
    /// <param name="alternative">What else the message suggests, after the registration, or the empty text.</param>
    /// <returns>Its descriptor.</returns>
    /// <exception cref="InvalidOperationException">The type is not a value object the registry describes.</exception>
    /// <remarks>
    /// This is <c>MustParseAs</c>'s lookup, made when a value is first validated rather than when the attribute is
    /// created, since anything reading a member's attributes creates them, a document generator among them. Under native
    /// AOT, where the reflection <see cref="ValueObjectRegistry.TryResolve"/> needs is not available, the call is not made:
    /// a generated value object is registered already, and a value object written by hand, or a construction of a generic
    /// one, is registered by hand, or named through the type arguments of <see cref="ParsesAsAttribute{TSelf, TValue}"/>.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Only reached for a value object nothing registered: one written by hand, or a construction of a "
                        + "generic one, whose type the attribute names, which keeps it. Under native AOT, where dynamic code "
                        + "is not supported, the registered descriptor is the only one.")]
    internal static ValueObjectDescriptor Resolve(Type valueObjectType, string attribute, string alternative)
    {
        if (ValueObjectRegistry.TryGet(valueObjectType, out var descriptor)
            || (RuntimeFeature.IsDynamicCodeSupported && ValueObjectRegistry.TryResolve(valueObjectType, out descriptor)))
        {
            return descriptor;
        }

        throw new InvalidOperationException(
            $"{attribute} '{valueObjectType.Name}', which is not a value object the registry describes. Under native AOT, a "
            + "value object written by hand, ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), and a construction "
            + "of a generic one, ValueObjectRegistry.Register<TSelf, TValue>(static () => new TSelf.ValueJsonConverter()), "
            + $"have to be registered before a value is validated{alternative}.");
    }

    /// <summary>
    /// Tells whether an attribute names a resource type alone, which looks the message of a refusal up by the code of the
    /// rule, rather than DataAnnotations' single message.
    /// </summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="resource">The resource type, when it names one alone.</param>
    /// <returns><see langword="true"/> when the message is looked up by code.</returns>
    internal static bool NamesResourceAlone(ValidationAttribute attribute, [NotNullWhen(true)] out Type? resource)
    {
        resource = attribute.ErrorMessageResourceType;

        return resource is not null
               && string.IsNullOrEmpty(attribute.ErrorMessageResourceName)
               && string.IsNullOrEmpty(attribute.ErrorMessage);
    }

    /// <summary>
    /// Gets the name a refusal quotes as <c>{0}</c>: the display name of the context, which falls back on the member's and
    /// the type's, or the value object's when there is no context.
    /// </summary>
    /// <param name="context">The context of the validation, which <see cref="ValidationAttribute.IsValid(object?)"/>
    /// leaves <see langword="null"/>.</param>
    /// <param name="valueObjectType">The value object type.</param>
    /// <returns>The name.</returns>
    internal static string DisplayName(ValidationContext? context, Type valueObjectType)
        => context?.DisplayName ?? valueObjectType.Name.Split('`')[0];

    /// <summary>Formats DataAnnotations' default message for a member.</summary>
    /// <param name="name">The member.</param>
    /// <returns>The message.</returns>
    internal static string InvalidMessage(string name) => string.Format(CultureInfo.CurrentCulture, Invalid, name);

    /// <summary>Formats <see cref="RequiredAttribute"/>'s message for a member.</summary>
    /// <param name="name">The member.</param>
    /// <returns>The message.</returns>
    internal static string RequiredMessageFor(string name) => string.Format(CultureInfo.CurrentCulture, Required, name);

    /// <summary>
    /// Gets the members a refusal is reported under: the member validated, or none.
    /// </summary>
    /// <param name="context">The context of the validation.</param>
    /// <returns>The member names, or <see langword="null"/>.</returns>
    internal static string[]? MemberNames(ValidationContext? context)
        => context?.MemberName is { } member ? [member] : null;

    /// <summary>
    /// Looks the message of a refusal up in a resource, under each key in turn, in the current UI culture, formatted in the
    /// current culture.
    /// </summary>
    /// <param name="resource">The resource type the attribute names.</param>
    /// <param name="keys">The keys, the most specific first.</param>
    /// <param name="arguments">Gets the arguments of the rule, quoted from <c>{1}</c>; asked only for a message found.</param>
    /// <param name="member">The member, quoted as <c>{0}</c>.</param>
    /// <returns>The message, or <see langword="null"/> when the resource holds none for the culture.</returns>
    /// <remarks>
    /// The resource is read through a <see cref="ResourceManager"/> over the type, the base name <c>IStringLocalizerFactory</c>
    /// reads while <c>LocalizationOptions.ResourcesPath</c> is unset, so that the resource an application hands MVC's and
    /// the minimal APIs' <c>UseResource&lt;T&gt;()</c> serves these attributes too, with no service provider. A resource
    /// with no neutral culture throws <see cref="MissingManifestResourceException"/> for a key its cultures lack, as it
    /// falls back to the neutral one, which the framework's localizer swallows too: the next key is looked up, and the
    /// English message stands when none is found. A template quoting an argument its code does not supply throws
    /// <see cref="FormatException"/>, the application's error to see.
    /// </remarks>
    internal static string? Find(
        Type resource,
        IReadOnlyList<string> keys,
        Func<IReadOnlyList<KeyValuePair<string, object?>>> arguments,
        string member)
    {
        var manager = Managers.GetOrAdd(resource, static type => new ResourceManager(type));
        foreach (var key in keys)
        {
            string? template;
            try
            {
                template = manager.GetString(key, CultureInfo.CurrentUICulture);
            }
            catch (MissingManifestResourceException)
            {
                continue;
            }

            if (template is null)
            {
                continue;
            }

            var supplied = arguments();
            var positional = new object?[supplied.Count + 1];
            positional[0] = member;
            for (var index = 0; index < supplied.Count; index++)
            {
                positional[index + 1] = supplied[index].Value;
            }

            return string.Format(CultureInfo.CurrentCulture, template, positional);
        }

        return null;
    }
}
