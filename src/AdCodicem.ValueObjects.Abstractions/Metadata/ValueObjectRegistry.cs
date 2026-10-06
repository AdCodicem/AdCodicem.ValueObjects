using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// Process-wide directory of the value object types known to the application.
/// </summary>
/// <remarks>
/// <para>
/// The source generator emits a module initializer per assembly that registers every generated value object,
/// so the common case is a lock-free dictionary hit with no reflection and no dynamic code — the registry is
/// therefore usable under native AOT.
/// </para>
/// <para>
/// <see cref="TryResolve"/> additionally falls back to reflection for hand-written value objects, for modules whose
/// initializer has not run yet, and for the constructions of a generic value object, whose generic definition is all
/// the initializer can register. It describes each of them from the <see cref="IValueObject{TSelf, TValue}.Schema"/>
/// the type declares. It is annotated as requiring dynamic code, and its result is cached, so a given type pays that
/// cost at most once.
/// </para>
/// </remarks>
public static class ValueObjectRegistry
{
    private static readonly ConcurrentDictionary<Type, ValueObjectDescriptor> Descriptors = new();
    private static readonly ConcurrentDictionary<Type, Type?> UnderlyingTypes = new();
    private static readonly ConcurrentDictionary<Assembly, bool> ScannedAssemblies = new();
    private static readonly ConcurrentDictionary<Type, bool> GenericDefinitions = new();

    /// <summary>
    /// Registers a descriptor, replacing any previous registration for the same type.
    /// </summary>
    /// <param name="descriptor">Descriptor to register.</param>
    public static void Register(ValueObjectDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Descriptors[descriptor.ValueObjectType] = descriptor;
        UnderlyingTypes[descriptor.ValueObjectType] = descriptor.ValueType;
    }

    /// <summary>
    /// Registers a value object without reflection.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="schema">Declarative constraints of the value object.</param>
    /// <remarks>
    /// This is the registration native AOT asks of a value object written by hand that a type-driven integration needs,
    /// <c>Register&lt;Link, Uri&gt;(Link.Schema)</c>, since <see cref="TryResolve"/> would describe it by reflection. Its
    /// descriptor carries no System.Text.Json converter: the converter factory of <c>AdCodicem.ValueObjects.Json</c> gives
    /// it a general-purpose one, closed through
    /// <see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/>.
    /// </remarks>
    public static void Register<TSelf, TValue>(ValueObjectSchema schema)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => Register(ValueObjectDescriptor.For<TSelf, TValue>(schema));

    /// <summary>
    /// Registers a value object and its System.Text.Json converter without reflection.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="schema">Declarative constraints of the value object.</param>
    /// <param name="jsonConverter">The converter of the value object.</param>
    /// <remarks>
    /// A source-generated serializer context finds the converter through the descriptor, whatever the assembly declaring
    /// the value object references. A construction of a generic value object registered by hand under native AOT takes
    /// the shorter <see cref="Register{TSelf, TValue}(Func{JsonConverter{TSelf}})"/>, which reads its schema off the type.
    /// </remarks>
    public static void Register<TSelf, TValue>(ValueObjectSchema schema, JsonConverter<TSelf> jsonConverter)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(jsonConverter);

        Register(ValueObjectDescriptor.For<TSelf, TValue>(schema, () => jsonConverter));
    }

    /// <summary>
    /// Registers a value object and the factory of its System.Text.Json converter without reflection.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="schema">Declarative constraints of the value object.</param>
    /// <param name="jsonConverter">Creates the converter of the value object, the first time it is asked for.</param>
    /// <remarks>
    /// The generated registration calls this one. It runs for every value object when the declaring assembly loads,
    /// and an application that never serializes a value object through the descriptor never builds its converter.
    /// </remarks>
    public static void Register<TSelf, TValue>(ValueObjectSchema schema, Func<JsonConverter<TSelf>> jsonConverter)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(jsonConverter);

        Register(ValueObjectDescriptor.For<TSelf, TValue>(schema, jsonConverter));
    }

    /// <summary>
    /// Registers a value object with the schema it declares and the factory of its System.Text.Json converter, without
    /// reflection.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="jsonConverter">Creates the converter of the value object, the first time it is asked for.</param>
    /// <remarks>
    /// The schema is <see cref="IValueObject{TSelf, TValue}.Schema"/>, the one the generated registration hands over. This
    /// is the registration native AOT asks of each construction of a generic value object that a type-driven integration
    /// needs: <c>Register&lt;Code&lt;Order&gt;, string&gt;(static () =&gt; new Code&lt;Order&gt;.ValueJsonConverter())</c>.
    /// Its descriptor is built in code, where the AOT compiler sees both type arguments, so a visitor
    /// (<see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/>) runs on it as on any generated
    /// value object.
    /// </remarks>
    public static void Register<TSelf, TValue>(Func<JsonConverter<TSelf>> jsonConverter)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(jsonConverter);

        Register(ValueObjectDescriptor.For<TSelf, TValue>(TSelf.Schema, jsonConverter));
    }

    /// <summary>
    /// Registers the definition of a generic value object, whose constructions the registry describes on demand.
    /// </summary>
    /// <param name="definition">
    /// The generic type definition, <c>typeof(Code&lt;&gt;)</c>, or that of a value object nested in a generic type,
    /// <c>typeof(Outer&lt;&gt;.Code)</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="definition"/> is not the generic type definition of a struct carrying the
    /// <see cref="IValueObject"/> marker.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The generated registration calls this for a generic value object, since it knows none of the constructions the
    /// application will use. <see cref="TryResolve"/> then describes each construction it is asked for from the members
    /// the generator wrote on it, its schema and its converter, and caches the descriptor.
    /// </para>
    /// <para>
    /// That takes reflection and dynamic code. Under native AOT, register each construction a type-driven integration
    /// needs instead, which takes neither:
    /// <c>ValueObjectRegistry.Register&lt;Code&lt;Order&gt;, string&gt;(static () =&gt; new Code&lt;Order&gt;.ValueJsonConverter())</c>.
    /// </para>
    /// <para>
    /// The definition is checked without reading its interfaces, which native AOT does not keep for a generic type
    /// definition: this runs in the module initializer of every assembly declaring a generic value object, where a
    /// failure would stop the application before it starts.
    /// </para>
    /// </remarks>
    public static void RegisterGenericDefinition(Type definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!definition.IsGenericTypeDefinition || !definition.IsValueType || !typeof(IValueObject).IsAssignableFrom(definition))
        {
            throw new ArgumentException(
                $"'{definition}' is not the generic type definition of a value object struct.",
                nameof(definition));
        }

        GenericDefinitions[definition] = true;
    }

    /// <summary>
    /// Gets the descriptors registered so far.
    /// </summary>
    /// <returns>A snapshot of the registered descriptors.</returns>
    /// <remarks>
    /// A generic value object appears through the constructions registered or resolved so far, never through its
    /// definition, which <see cref="GetRegisteredGenericDefinitions"/> lists.
    /// </remarks>
    public static IReadOnlyCollection<ValueObjectDescriptor> GetRegistered() => [.. Descriptors.Values];

    /// <summary>
    /// Gets the definitions of the generic value objects registered so far.
    /// </summary>
    /// <returns>A snapshot of the generic type definitions.</returns>
    /// <remarks>
    /// An integration configuring every value object up front, as the Entity Framework Core convention does, configures
    /// these by their definition and closes each construction where it meets one.
    /// </remarks>
    public static IReadOnlyCollection<Type> GetRegisteredGenericDefinitions() => [.. GenericDefinitions.Keys];

    /// <summary>
    /// Looks up an already registered descriptor.
    /// </summary>
    /// <param name="type">Value object type, possibly nullable.</param>
    /// <param name="descriptor">The descriptor when found.</param>
    /// <returns><see langword="true"/> when a descriptor is registered for <paramref name="type"/>.</returns>
    /// <remarks>
    /// Asked for <c>Iban?</c>, it returns the descriptor of <c>Iban</c>: a descriptor describes the value object
    /// itself, never its nullable type. A converter or a provider built from it closes its generic types over
    /// <see cref="ValueObjectDescriptor.ValueObjectType"/> rather than over <paramref name="type"/>, which a
    /// constraint on the value object refuses for a <see cref="Nullable{T}"/>, and handles a <see langword="null"/>
    /// before the descriptor's delegates see it, leaving it to the host's own nullable wrapper where one exists.
    /// </remarks>
    public static bool TryGet(Type type, [NotNullWhen(true)] out ValueObjectDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(type);

        return Descriptors.TryGetValue(Nullable.GetUnderlyingType(type) ?? type, out descriptor);
    }

    /// <summary>
    /// Looks up a descriptor, building it by reflection when the type has not registered itself.
    /// </summary>
    /// <param name="type">Value object type, possibly nullable.</param>
    /// <param name="descriptor">The descriptor when the type is a value object.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="type"/> is a value object, as <see cref="IsValueObject"/> defines
    /// one.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Asked for <c>Iban?</c>, it returns the descriptor of <c>Iban</c>, as <see cref="TryGet"/> does, and the caller
    /// handles <see cref="Nullable{T}"/> and <see langword="null"/> as <see cref="TryGet"/> says.
    /// </para>
    /// <para>
    /// A type nothing registered is described with the <see cref="IValueObject{TSelf, TValue}.Schema"/> it declares, the
    /// one generic code constrained on it reads, whatever annotation it carries.
    /// </para>
    /// </remarks>
    [RequiresDynamicCode("Building a descriptor for an unregistered value object instantiates a generic method at run time.")]
    [RequiresUnreferencedCode("Building a descriptor for an unregistered value object inspects its interfaces and generated members.")]
    public static bool TryResolve(Type type, [NotNullWhen(true)] out ValueObjectDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(type);

        var valueObjectType = Nullable.GetUnderlyingType(type) ?? type;
        if (Descriptors.TryGetValue(valueObjectType, out descriptor))
        {
            return true;
        }

        if (!TryGetSelfDescribedValueType(valueObjectType, out var valueType))
        {
            descriptor = null;
            return false;
        }

        // The module initializer of the declaring assembly may simply not have run yet: force it, then retry.
        EnsureAssemblyRegistered(valueObjectType.Assembly);
        if (Descriptors.TryGetValue(valueObjectType, out descriptor))
        {
            return true;
        }

        descriptor = Descriptors.GetOrAdd(
            valueObjectType,
            static (key, underlying) => BuildByReflection(key, underlying),
            valueType);
        return true;
    }

    /// <summary>
    /// Determines whether a type is a value object: a struct implementing <see cref="IValueObject{TSelf, TValue}"/>
    /// over itself, the one shape a descriptor, a converter or a model binder can be built for.
    /// </summary>
    /// <param name="type">Type to test, possibly nullable.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="type"/>, or the type it makes nullable, is a value object, which is
    /// exactly when <see cref="TryResolve"/> describes it. An interface, a class, or a struct carrying only the
    /// <see cref="IValueObject"/> marker or <see cref="IValueObject{TValue}"/> is not one.
    /// </returns>
    /// <remarks>
    /// Answering registers nothing. A registered type is answered from the registry, without reflection.
    /// </remarks>
    public static bool IsValueObject(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var valueObjectType = Nullable.GetUnderlyingType(type) ?? type;

        return Descriptors.ContainsKey(valueObjectType) || TryGetSelfDescribedValueType(valueObjectType, out _);
    }

    /// <summary>
    /// Gets the underlying value type of a value object.
    /// </summary>
    /// <param name="type">Value object type, possibly nullable.</param>
    /// <returns>
    /// The <c>TValue</c> of the <see cref="IValueObject{TSelf, TValue}"/> the type implements over itself, or
    /// <see langword="null"/> when the type is not a value object as <see cref="IsValueObject"/> defines one — a
    /// class or a struct implementing only <see cref="IValueObject{TValue}"/> included.
    /// </returns>
    /// <remarks>
    /// Answering registers nothing. A registered type is answered from the registry, without reflection.
    /// </remarks>
    public static Type? GetUnderlyingType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return UnderlyingTypes.GetOrAdd(
            Nullable.GetUnderlyingType(type) ?? type,
            static key => TryGetSelfDescribedValueType(key, out var valueType) ? valueType : null);
    }

    /// <summary>
    /// Runs the generated registration of an assembly, if it has one.
    /// </summary>
    /// <param name="assembly">Assembly declaring value objects.</param>
    /// <remarks>
    /// Module initializers are triggered by the first access to a member of the module, which model-building
    /// code that only reflects over types may never perform. Integrations call this before enumerating types.
    /// </remarks>
    [RequiresUnreferencedCode("Locating the generated registration type of an assembly requires its metadata.")]
    public static void EnsureAssemblyRegistered(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (!ScannedAssemblies.TryAdd(assembly, true))
        {
            return;
        }

        var registration = assembly.GetType("AdCodicem.ValueObjects.Generated.ValueObjectRegistration", throwOnError: false);
        registration
            ?.GetMethod("RegisterAll", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, null);
    }

    /// <summary>
    /// Finds the underlying type of a struct implementing <see cref="IValueObject{TSelf, TValue}"/> over itself,
    /// the only shape <see cref="ValueObjectDescriptor.For{TSelf, TValue}(ValueObjectSchema)"/> accepts.
    /// </summary>
    /// <param name="type">Candidate type, already unwrapped from <see cref="Nullable{T}"/>.</param>
    /// <param name="valueType">The underlying type when the candidate qualifies.</param>
    /// <returns><see langword="true"/> when a descriptor can be built for <paramref name="type"/>.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "The interface list of a value object is preserved: the type is referenced by the caller and its IValueObject implementation is part of its public contract.")]
    private static bool TryGetSelfDescribedValueType(Type type, [NotNullWhen(true)] out Type? valueType)
    {
        // The marker is a cheap filter for the many types a serializer asks about that are not value objects at all. A
        // type with type parameters left open, a generic definition first among them, is no value: no instance of it
        // exists, and no descriptor can be built for it.
        if (type.IsValueType && !type.ContainsGenericParameters && typeof(IValueObject).IsAssignableFrom(type))
        {
            foreach (var candidate in type.GetInterfaces())
            {
                if (candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == typeof(IValueObject<,>)
                    && candidate.GetGenericArguments()[0] == type)
                {
                    valueType = candidate.GetGenericArguments()[1];
                    return true;
                }
            }
        }

        valueType = null;
        return false;
    }

    /// <summary>
    /// Describes a value object nothing registered, from the schema it declares and, for a construction of a generated
    /// generic value object, the converter the generator wrote on it.
    /// </summary>
    /// <param name="valueObjectType">The value object, <c>Code&lt;Order&gt;</c> or one written by hand.</param>
    /// <param name="valueType">Its underlying type.</param>
    /// <returns>The descriptor.</returns>
    /// <remarks>
    /// The schema is <c>TSelf.Schema</c>, the one the typed path reads, so that a type known only at run time is
    /// described with the rules generic code constrained on it sees: for a construction, the one the generated
    /// registration would have handed over; for a value object written by hand, the one it declares, whatever annotation
    /// it carries. The converter of a construction is nested in the generic type, and so is generic itself: it is closed
    /// over the type arguments of the construction. A value object written by hand carries none the registry knows of.
    /// </remarks>
    [RequiresDynamicCode("Closes the generated converter and ValueObjectDescriptor.For<,> over the type.")]
    [RequiresUnreferencedCode("Reads the generated members of the type.")]
    private static ValueObjectDescriptor BuildByReflection(Type valueObjectType, Type valueType)
    {
        var converterType = valueObjectType.IsConstructedGenericType
                            && GenericDefinitions.ContainsKey(valueObjectType.GetGenericTypeDefinition())
            ? valueObjectType.GetNestedType("ValueJsonConverter", BindingFlags.Public)
                ?.MakeGenericType(valueObjectType.GetGenericArguments())
            : null;

        return (ValueObjectDescriptor)typeof(ValueObjectRegistry)
            .GetMethod(nameof(Describe), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(valueObjectType, valueType)
            .Invoke(null, [converterType])!;
    }

    /// <summary>
    /// Builds the descriptor of a value object from the schema it declares and the type of its converter.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="converterType">
    /// Its generated converter, closed over it, created the first time it is asked for; <see langword="null"/> for none.
    /// </param>
    /// <returns>The descriptor.</returns>
    [RequiresUnreferencedCode("Creates the generated converter of the construction by reflection.")]
    private static ValueObjectDescriptor Describe<TSelf, TValue>(Type? converterType)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => ValueObjectDescriptor.For<TSelf, TValue>(
            TSelf.Schema,
            converterType is null ? null : () => (JsonConverter<TSelf>)Activator.CreateInstance(converterType)!);
}
