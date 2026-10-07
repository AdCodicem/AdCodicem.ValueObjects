using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace AdCodicem.ValueObjects.MongoDB;

/// <summary>
/// Registers the MongoDB.Driver serializers of value objects.
/// </summary>
/// <remarks>
/// <para>
/// MongoDB.Driver keeps its serializers in a process-wide registry that never forgets one, and maps a type it has no
/// serializer for through a class map. A value object has no member a class map can set, so without these serializers it
/// is written as an empty sub-document, <c>{}</c>, read back as its default instance, and a query over it compares
/// <c>{}</c>, matching every document or none. Call <see cref="Register(Assembly[])"/> once at start-up, after
/// registering any serializer of the application's own, the <see cref="GuidSerializer"/> first, and before anything is
/// serialized or any class map is built.
/// </para>
/// </remarks>
public static class ValueObjectBson
{
    private static readonly Lock Gate = new();

    /// <summary>
    /// The provider registered with the driver, or <see langword="null"/> before the first registration.
    /// </summary>
    private static ValueObjectBsonSerializationProvider? _provider;

    /// <summary>
    /// Registers, once, the provider that hands the driver a strict serializer for every value object.
    /// </summary>
    /// <param name="assemblies">
    /// Assemblies declaring value objects, whose generated registration runs first, so that their value objects are
    /// checked.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The provider is already registered with <c>trusted: true</c>; a value object over <see cref="Guid"/> is
    /// registered and the serializer of <see cref="Guid"/> has no representation; or the driver has already mapped a
    /// registered value object through a class map.
    /// </exception>
    /// <remarks>
    /// Strict, as <see cref="Register(bool, Assembly[])"/> with <c>trusted: false</c> is: every value read goes through
    /// the value object's rules, since a collection has no schema and is often written by other services, scripts or
    /// tools.
    /// </remarks>
    [RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
    public static void Register(params Assembly[] assemblies) => Register(trusted: false, assemblies);

    /// <summary>
    /// Registers, once, the provider that hands the driver a serializer for every value object, trusted or strict.
    /// </summary>
    /// <param name="trusted">
    /// Whether reads skip validation, through <c>CreateUnchecked</c>, for a collection the application alone writes;
    /// <see langword="false"/> reads through <c>TryCreate</c>, and refuses a value the value object rejects.
    /// </param>
    /// <param name="assemblies">
    /// Assemblies declaring value objects, whose generated registration runs first, so that their value objects are
    /// checked.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The provider is already registered with the other trust; a value object over <see cref="Guid"/> is registered
    /// and the serializer of <see cref="Guid"/> has no representation; or the driver has already mapped a registered value
    /// object through a class map.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The provider answers every value object the driver meets, whichever assembly declares it, with a
    /// <see cref="ValueObjectBsonSerializer{TSelf, TValue}"/> over the serializer the registry holds for its underlying
    /// type, closed at compile time through the type arguments the descriptor hands back to a visitor
    /// (<see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/>). It is registered once: a
    /// second call with the same trust registers the assemblies given and repeats the checks below, one with the other
    /// trust throws. A value object that needs the other trust, or a serializer of the application's own, is given it per
    /// type with <see cref="Register{TSelf, TValue}(bool)"/> or <see cref="BsonSerializer.RegisterSerializer(Type, IBsonSerializer)"/>,
    /// or per member, on its class map, before the driver first asks for it.
    /// </para>
    /// <para>
    /// Two mistakes are refused here rather than on the first document. A value object over <see cref="Guid"/>, with the
    /// driver's default <see cref="GuidSerializer"/>, whose representation is unspecified, would throw on the first write:
    /// register <c>new GuidSerializer(GuidRepresentation.Standard)</c> before calling this method. And a value object the
    /// driver has already serialized, before this call, keeps the class map it was given, which writes <c>{}</c>, for the
    /// rest of the process: the exception names the value objects concerned, and the registration has to move before
    /// whatever serialized them, a construction of a generic value object and a value object written by hand included.
    /// The <see cref="Guid"/> check covers the value objects the registry holds when the method runs; the provider repeats
    /// it for one it meets later, when it writes one and when it reads a binary value.
    /// </para>
    /// <para>
    /// A value object over <see cref="Int128"/> or <see cref="System.UInt128"/>, for which MongoDB.Bson has no serializer, is
    /// refused with a <see cref="BsonSerializationException"/> when it is written or read, unless the application
    /// registered a serializer of its own for the underlying type, which it then goes through.
    /// </para>
    /// <para>
    /// A construction of a generic value object, a value object written by hand, or one in an assembly whose registration
    /// has not run, that nothing registered, is described by reflection the first time the driver asks for it
    /// (<see cref="ValueObjectRegistry.TryResolve"/>), which only the JIT can run; <see cref="Register{TSelf, TValue}(bool)"/>
    /// registers one without reflection. Locating the generated registration of an assembly given by name reads its
    /// metadata, which trimming may remove. MongoDB.Driver itself maps documents through class maps it builds by
    /// reflection and is not compatible with trimming or native AOT.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
    public static void Register(bool trusted, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ValueObjectRegistry.EnsureAssemblyRegistered(assembly);
        }

        lock (Gate)
        {
            if (_provider is { } registered && registered.Trusted != trusted)
            {
                throw new InvalidOperationException(
                    $"The serializers of value objects are already registered with trusted: {Lowercase(registered.Trusted)}, "
                    + $"and cannot be registered again with trusted: {Lowercase(trusted)}. Give a value object the other "
                    + "trust with ValueObjectBson.Register<TSelf, TValue>(trusted), before the driver first asks for it.");
            }

            EnsureGuidRepresentation(ValueObjectRegistry.GetRegistered(), BsonSerializer.SerializerRegistry);
            EnsureNotMappedYet(BsonClassMap.GetRegisteredClassMaps().Select(static classMap => classMap.ClassType));

            if (_provider is null)
            {
                _provider = new ValueObjectBsonSerializationProvider(trusted);
                BsonSerializer.RegisterSerializationProvider(_provider);
            }
        }
    }

    /// <summary>
    /// Registers the serializer of one value object, such as a construction of a generic value object.
    /// </summary>
    /// <typeparam name="TSelf">Value object type, <c>Code&lt;Order&gt;</c> for instance.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="trusted">
    /// Whether reads skip validation, for a collection the application alone writes; <see langword="false"/>, the
    /// default, reads through the value object's rules.
    /// </param>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TValue"/> is <see cref="Int128"/> or <see cref="System.UInt128"/>, MongoDB.Bson has no serializer for
    /// it, and the application registered none of its own.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TValue"/> is <see cref="Guid"/> and the serializer of <see cref="Guid"/> has no
    /// representation.
    /// </exception>
    /// <exception cref="BsonSerializationException">
    /// The driver already holds a different serializer for <typeparamref name="TSelf"/>: one of the other trust, one of
    /// the application's own, or the class map it gave a value object it serialized before any registration.
    /// </exception>
    /// <remarks>
    /// The serializer is the one <see cref="Register(bool, Assembly[])"/> hands the driver, registered with
    /// <see cref="BsonSerializer.TryRegisterSerializer(Type, IBsonSerializer)"/>, so a second call with the same trust,
    /// or a call for a value object the provider already served with that trust, does nothing. Closed at compile time,
    /// it needs no reflection and no dynamic code to be built.
    /// </remarks>
    public static void Register<TSelf, TValue>(bool trusted = false)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var registry = BsonSerializer.SerializerRegistry;
        if (typeof(TValue) == typeof(Guid))
        {
            EnsureGuidRepresentation(registry);
        }

        var serializer = new ValueObjectBsonSerializationProvider.SerializerFactory(registry, trusted).Visit<TSelf, TValue>();
        if (serializer is UnrepresentableValueObjectSerializer<TSelf>)
        {
            throw new NotSupportedException(
                $"'{typeof(TSelf)}' is a value object over {typeof(TValue).Name}, which MongoDB has no representation for. "
                + $"Register a serializer of the application's own for {typeof(TValue).Name}, with "
                + "BsonSerializer.RegisterSerializer, before this method.");
        }

        BsonSerializer.TryRegisterSerializer(typeof(TSelf), serializer);
    }

    /// <summary>
    /// Tells whether an underlying type is a 128-bit integer, which MongoDB.Bson has no serializer for.
    /// </summary>
    /// <param name="valueType">Underlying type of a value object.</param>
    /// <returns><see langword="true"/> for <see cref="Int128"/> and <see cref="System.UInt128"/>.</returns>
    internal static bool Is128Bit(Type valueType) => valueType == typeof(Int128) || valueType == typeof(UInt128);

    /// <summary>
    /// Tells whether a serializer of <see cref="Guid"/> is the driver's own with no representation, which throws on the
    /// first <see cref="Guid"/> it writes.
    /// </summary>
    /// <param name="serializer">The serializer of <see cref="Guid"/>, or of any other type.</param>
    /// <returns>
    /// <see langword="true"/> for a <see cref="GuidSerializer"/> writing binary under
    /// <see cref="GuidRepresentation.Unspecified"/>; a legacy representation, a representation as text, and a serializer
    /// of the application's own write.
    /// </returns>
    internal static bool LacksGuidRepresentation(IBsonSerializer serializer)
        => serializer is GuidSerializer
        {
            Representation: BsonType.Binary,
            GuidRepresentation: GuidRepresentation.Unspecified,
        };

    /// <summary>
    /// Throws when one of the value objects is over <see cref="Guid"/> and the serializer a registry holds for
    /// <see cref="Guid"/> cannot write one.
    /// </summary>
    /// <param name="descriptors">The value objects to check.</param>
    /// <param name="registry">The registry, asked for the serializer of <see cref="Guid"/> only when one is over it.</param>
    /// <exception cref="InvalidOperationException">
    /// A value object is over <see cref="Guid"/>, and the serializer of <see cref="Guid"/> has no representation.
    /// </exception>
    internal static void EnsureGuidRepresentation(IEnumerable<ValueObjectDescriptor> descriptors, IBsonSerializerRegistry registry)
    {
        if (descriptors.Any(static descriptor => descriptor.ValueType == typeof(Guid)))
        {
            EnsureGuidRepresentation(registry);
        }
    }

    /// <summary>
    /// Throws when the serializer a registry holds for <see cref="Guid"/> cannot write one.
    /// </summary>
    /// <param name="registry">The registry.</param>
    /// <exception cref="InvalidOperationException">The serializer of <see cref="Guid"/> has no representation.</exception>
    internal static void EnsureGuidRepresentation(IBsonSerializerRegistry registry)
    {
        if (LacksGuidRepresentation(registry.GetSerializer<Guid>()))
        {
            throw new InvalidOperationException(
                "A value object over Guid is registered, and the serializer of Guid has no representation "
                + "(GuidRepresentation.Unspecified), so it would throw on the first write. Call "
                + "BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)) before this method.");
        }
    }

    /// <summary>
    /// Throws when the driver has already mapped value objects through a class map, which writes them as <c>{}</c>.
    /// </summary>
    /// <param name="classMapped">
    /// The types a class map is registered for: the driver builds one for a type the first time it serializes it with no
    /// serializer of its own, and keeps the serializer built over it for good.
    /// </param>
    /// <exception cref="InvalidOperationException">At least one of the types is a value object.</exception>
    /// <remarks>
    /// Every value object counts, not only those the registry holds: a construction of a generic value object, which the
    /// registry holds as its generic definition, a value object written by hand, and one of a module whose registration
    /// has not run, are each given a class map as any other is.
    /// </remarks>
    internal static void EnsureNotMappedYet(IEnumerable<Type> classMapped)
    {
        var mapped = classMapped
            .Where(ValueObjectRegistry.IsValueObject)
            .Select(NameOf)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (mapped.Count > 0)
        {
            throw new InvalidOperationException(
                $"MongoDB.Driver has already mapped {string.Join(", ", mapped)} through a class map, which writes a value "
                + "object as {} and reads it back as its default, and keeps that serializer for the rest of the process. "
                + "Call ValueObjectBson.Register at start-up, before anything is serialized or any class map is built.");
        }
    }

    /// <summary>
    /// Names a type as C# names it, <c>ShipmentTag&lt;Shipment&gt;</c> for a construction, without its namespace.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>Its name, followed by its type arguments when it has some of its own.</returns>
    private static string NameOf(Type type)
    {
        var tick = type.Name.IndexOf('`', StringComparison.Ordinal);

        return tick < 0
            ? type.Name
            : $"{type.Name[..tick]}<{string.Join(", ", type.GetGenericArguments().Select(NameOf))}>";
    }

    /// <summary>
    /// Writes a flag as C# writes it.
    /// </summary>
    /// <param name="value">The flag.</param>
    /// <returns><c>true</c> or <c>false</c>.</returns>
    private static string Lowercase(bool value) => value ? "true" : "false";
}
