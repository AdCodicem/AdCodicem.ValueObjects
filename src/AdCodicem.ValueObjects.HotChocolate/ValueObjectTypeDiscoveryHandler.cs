using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.Metadata;
using HotChocolate.Internal;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace AdCodicem.ValueObjects.HotChocolate;

/// <summary>
/// Answers each value object the schema references with a scalar of its own, closed over it through the descriptor's
/// visitor, before HotChocolate infers an object or an input object type from the struct.
/// </summary>
/// <remarks>
/// HotChocolate creates one handler per schema it builds, and asks it about every runtime type it meets, before its own
/// scalar handler and its default one. The scalar of each value object is kept for the schema, since a type system object
/// belongs to the schema that completes it.
/// </remarks>
internal sealed class ValueObjectTypeDiscoveryHandler : TypeDiscoveryHandler
{
    private const string UnregisteredOnly =
        "ValueObjectRegistry.TryResolve reflects only for a value object nothing registered: one written by hand, or a "
        + "construction of a generic one. A generated value object registers itself statically. Without dynamic code, as "
        + "under native AOT, it is not called, and AddValueObjectScalar<TSelf, TValue>() maps such a type at compile time.";

    private readonly Dictionary<Type, ScalarType?> _scalars = [];

    /// <inheritdoc />
    public override bool TryInferType(
        TypeReference typeReference,
        TypeDiscoveryInfo typeInfo,
        [NotNullWhen(true)] out TypeReference[]? schemaTypeRefs)
    {
        schemaTypeRefs = ScalarOf(typeInfo.RuntimeType) is { } scalar ? [TypeReference.Create(scalar)] : null;
        return schemaTypeRefs is not null;
    }

    /// <inheritdoc />
    public override bool TryInferKind(TypeReference typeReference, TypeDiscoveryInfo typeInfo, out TypeKind typeKind)
    {
        if (ScalarOf(typeInfo.RuntimeType) is null)
        {
            typeKind = default;
            return false;
        }

        typeKind = TypeKind.Scalar;
        return true;
    }

    /// <summary>
    /// Gets the scalar of a value object, created the first time the schema meets it, or <see langword="null"/> for any
    /// other type.
    /// </summary>
    /// <param name="type">The runtime type HotChocolate meets, a nullable value type already unwrapped.</param>
    /// <returns>The scalar, or <see langword="null"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// The type is a value object nothing registered, and the runtime does not support the dynamic code describing it by
    /// reflection takes, as under native AOT.
    /// </exception>
    private ScalarType? ScalarOf(Type type)
    {
        if (_scalars.TryGetValue(type, out var known))
        {
            return known;
        }

        var scalar = type.IsValueType && Describe(type) is { } descriptor ? descriptor.Accept(ScalarFactory.Instance) : null;
        _scalars[type] = scalar;
        return scalar;
    }

    /// <summary>
    /// Finds the descriptor of a value object, registered, or described by reflection where the runtime supports dynamic
    /// code.
    /// </summary>
    /// <param name="type">A value type.</param>
    /// <returns>The descriptor, or <see langword="null"/> for a type that is not a value object.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = UnregisteredOnly)]
    private static ValueObjectDescriptor? Describe(Type type)
    {
        if (ValueObjectRegistry.TryGet(type, out var descriptor))
        {
            return descriptor;
        }

        if (!ValueObjectRegistry.IsValueObject(type))
        {
            return null;
        }

        // A descriptor built in code first. Describing a value object nothing registered takes dynamic code, which native
        // AOT has not: there, the reflection would fail inside the registry, so the handler says what the type needs.
        return RuntimeFeature.IsDynamicCodeSupported && ValueObjectRegistry.TryResolve(type, out descriptor)
            ? descriptor
            : throw new NotSupportedException(
                $"'{type}' is a value object nothing registered, and describing it by reflection takes dynamic code, which "
                + "this runtime does not support. Register it at start-up, "
                + "ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), or map it to its scalar with "
                + "AddValueObjectScalar<TSelf, TValue>().");
    }

    /// <summary>
    /// Creates the scalar of the value object a descriptor stands for, closed over the type arguments it hands back.
    /// </summary>
    private sealed class ScalarFactory : IValueObjectVisitor<ScalarType>
    {
        public static readonly ScalarFactory Instance = new();

        /// <inheritdoc />
        public ScalarType Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ValueObjectScalarType<TSelf, TValue>(name: null);
    }
}
