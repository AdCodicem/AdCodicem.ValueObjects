using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// Looks up the message of a refused value object in a resource the application names, by the key of the rule's code,
/// with the arguments the type's rules supply.
/// </summary>
/// <remarks>
/// <para>
/// The localizer comes from <c>IStringLocalizerFactory</c>, in the request's services or the application's, which
/// looks the template up in the current UI culture and formats it with
/// <see cref="string.Format(IFormatProvider, string, object[])"/> in the current culture: <c>{0}</c> is the member the
/// refusal is listed under, and the arguments <see cref="ValueObjectMessages"/> reads from the schema follow from
/// <c>{1}</c>, formatted in the current culture too.
/// The first key of <see cref="ValueObjectMessages.KeysFor"/> the resource holds gives the message; none, and the caller
/// keeps the English message of the rule. A template quoting an argument its code does not supply throws the localizer's
/// <see cref="FormatException"/>, which is the application's error to see, not to hide behind the English text.
/// </para>
/// <para>
/// This file is linked into the four packages that localize a refusal: the three that answer one on ASP.NET Core, the
/// minimal API one, the MVC one and the Azure Functions ASP.NET Core one, which link
/// <c>ValueObjectLocalizedMessages.AspNetCore.cs</c> beside it for the options they share, and the HotChocolate one, which
/// links this file alone. Each compiles its own copy, as the AI and the Model Context Protocol packages each compile
/// <c>ValueObjectArguments.cs</c>. It reads no annotation and closes no generic type at run time, so it runs in a native
/// binary.
/// </para>
/// </remarks>
internal static partial class ValueObjectLocalizedMessages
{
    private const string NoLocalization =
        "The messages of refused value objects are read from a resource, through the IStringLocalizerFactory of the "
        + "request's services, which this application does not register. Call builder.Services.AddLocalization().";

    /// <summary>
    /// Looks up the message of a value object a typed integration refused, described by its own schema, in a resource the
    /// application named.
    /// </summary>
    /// <typeparam name="TSelf">The value object type.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="resource">The type whose resources hold the messages.</param>
    /// <param name="services">The services the localizer factory is resolved from.</param>
    /// <param name="code">The code of the rule.</param>
    /// <param name="member">The member the refusal is listed under, quoted as <c>{0}</c>.</param>
    /// <returns>The message, or <see langword="null"/> to keep the English one.</returns>
    /// <exception cref="InvalidOperationException">The services hold no <c>IStringLocalizerFactory</c>.</exception>
    public static string? Find<TSelf, TValue>(Type resource, IServiceProvider services, string code, string member)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => Find(
            resource,
            services,
            ValueObjectMessages.KeysFor(TSelf.Schema, code),
            ValueObjectMessages.ArgumentsFor<TSelf, TValue>(code, CultureInfo.CurrentCulture),
            member);

    /// <summary>
    /// Looks up the message of a value object known only by its descriptor, or by nothing, in a resource the application
    /// named.
    /// </summary>
    /// <param name="resource">The type whose resources hold the messages.</param>
    /// <param name="services">The services the localizer factory is resolved from.</param>
    /// <param name="descriptor">
    /// The descriptor of the type that refused the value, or <see langword="null"/> when there is none to read: an absent
    /// value, or a type the registry does not describe, whose message has the key of the code alone and no argument.
    /// </param>
    /// <param name="code">The code of the rule.</param>
    /// <param name="member">The member the refusal is listed under, quoted as <c>{0}</c>.</param>
    /// <returns>The message, or <see langword="null"/> to keep the English one.</returns>
    /// <exception cref="InvalidOperationException">The services hold no <c>IStringLocalizerFactory</c>.</exception>
    /// <remarks>
    /// The caller tells whether the application named a resource, so that it describes the value object only when one
    /// is named.
    /// </remarks>
    public static string? Find(Type resource, IServiceProvider services, ValueObjectDescriptor? descriptor, string code, string member)
        => descriptor is null
            ? Find(resource, services, [ValueObjectMessages.KeyFor(code)], [], member)
            : Find(
                resource,
                services,
                ValueObjectMessages.KeysFor(descriptor.Schema, code),
                ValueObjectMessages.ArgumentsFor(descriptor, code, CultureInfo.CurrentCulture),
                member);

    /// <summary>
    /// Gets the name a value object is quoted under where no member names it, as in a request body: the name of its type,
    /// without the arity of a generic one, <c>Reference</c> for <c>Reference&lt;PurchaseOrder&gt;</c>.
    /// </summary>
    /// <param name="valueObjectType">The value object type.</param>
    /// <returns>Its name.</returns>
    public static string NameOf(Type valueObjectType) => valueObjectType.Name.Split('`')[0];

    /// <summary>Asks the localizer for each key in turn, and returns the first message the resource holds.</summary>
    private static string? Find(
        Type resource,
        IServiceProvider services,
        IReadOnlyList<string> keys,
        IReadOnlyList<KeyValuePair<string, object?>> arguments,
        string member)
    {
        var factory = services.GetService<IStringLocalizerFactory>() ?? throw new InvalidOperationException(NoLocalization);
        var localizer = factory.Create(resource);

        var positional = new object[arguments.Count + 1];
        positional[0] = member;
        for (var index = 0; index < arguments.Count; index++)
        {
            positional[index + 1] = arguments[index].Value!;
        }

        foreach (var key in keys)
        {
            var message = localizer[key, positional];
            if (!message.ResourceNotFound)
            {
                return message.Value;
            }
        }

        return null;
    }
}
