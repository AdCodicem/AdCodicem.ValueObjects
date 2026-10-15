using System.Collections.Concurrent;
using AdCodicem.ValueObjects.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AdCodicem.ValueObjects.NewtonsoftJson;

/// <summary>
/// Resolves each value object, and its nullable type, to a contract carrying <see cref="ValueObjectConverter"/>, and
/// every other type through the resolver it wraps.
/// </summary>
/// <remarks>
/// <para>
/// It is for code that hands Newtonsoft.Json a contract resolver alone, with no serializer settings to add the
/// converter to: settings take <see cref="ValueObjectJsonSerializerSettingsExtensions.AddValueObjects"/> instead, and
/// neither that call nor <c>AddValueObjectsNewtonsoftJson()</c> wraps a resolver.
/// </para>
/// <para>
/// A type <see cref="ValueObjectConverter.CanConvert"/> claims, which <see cref="ValueObjectRegistry.IsValueObject"/>
/// tells, nullable or not, a construction of a generic value object and a value object written by hand included, gets a
/// <see cref="JsonStringContract"/> of this resolver's own whose <see cref="JsonContract.Converter"/> is the converter,
/// kept for the resolver's lifetime. A contract of the wrapped resolver that carries a converter already, as one a
/// <c>[JsonConverter]</c> on the type names, is answered as it is. The wrapped resolver's contracts are never changed:
/// its cache may be shared by every instance of its type, as <see cref="CamelCasePropertyNamesContractResolver"/>'s is.
/// </para>
/// <para>
/// The contract of a type holding value objects stays the wrapped resolver's, and Newtonsoft.Json keeps the contract
/// of each of its members on it, from the first time a serializer reads or writes that member. A serializer over this
/// resolver and another over the same wrapped resolver therefore see each other's member contracts, in whichever order
/// they first run. Wrap a resolver no other serializer uses, as the parameterless constructor does: never MVC's default
/// one, which is a single instance shared by every set of MVC's serializer settings in the process.
/// </para>
/// <para>
/// A contract's converter wins over the converters the serializer settings name: an application's own converter for a
/// value object in <see cref="JsonSerializerSettings.Converters"/> loses to this one. Name it on the type with
/// <c>[JsonConverter]</c> instead.
/// </para>
/// <para>
/// Given to a JSON Patch document of Microsoft.AspNetCore.JsonPatch as its <c>ContractResolver</c>, it is what the
/// document converts a value with. JSON Patch reads the value again under settings of its own, which drop
/// <see cref="DateParseHandling.None"/> and <see cref="FloatParseHandling.Decimal"/>, and replaces the converter's
/// refusal with a message of its own, which quotes the value and carries no code. In MVC,
/// <c>ValueObjectJsonPatchExtensions.TryApplyTo</c>, in AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson, reads every
/// value through the rules, with their codes, and needs no resolver.
/// </para>
/// </remarks>
public sealed class ValueObjectContractResolver : IContractResolver
{
    private static readonly ValueObjectConverter Converter = new();

    private readonly ConcurrentDictionary<Type, JsonContract> _contracts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectContractResolver"/> class, wrapping a new
    /// <see cref="DefaultContractResolver"/> of its own.
    /// </summary>
    public ValueObjectContractResolver()
        : this(new DefaultContractResolver())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectContractResolver"/> class, wrapping a resolver, which
    /// every type but a value object keeps.
    /// </summary>
    /// <param name="inner">The resolver of every other type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public ValueObjectContractResolver(IContractResolver inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        Inner = inner;
    }

    /// <summary>
    /// Gets the resolver it wraps.
    /// </summary>
    public IContractResolver Inner { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    public JsonContract ResolveContract(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return Converter.CanConvert(type)
            ? _contracts.GetOrAdd(type, static (candidate, inner) => Own(candidate, inner), Inner)
            : Inner.ResolveContract(type);
    }

    /// <summary>
    /// Gives the contract of a value object: the wrapped resolver's, when it carries a converter already; otherwise
    /// one of this resolver's own, so that the wrapped resolver's is never changed.
    /// </summary>
    /// <param name="type">The value object, or its nullable type.</param>
    /// <param name="inner">The wrapped resolver.</param>
    /// <returns>The contract.</returns>
    private static JsonContract Own(Type type, IContractResolver inner)
    {
        var contract = inner.ResolveContract(type);

        return contract.Converter is not null ? contract : new JsonStringContract(type) { Converter = Converter };
    }
}
