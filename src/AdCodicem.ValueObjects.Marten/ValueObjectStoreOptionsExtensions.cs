using System.Diagnostics.CodeAnalysis;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Metadata;
using Marten;
using Marten.Services;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// Teaches a Marten document store the value objects its documents hold.
/// </summary>
/// <remarks>
/// <para>
/// Marten stores a value object as the bare value its generated converter writes, but its LINQ knows none: a comparison
/// throws, <c>.Value</c> reads a path no document has, and duplicated fields refuse the type. <c>UseValueObjects()</c> and
/// <c>UseValueObject&lt;TSelf, TValue&gt;()</c> hand Marten its own value-type and collection members for each value
/// object, closed through the descriptor's visitor, add each one's nullable type to the value types Marten's duplicated
/// fields read, and put <see cref="ValueObjectJsonConverterFactory"/> into the store's System.Text.Json serializer.
/// </para>
/// <para>
/// On bare <see cref="StoreOptions"/>, call them after <c>UseSystemTextJsonForSerialization</c>, which replaces the
/// serializer. <c>AddMarten(…).UseValueObjects()</c> applies them after <c>AddMarten</c>'s own configuration, whatever the
/// order of the calls inside it; a <c>ConfigureMarten</c> registered after it runs later, and replacing the serializer
/// there drops the factory.
/// </para>
/// </remarks>
public static class ValueObjectStoreOptionsExtensions
{
    /// <summary>
    /// Teaches Marten every value object the registry describes: its LINQ, its duplicated fields and the collections of its
    /// compiled queries see the underlying value, and its System.Text.Json serializer writes it.
    /// </summary>
    /// <param name="options">The store's options.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static StoreOptions UseValueObjects(this StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var members = Prepare(options);
        members.FromRegistry = true;

        // What the registry holds now is mapped and listed before the store is built; a value object met later, in an
        // assembly whose registration had not run yet, a construction or one written by hand, when LINQ meets it.
        foreach (var descriptor in ValueObjectRegistry.GetRegistered())
        {
            members.Describe(descriptor).AddTo(options);
        }

        return options;
    }

    /// <summary>
    /// Teaches Marten one value object without the registry, as native AOT needs for a construction of a generic one or a
    /// value object written by hand.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="options">The store's options.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static StoreOptions UseValueObject<TSelf, TValue>(this StoreOptions options)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(options);

        Prepare(options).Add(new MartenValueObject<TSelf, TValue>()).AddTo(options);
        return options;
    }

    /// <summary>
    /// Declares a value object as the identity of a document type, keeping what Marten reflects over in a native image.
    /// </summary>
    /// <typeparam name="TDocument">The document type.</typeparam>
    /// <typeparam name="TSelf">The value object its identity is.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="options">The store's options.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static StoreOptions UseValueObjectId<TDocument,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicMethods
            | DynamicallyAccessedMemberTypes.PublicConstructors)] TSelf, TValue>(this StoreOptions options)
        where TDocument : notnull
        where TSelf : struct, IValueObject<TSelf, TValue>
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.RegisterValueTypeId<TDocument, TSelf, TValue>();
    }

    /// <summary>
    /// Inserts, once per store, the package's member source ahead of Marten's own, its parser of <c>Contains</c> and
    /// <c>IsOneOf</c>, and the converter factory into the serializer.
    /// </summary>
    /// <param name="options">The store's options.</param>
    /// <returns>The store's member source.</returns>
    private static ValueObjectMembers Prepare(StoreOptions options)
    {
        if (options.Linq.MemberSources.OfType<ValueObjectMembers>().FirstOrDefault() is not { } members)
        {
            members = new ValueObjectMembers();
            options.Linq.MemberSources.Insert(0, members);
            options.Linq.MethodCallParsers.Insert(0, new ValueObjectContainsParser(members));
        }

        // A serializer configured after this call replaces this one, and loses the factory with it: the remarks say so.
        if (options.Serializer() is SystemTextJsonSerializer serializer)
        {
            serializer.Configure(static json =>
            {
                if (!json.Converters.OfType<ValueObjectJsonConverterFactory>().Any())
                {
                    json.Converters.Insert(0, new ValueObjectJsonConverterFactory());
                }
            });
        }

        return members;
    }
}
