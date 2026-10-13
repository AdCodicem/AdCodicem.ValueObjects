using AdCodicem.ValueObjects.AspNetCore;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// Looks up the message of a refused value object in the resource an application names through
/// <see cref="ValueObjectMessageOptions"/>.
/// </summary>
/// <remarks>
/// The part of the lookup the three ASP.NET Core packages share through <see cref="ValueObjectMessageOptions"/>, which
/// only they reference, the minimal API one, the MVC one and the Azure Functions ASP.NET Core one: linked into the three
/// beside <c>ValueObjectLocalizedMessages.cs</c>, which the HotChocolate package links alone.
/// </remarks>
internal static partial class ValueObjectLocalizedMessages
{
    /// <summary>
    /// Looks up the message of a value object a typed integration refused, described by its own schema.
    /// </summary>
    /// <typeparam name="TSelf">The value object type.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="options">The messages the application configured, or <see langword="null"/> for none.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="code">The code of the rule.</param>
    /// <param name="member">The member the refusal is listed under, quoted as <c>{0}</c>.</param>
    /// <returns>The message, or <see langword="null"/> to keep the English one.</returns>
    /// <exception cref="InvalidOperationException">The request's services hold no <c>IStringLocalizerFactory</c>.</exception>
    public static string? Find<TSelf, TValue>(ValueObjectMessageOptions? options, IServiceProvider services, string code, string member)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => options?.ResourceType is { } resource ? Find<TSelf, TValue>(resource, services, code, member) : null;
}
