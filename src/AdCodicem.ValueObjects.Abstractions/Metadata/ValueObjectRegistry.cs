using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
    /// <returns><see langword="true"/> when <paramref name="type"/> is a value object.</returns>
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

        if (!IsValueObject(valueObjectType))
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

        descriptor = Descriptors.GetOrAdd(valueObjectType, static key => BuildByReflection(key));
        return true;
    }

    /// <summary>
    /// Determines whether a type is a value object.
    /// </summary>
    /// <param name="type">Type to test, possibly nullable.</param>
    /// <returns><see langword="true"/> when the type implements <see cref="IValueObject"/>.</returns>
    public static bool IsValueObject(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return typeof(IValueObject).IsAssignableFrom(Nullable.GetUnderlyingType(type) ?? type);
    }

    /// <summary>
    /// Gets the underlying value type of a value object.
    /// </summary>
    /// <param name="type">Value object type, possibly nullable.</param>
    /// <returns>The underlying value type, or <see langword="null"/> when the type is not a value object.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "The interface list of a value object is preserved: the type is referenced by the caller and its IValueObject implementation is part of its public contract.")]
    public static Type? GetUnderlyingType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return UnderlyingTypes.GetOrAdd(
            Nullable.GetUnderlyingType(type) ?? type,
            static key =>
            {
                foreach (var candidate in key.GetInterfaces())
                {
                    if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IValueObject<>))
                    {
                        return candidate.GetGenericArguments()[0];
                    }
                }

                return null;
            });
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

    [RequiresDynamicCode("Instantiates ValueObjectDescriptor.For<,> for the resolved type arguments.")]
    [RequiresUnreferencedCode("Reads the value object interfaces and annotations of the type.")]
    private static ValueObjectDescriptor BuildByReflection(Type valueObjectType)
    {
        var valueType = GetUnderlyingType(valueObjectType)
                        ?? throw new InvalidOperationException(
                            $"'{valueObjectType.Name}' implements IValueObject but not IValueObject<TValue>.");

        var factory = typeof(ValueObjectDescriptor)
            .GetMethod(nameof(ValueObjectDescriptor.For), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(valueObjectType, valueType);

        return (ValueObjectDescriptor)factory.Invoke(null, [ReadSchema(valueObjectType)])!;
    }

    [RequiresUnreferencedCode("Reads the annotations of the value object type.")]
    private static ValueObjectSchema ReadSchema(Type valueObjectType)
    {
        var attribute = valueObjectType
            .GetCustomAttributes(inherit: false)
            .FirstOrDefault(candidate => candidate.GetType().IsGenericType
                                         && candidate.GetType().GetGenericTypeDefinition() == typeof(ValueObjectAttribute<>));

        if (attribute is null)
        {
            return ValueObjectSchema.Unconstrained;
        }

        var knownValues = valueObjectType
            .GetCustomAttributes<KnownValueAttribute>(inherit: false)
            .Select(known => known.Value)
            .ToImmutableArray();

        var type = attribute.GetType();

        return new ValueObjectSchema
        {
            Pattern = ReadString(type, attribute, nameof(ValueObjectAttribute<object>.Pattern)),
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

    [RequiresUnreferencedCode("Reads a property of the value object annotation.")]
    private static string? ReadString(Type attributeType, object attribute, string propertyName)
        => attributeType.GetProperty(propertyName)?.GetValue(attribute)?.ToString();

    [RequiresUnreferencedCode("Reads a property of the value object annotation.")]
    private static int ReadInt32(Type attributeType, object attribute, string propertyName)
        => attributeType.GetProperty(propertyName)?.GetValue(attribute) is int value ? value : -1;
}
