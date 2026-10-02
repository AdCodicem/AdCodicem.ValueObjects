using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
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
/// <see cref="TryResolve"/> additionally falls back to reflection for hand-written value objects and for
/// modules whose initializer has not run yet. It is annotated as requiring dynamic code, and its result is
/// cached, so a given type pays that cost at most once.
/// </para>
/// </remarks>
public static class ValueObjectRegistry
{
    private static readonly ConcurrentDictionary<Type, ValueObjectDescriptor> Descriptors = new();
    private static readonly ConcurrentDictionary<Type, Type?> UnderlyingTypes = new();
    private static readonly ConcurrentDictionary<Assembly, bool> ScannedAssemblies = new();

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
    /// Gets the descriptors registered so far.
    /// </summary>
    /// <returns>A snapshot of the registered descriptors.</returns>
    public static IReadOnlyCollection<ValueObjectDescriptor> GetRegistered() => [.. Descriptors.Values];

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
    /// the only shape <see cref="ValueObjectDescriptor.For{TSelf, TValue}"/> accepts.
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
        // The marker is a cheap filter for the many types a serializer asks about that are not value objects at all.
        if (type.IsValueType && typeof(IValueObject).IsAssignableFrom(type))
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
        var schema = ReadSchema(valueObjectType);
        if (!schema.KnownValues.IsDefaultOrEmpty)
        {
            var normalize = typeof(ValueObjectRegistry)
                .GetMethod(nameof(NormalizeKnownValues), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(valueObjectType, valueType);

            schema = schema with { KnownValues = (ImmutableArray<object>)normalize.Invoke(null, [schema.KnownValues])! };
        }

        var factory = typeof(ValueObjectDescriptor)
            .GetMethod(nameof(ValueObjectDescriptor.For), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(valueObjectType, valueType);

        return (ValueObjectDescriptor)factory.Invoke(null, [schema])!;
    }

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

    [RequiresDynamicCode("Instantiates the generic reader of IValueObjectPatternValidator for the type.")]
    [RequiresUnreferencedCode("Reads the annotations of the value object type.")]
    private static ValueObjectSchema ReadSchema(Type valueObjectType)
    {
        var attribute = valueObjectType
            .GetCustomAttributes(inherit: false)
            .FirstOrDefault(candidate => candidate.GetType().IsGenericType
                                         && candidate.GetType().GetGenericTypeDefinition() == typeof(ValueObjectAttribute<>));

        // The pattern hook describes a type with no annotation as well: it is an interface the type implements.
        var hookPattern = ReadPatternHook(valueObjectType);

        if (attribute is null)
        {
            return hookPattern is null ? ValueObjectSchema.Unconstrained : new ValueObjectSchema { Pattern = hookPattern };
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
            Minimum = ReadString(type, attribute, nameof(ValueObjectAttribute<object>.Minimum)),
            Maximum = ReadString(type, attribute, nameof(ValueObjectAttribute<object>.Maximum)),
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

    [RequiresUnreferencedCode("Reads a property of the value object annotation.")]
    private static string? ReadString(Type attributeType, object attribute, string propertyName)
        => attributeType.GetProperty(propertyName)?.GetValue(attribute)?.ToString();

    [RequiresUnreferencedCode("Reads a property of the value object annotation.")]
    private static int ReadInt32(Type attributeType, object attribute, string propertyName)
        => attributeType.GetProperty(propertyName)?.GetValue(attribute) is int value ? value : -1;
}
