using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Annotations;

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
/// the initializer can register. It is annotated as requiring dynamic code, and its result is cached, so a given type
/// pays that cost at most once.
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
    /// the value object references. This is the registration native AOT asks of a construction of a generic value
    /// object: <c>Register&lt;Code&lt;Order&gt;, string&gt;(Code&lt;Order&gt;.Schema, new Code&lt;Order&gt;.ValueJsonConverter())</c>.
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
    /// <c>ValueObjectRegistry.Register&lt;Code&lt;Order&gt;, string&gt;(Code&lt;Order&gt;.Schema, new Code&lt;Order&gt;.ValueJsonConverter())</c>.
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
    [RequiresDynamicCode("Building a descriptor for an unregistered value object instantiates a generic method at run time.")]
    [RequiresUnreferencedCode("Building a descriptor for an unregistered value object inspects its interfaces and attributes.")]
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

    [RequiresDynamicCode("Instantiates ValueObjectDescriptor.For<,> for the resolved type arguments.")]
    [RequiresUnreferencedCode("Reads the value object interfaces and annotations of the type.")]
    private static ValueObjectDescriptor BuildByReflection(Type valueObjectType, Type valueType)
    {
        if (valueObjectType.IsConstructedGenericType
            && GenericDefinitions.ContainsKey(valueObjectType.GetGenericTypeDefinition())
            && BuildConstruction(valueObjectType, valueType) is { } construction)
        {
            return construction;
        }

        var schema = ReadSchema(valueObjectType, valueType);
        if (!schema.KnownValues.IsDefaultOrEmpty)
        {
            var normalize = typeof(ValueObjectRegistry)
                .GetMethod(nameof(NormalizeKnownValues), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(valueObjectType, valueType);

            schema = schema with { KnownValues = (ImmutableArray<object>)normalize.Invoke(null, [schema.KnownValues])! };
        }

        var factory = typeof(ValueObjectDescriptor)
            .GetMethod(nameof(ValueObjectDescriptor.For), 2, BindingFlags.Public | BindingFlags.Static, [typeof(ValueObjectSchema)])!
            .MakeGenericMethod(valueObjectType, valueType);

        return (ValueObjectDescriptor)factory.Invoke(null, [schema])!;
    }

    /// <summary>
    /// Describes a construction of a generated generic value object from the members the generator wrote on it.
    /// </summary>
    /// <param name="valueObjectType">The construction, <c>Code&lt;Order&gt;</c>.</param>
    /// <param name="valueType">Its underlying type.</param>
    /// <returns>
    /// The descriptor, carrying the generated schema and converter, or <see langword="null"/> when the type carries no
    /// generated schema, as a definition registered by hand may not.
    /// </returns>
    /// <remarks>
    /// The schema is the one the generated registration would have handed over, so the construction is described
    /// exactly as a value object that is not generic is. The converter is nested in the generic type, and so is
    /// generic itself: it is closed over the type arguments of the construction.
    /// </remarks>
    [RequiresDynamicCode("Closes the generated converter and ValueObjectDescriptor.For<,> over the construction.")]
    [RequiresUnreferencedCode("Reads the generated members of the construction.")]
    private static ValueObjectDescriptor? BuildConstruction(Type valueObjectType, Type valueType)
    {
        if (valueObjectType.GetProperty("Schema", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            is not ValueObjectSchema schema)
        {
            return null;
        }

        var converterType = valueObjectType.GetNestedType("ValueJsonConverter", BindingFlags.Public)
            ?.MakeGenericType(valueObjectType.GetGenericArguments());

        return (ValueObjectDescriptor)typeof(ValueObjectRegistry)
            .GetMethod(nameof(Construct), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(valueObjectType, valueType)
            .Invoke(null, [schema, converterType])!;
    }

    /// <summary>
    /// Builds the descriptor of a construction from its schema and the type of its converter.
    /// </summary>
    /// <typeparam name="TSelf">The construction.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="schema">Its generated schema.</param>
    /// <param name="converterType">Its generated converter, closed over it, created the first time it is asked for.</param>
    /// <returns>The descriptor.</returns>
    [RequiresUnreferencedCode("Creates the generated converter of the construction by reflection.")]
    private static ValueObjectDescriptor Construct<TSelf, TValue>(ValueObjectSchema schema, Type? converterType)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => ValueObjectDescriptor.For<TSelf, TValue>(
            schema,
            converterType is null ? null : () => (JsonConverter<TSelf>)Activator.CreateInstance(converterType)!);

    /// <summary>
    /// Turns the known values an annotation declares into the values the type holds, as the generated schema
    /// publishes them.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="declared">The known values, as the attributes hold them.</param>
    /// <returns>The known values, normalized.</returns>
    /// <remarks>
    /// A value of the underlying type is normalized. Any other, text for a type no attribute argument can carry
    /// first among them, is parsed the way the type parses text. What the type cannot parse stays as written: the
    /// generator would have refused it, but nothing checks an annotation where no generator runs.
    /// </remarks>
    private static ImmutableArray<object> NormalizeKnownValues<TSelf, TValue>(ImmutableArray<object> declared)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var normalized = ImmutableArray.CreateBuilder<object>(declared.Length);
        foreach (var known in declared)
        {
            if (known is TValue typed)
            {
                normalized.Add(TSelf.Normalize(typed)!);
            }
            else if (TSelf.TryParse(Convert.ToString(known, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out var parsed, out _))
            {
                normalized.Add(parsed.Value!);
            }
            else
            {
                normalized.Add(known);
            }
        }

        return normalized.MoveToImmutable();
    }

    /// <summary>
    /// The name of the deprecated <c>Pattern</c> option, read by name: naming the obsolete property would report
    /// <c>VO0021</c> here, and once the property is removed the read finds nothing instead of failing to compile.
    /// </summary>
    private const string PatternOption = "Pattern";

    /// <summary>
    /// The names of the deprecated <c>Minimum</c> and <c>Maximum</c> options, read by name for the reason
    /// <see cref="PatternOption"/> is: naming the obsolete properties would report <c>VO0028</c> here.
    /// </summary>
    private const string MinimumOption = "Minimum";

    /// <inheritdoc cref="MinimumOption"/>
    private const string MaximumOption = "Maximum";

    [RequiresDynamicCode("Instantiates the generic readers of the hooks for the type.")]
    [RequiresUnreferencedCode("Reads the annotations of the value object type.")]
    private static ValueObjectSchema ReadSchema(Type valueObjectType, Type valueType)
    {
        var attribute = valueObjectType
            .GetCustomAttributes(inherit: false)
            .FirstOrDefault(candidate => candidate.GetType().IsGenericType
                                         && candidate.GetType().GetGenericTypeDefinition() == typeof(ValueObjectAttribute<>));

        // The hooks describe a type with no annotation as well: each is an interface the type implements.
        var hookPattern = ReadPatternHook(valueObjectType);
        var hookMinimum = ReadBoundHook(valueObjectType, valueType, typeof(IValueObjectMinimum<>), nameof(ValueObjectBound.Minimum));
        var hookMaximum = ReadBoundHook(valueObjectType, valueType, typeof(IValueObjectMaximum<>), nameof(ValueObjectBound.Maximum));

        if (attribute is null)
        {
            return hookPattern is null && hookMinimum is null && hookMaximum is null
                ? ValueObjectSchema.Unconstrained
                : new ValueObjectSchema { Pattern = hookPattern, Minimum = hookMinimum, Maximum = hookMaximum };
        }

        var knownValues = valueObjectType
            .GetCustomAttributes<KnownValueAttribute>(inherit: false)
            .Select(known => known.Value)
            .ToImmutableArray();

        var type = attribute.GetType();

        return new ValueObjectSchema
        {
            Pattern = hookPattern ?? ReadString(type, attribute, PatternOption),
            MinLength = NormalizeLength(ReadInt32(type, attribute, nameof(ValueObjectAttribute<object>.MinLength))),
            MaxLength = NormalizeLength(ReadInt32(type, attribute, nameof(ValueObjectAttribute<object>.MaxLength))),
            Minimum = hookMinimum ?? ReadString(type, attribute, MinimumOption),
            Maximum = hookMaximum ?? ReadString(type, attribute, MaximumOption),
            Format = ReadString(type, attribute, nameof(ValueObjectAttribute<object>.SchemaFormat)),
            Description = ReadString(type, attribute, nameof(ValueObjectAttribute<object>.Description)),
            Example = ReadString(type, attribute, nameof(ValueObjectAttribute<object>.Example)),
            IsClosedValueSet = ReadString(type, attribute, nameof(ValueObjectAttribute<object>.ValueSet)) == nameof(ValueSetKind.Closed),
            KnownValues = knownValues,
        };

        static int? NormalizeLength(int value) => value < 0 ? null : value;
    }

    /// <summary>
    /// Reads the text of the pattern a type declares through <see cref="IValueObjectPatternValidator"/>, or
    /// <see langword="null"/> when it implements none.
    /// </summary>
    /// <remarks>
    /// A static abstract member is reachable through a type parameter only, so the read goes through
    /// <see cref="ValueObjectPattern.Of{TSelf}"/> closed over the type, as generated code does, rather than through a
    /// property lookup that would miss an explicit implementation.
    /// </remarks>
    [RequiresDynamicCode("Instantiates ValueObjectPattern.Of for the type.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2060:MakeGenericMethod",
        Justification = "ValueObjectPattern.Of has no requirement on its type parameter beyond the interface the check above proves.")]
    private static string? ReadPatternHook(Type valueObjectType)
        => typeof(IValueObjectPatternValidator).IsAssignableFrom(valueObjectType)
            ? typeof(ValueObjectPattern)
                .GetMethod(nameof(ValueObjectPattern.Of), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(valueObjectType)
                .Invoke(null, null)?
                .ToString()
            : null;

    /// <summary>
    /// Reads a bound a type declares through <see cref="IValueObjectMinimum{TValue}"/> or
    /// <see cref="IValueObjectMaximum{TValue}"/>, as text, or <see langword="null"/> when it implements neither.
    /// </summary>
    /// <param name="valueObjectType">The value object.</param>
    /// <param name="valueType">Its underlying type, the one a hook bounds it with.</param>
    /// <param name="hook">The generic definition of the hook.</param>
    /// <param name="bridge">The member of <see cref="ValueObjectBound"/> that reads the hook.</param>
    /// <returns>The bound, in the form <see cref="ValueObjectBound.Text{TValue}"/> writes it.</returns>
    /// <remarks>
    /// As for the pattern, the bound is read through <see cref="ValueObjectBound"/> closed over the type, which reaches
    /// an explicit implementation too.
    /// </remarks>
    [RequiresDynamicCode("Instantiates the generic reader of the bound for the type.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2060:MakeGenericMethod",
        Justification = "ValueObjectBound has no requirement on its type parameters beyond the interface the check above proves.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2055:MakeGenericType",
        Justification = "The hook interfaces have no requirement on their type parameter.")]
    private static string? ReadBoundHook(Type valueObjectType, Type valueType, Type hook, string bridge)
        => hook.MakeGenericType(valueType).IsAssignableFrom(valueObjectType)
            ? ValueObjectBound.Text(typeof(ValueObjectBound)
                .GetMethod(bridge, BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(valueObjectType, valueType)
                .Invoke(null, null))
            : null;

    [RequiresUnreferencedCode("Reads a property of the value object annotation.")]
    private static string? ReadString(Type attributeType, object attribute, string propertyName)
        => attributeType.GetProperty(propertyName)?.GetValue(attribute)?.ToString();

    [RequiresUnreferencedCode("Reads a property of the value object annotation.")]
    private static int ReadInt32(Type attributeType, object attribute, string propertyName)
        => attributeType.GetProperty(propertyName)?.GetValue(attribute) is int value ? value : -1;
}
