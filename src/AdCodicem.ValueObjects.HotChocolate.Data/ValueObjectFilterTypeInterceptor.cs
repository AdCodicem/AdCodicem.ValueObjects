using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.Metadata;
using HotChocolate.Configuration;
using HotChocolate.Data.Filters;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Configurations;

namespace AdCodicem.ValueObjects.HotChocolate.Data;

/// <summary>
/// Replaces the filter HotChocolate.Data infers for a value-object member, an input of the struct's public properties,
/// with the operations of the value object's scalar, closed through the descriptor's visitor, and names a list of them
/// after its element.
/// </summary>
/// <remarks>
/// It runs before the dependencies of a filter input are registered: once they are, the inferred input is part of the
/// schema. It replaces only what HotChocolate infers for a struct no binding names, <c>FilterInputType&lt;TSelf&gt;</c>
/// or a list of it, so that a binding of the application's own, HotChocolate's <c>[ID]</c> and a field typed explicitly
/// win.
/// </remarks>
/// <param name="options">The options of the schema's filters, read when the schema is built.</param>
internal sealed class ValueObjectFilterTypeInterceptor(ValueObjectFilteringOptions options) : TypeInterceptor
{
    private const string UnregisteredOnly =
        "ValueObjectRegistry.TryResolve reflects only for a value object nothing registered: one written by hand, or a "
        + "construction of a generic one. A generated value object registers itself statically. Without dynamic code, as "
        + "under native AOT, it is not called, and AddValueObjectScalar<TSelf, TValue>() with "
        + "BindValueObject<TSelf, TValue>() map such a type at compile time.";

    /// <inheritdoc />
    public override void OnBeforeRegisterDependencies(ITypeDiscoveryContext discoveryContext, TypeSystemConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(discoveryContext);

        if (configuration is not FilterInputTypeConfiguration { EntityType: not null } filter)
        {
            return;
        }

        var inspector = discoveryContext.DescriptorContext.TypeInspector;

        // HotChocolate's own list over one of the package's operation types, which its convention names after the CLR
        // types: named after its element, whoever made it, the interceptor or a binding.
        if (Argument(discoveryContext.Type.GetType(), typeof(ListFilterInputType<>)) is { } element
            && typeof(IValueObjectOperationFilterInputType).IsAssignableFrom(element))
        {
            ValueObjectFilterNames.NameAfter(
                FilterInputTypeDescriptor.From(discoveryContext.DescriptorContext, filter, filter.Scope),
                inspector.GetTypeRef(element, TypeContext.Input, filter.Scope),
                static elementName => "List" + elementName);
            return;
        }

        // The text operations' handlers extend the default convention alone.
        var text = filter.Scope is null && options.TextOperations;
        foreach (var field in filter.Fields)
        {
            // The operation fields, `and` and `or`, reference the filter itself, never what HotChocolate infers for a
            // struct.
            if (field is not FilterFieldConfiguration { Type: ExtendedTypeReference { Type.Source: var source } }
                || Replacement(source, text) is not { } replacement)
            {
                continue;
            }

            field.Type = inspector.GetTypeRef(replacement, TypeContext.Input, filter.Scope);
        }
    }

    /// <summary>
    /// Gets the type that replaces what HotChocolate infers for a struct, when the struct is a value object.
    /// </summary>
    /// <param name="source">The type the field's reference names.</param>
    /// <param name="text">Whether a value object over <see cref="string"/> gets the string operations.</param>
    /// <returns>
    /// The operation type, or HotChocolate's list over it, or <see langword="null"/> to leave the field alone.
    /// </returns>
    private static Type? Replacement(Type source, bool text)
    {
        var types = text ? FilterTypes.Text : FilterTypes.Comparable;
        if (Argument(source, typeof(FilterInputType<>)) is { } single)
        {
            return Describe(single)?.Accept(types).Single;
        }

        return Argument(source, typeof(ListFilterInputType<>)) is { } element && Argument(element, typeof(FilterInputType<>)) is { } inferred
            ? Describe(inferred)?.Accept(types).List
            : null;
    }

    /// <summary>
    /// Gets the type argument of a construction of a generic type definition.
    /// </summary>
    private static Type? Argument(Type type, Type definition)
        => type.IsGenericType && type.GetGenericTypeDefinition() == definition ? type.GenericTypeArguments[0] : null;

    /// <summary>
    /// Finds the descriptor of a value object, registered, or described by reflection where the runtime supports dynamic
    /// code.
    /// </summary>
    /// <param name="type">The entity type of an inferred filter.</param>
    /// <returns>The descriptor, or <see langword="null"/> for a type that is not a value object.</returns>
    /// <exception cref="NotSupportedException">
    /// The type is a value object nothing registered, and the runtime does not support the dynamic code describing it by
    /// reflection takes, as under native AOT.
    /// </exception>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = UnregisteredOnly)]
    private static ValueObjectDescriptor? Describe(Type type)
    {
        if (!type.IsValueType)
        {
            return null;
        }

        if (ValueObjectRegistry.TryGet(type, out var descriptor))
        {
            return descriptor;
        }

        if (!ValueObjectRegistry.IsValueObject(type))
        {
            return null;
        }

        // A descriptor built in code first. Describing a value object nothing registered takes dynamic code, which native
        // AOT has not: there, the reflection would fail inside the registry, so the interceptor says what the type needs.
        return RuntimeFeature.IsDynamicCodeSupported && ValueObjectRegistry.TryResolve(type, out descriptor)
            ? descriptor
            : throw new NotSupportedException(
                $"'{type}' is a value object nothing registered, and describing it by reflection takes dynamic code, which "
                + "this runtime does not support. Register it at start-up, "
                + "ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), or map it to its scalar with "
                + "AddValueObjectScalar<TSelf, TValue>() and bind it in the filter convention with "
                + "BindValueObject<TSelf, TValue>().");
    }

    /// <summary>
    /// Closes the operation type of the value object a descriptor stands for, and HotChocolate's list over it.
    /// </summary>
    /// <param name="text">Whether a value object over <see cref="string"/> gets the string operations.</param>
    private sealed class FilterTypes(bool text) : IValueObjectVisitor<(Type Single, Type List)>
    {
        public static readonly FilterTypes Comparable = new(text: false);

        public static readonly FilterTypes Text = new(text: true);

        /// <inheritdoc />
        public (Type Single, Type List) Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => text && typeof(TValue) == typeof(string)
                ? (typeof(ValueObjectTextOperationFilterInputType<TSelf>), typeof(ListFilterInputType<ValueObjectTextOperationFilterInputType<TSelf>>))
                : (typeof(ValueObjectOperationFilterInputType<TSelf>), typeof(ListFilterInputType<ValueObjectOperationFilterInputType<TSelf>>));
    }
}
