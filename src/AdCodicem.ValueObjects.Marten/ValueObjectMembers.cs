using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using Marten;
using Marten.Linq.Members;
using Marten.Linq.Members.ValueCollections;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// Hands Marten its own value-type member for each value object a LINQ expression, a duplicated field or a compiled query
/// meets, and its own collection member for a collection of one, closed over the value object through the descriptor's
/// visitor, and adds the value object's nullable type to Marten's value types first.
/// </summary>
/// <remarks>
/// Marten asks the member sources of its options before its own, for every member it resolves, once per member of each
/// document type, and keeps the member it gets. A member of type <c>TSelf?</c> gets the member of <c>TSelf</c>, as Marten's
/// own value types do, and a collection of either Marten's own collection member.
/// </remarks>
internal sealed class ValueObjectMembers : IMemberSource
{
    private const string ReflectionGuarded =
        "ValueObjectRegistry.TryResolve is reached only where RuntimeFeature.IsDynamicCodeSupported is true: without "
        + "dynamic code, as under native AOT, a value object nothing registered is refused with a NotSupportedException.";

    /// <summary>The value objects known so far, by type: taught one by one, or described from the registry.</summary>
    private readonly ConcurrentDictionary<Type, MartenValueObject> _known = new();

    /// <summary>
    /// Gets or sets whether a value object nobody taught one by one is described from the registry, as
    /// <see cref="ValueObjectStoreOptionsExtensions.UseValueObjects(StoreOptions)"/> asks.
    /// </summary>
    public bool FromRegistry { get; set; }

    /// <inheritdoc />
    public bool TryResolve(
        IQueryableMember parent,
        StoreOptions options,
        MemberInfo memberInfo,
        Type memberType,
        [NotNullWhen(true)] out IQueryableMember? member)
    {
        // A value object over a type Marten does not query is left to Marten, which refuses it as it refuses that type.
        if (TryFind(Nullable.GetUnderlyingType(memberType) ?? memberType, out var valueObject) && valueObject.IsQueryable)
        {
            valueObject.AddTo(options);
            member = valueObject.CreateMember(parent, options, memberInfo);
            return true;
        }

        // A collection of value objects is Marten's own collection member, which Marten builds for an element type its list
        // of value types holds: the value object stays out of that list (MartenValueObject.AddTo), so the member is built
        // here, as Marten builds it.
        if (ElementOf(memberType) is { } element && TryFind(element, out valueObject) && valueObject.IsQueryable && valueObject.IsHeldBy(memberType))
        {
            valueObject.AddTo(options);
            member = new ValueCollectionMember(options, parent, options.Serializer().Casing, memberInfo);
            return true;
        }

        member = null;
        return false;
    }

    /// <summary>Keeps a value object taught one by one, which wins over the registry's description of it.</summary>
    /// <param name="valueObject">The value object.</param>
    /// <returns>The same value object.</returns>
    public MartenValueObject Add(MartenValueObject valueObject) => _known[valueObject.ValueObjectType] = valueObject;

    /// <summary>Gets the value object a descriptor stands for, closed through its visitor once.</summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>The value object.</returns>
    public MartenValueObject Describe(ValueObjectDescriptor descriptor)
        => _known.GetOrAdd(descriptor.ValueObjectType, static (_, d) => d.Accept(MartenValueObjectFactory.Instance), descriptor);

    /// <summary>
    /// Finds the value object a type is: one taught one by one, or, under <see cref="FromRegistry"/>, one the registry
    /// describes.
    /// </summary>
    /// <param name="type">The type, unwrapped from <see cref="Nullable{T}"/>.</param>
    /// <param name="valueObject">The value object when found.</param>
    /// <returns><see langword="true"/> when the type is a value object this store knows.</returns>
    /// <exception cref="NotSupportedException">
    /// The type is a value object nothing registered, and the runtime does not support the dynamic code describing it by
    /// reflection takes, as under native AOT.
    /// </exception>
    public bool TryFind(Type type, [NotNullWhen(true)] out MartenValueObject? valueObject)
    {
        if (_known.TryGetValue(type, out valueObject))
        {
            return true;
        }

        if (!FromRegistry || !type.IsValueType)
        {
            return false;
        }

        if (ValueObjectRegistry.TryGet(type, out var descriptor))
        {
            valueObject = Describe(descriptor);
            return true;
        }

        if (!ValueObjectRegistry.IsValueObject(type))
        {
            return false;
        }

        valueObject = Describe(Resolve(type));
        return true;
    }

    /// <summary>Tells whether a type is a value object this store knows, without describing it.</summary>
    /// <param name="type">The type, unwrapped from <see cref="Nullable{T}"/>.</param>
    /// <returns><see langword="true"/> when the type is a value object this store knows.</returns>
    public bool Knows(Type type)
        => _known.TryGetValue(type, out var valueObject)
            ? valueObject.IsQueryable
            : FromRegistry
              && type.IsValueType
              && ValueObjectRegistry.GetUnderlyingType(type) is { } underlying
              && Weasel.Postgresql.PostgresqlProvider.Instance.HasTypeMapping(underlying);

    /// <summary>
    /// Gets the element type of a collection as Marten reads it, an array's element or the first type argument, unwrapped
    /// from <see cref="Nullable{T}"/>; whether the type is a collection of it is the value object's to tell.
    /// </summary>
    /// <param name="type">The type of a member.</param>
    /// <returns>The candidate element type, or <see langword="null"/> for a type taking no single type argument.</returns>
    private static Type? ElementOf(Type type)
    {
        var element = type.IsArray ? type.GetElementType() : type.IsGenericType && type.GetGenericArguments() is [var argument] ? argument : null;
        return element is null ? null : Nullable.GetUnderlyingType(element) ?? element;
    }

    /// <summary>Describes by reflection a value object nothing registered, where the runtime supports dynamic code.</summary>
    /// <param name="type">The value object.</param>
    /// <returns>Its descriptor.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = ReflectionGuarded)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = ReflectionGuarded)]
    private static ValueObjectDescriptor Resolve(Type type)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new NotSupportedException(
                $"'{TypeNames.Of(type)}' is a value object nothing registered, and describing it by reflection takes dynamic "
                + "code, which this runtime does not support. Register it at start-up, ValueObjectRegistry.Register<TSelf, "
                + "TValue>(TSelf.Schema) for a value object written by hand, ValueObjectRegistry.Register<TSelf, TValue>("
                + "static () => new TSelf.ValueJsonConverter()) for a construction of a generic one, or teach it to the store "
                + "with UseValueObject<TSelf, TValue>().");
        }

        // IsValueObject answered yes, and TryResolve describes exactly what IsValueObject accepts.
        _ = ValueObjectRegistry.TryResolve(type, out var descriptor);
        return descriptor!;
    }

    /// <summary>Closes the package's view of a value object over the type arguments its descriptor hands back.</summary>
    private sealed class MartenValueObjectFactory : IValueObjectVisitor<MartenValueObject>
    {
        public static readonly MartenValueObjectFactory Instance = new();

        /// <inheritdoc />
        public MartenValueObject Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new MartenValueObject<TSelf, TValue>();
    }
}
