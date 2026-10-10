using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Maps each property holding a collection of value objects as a primitive collection, whose elements get the converter,
/// the comparer and the length of their value object.
/// </summary>
/// <remarks>
/// <para>
/// Entity Framework Core takes a collection of a type it cannot map, <c>List&lt;Iban&gt;</c>, for a navigation, and
/// refuses the model. The convention makes such a property a primitive collection when the entity type or the complex
/// type holding it is added, as Entity Framework Core makes one of a <c>List&lt;string&gt;</c>. It takes only a property
/// Entity Framework Core would discover itself, one with a setter of any accessibility, so that a read-only property,
/// which a model leaves out today, stays out of it.
/// </para>
/// <para>
/// The elements are configured as the model finalizes, so that a primitive collection the application declares itself,
/// a read-only property over a backing field among them, gets them too. An element whose converter the application set
/// is left as it is.
/// </para>
/// <para>
/// An element of <c>TSelf?</c> over text stores a value the value object rejects as a <c>null</c>, as a <c>TSelf?</c>
/// column stores a <c>NULL</c>. Over a value type it takes the converter of <c>TSelf</c>, which refuses such a value:
/// the JSON writer of Entity Framework Core throws a <see cref="NullReferenceException"/> on the <c>null</c> a nullable
/// converter hands it there.
/// </para>
/// <para>
/// Built against Entity Framework Core 10, it runs under 11 as well: it marks a primitive collection through
/// <see cref="IConventionProperty.SetElementType(Type?, bool)"/>, and configures the elements while the model finalizes,
/// since Entity Framework Core 11 removed <c>IConventionPropertyBuilder.SetElementType</c> and
/// <c>IPropertyElementTypeChangedConvention</c>, which a package built against 10 would fail to find there.
/// </para>
/// </remarks>
/// <param name="valueObjects">The value objects the convention maps, by type.</param>
/// <param name="definitions">The generic definitions the convention maps.</param>
/// <param name="strict">Whether values read from the database are validated again.</param>
internal sealed class ValueObjectCollectionConvention(
    IReadOnlyDictionary<Type, ValueObjectDescriptor> valueObjects,
    IReadOnlySet<Type> definitions,
    bool strict) : IEntityTypeAddedConvention, IComplexPropertyAddedConvention, IModelFinalizingConvention
{
    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <inheritdoc />
    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
        => Discover(entityTypeBuilder);

    /// <inheritdoc />
    public void ProcessComplexPropertyAdded(
        IConventionComplexPropertyBuilder propertyBuilder,
        IConventionContext<IConventionComplexPropertyBuilder> context)
        => Discover(propertyBuilder.Metadata.ComplexType.Builder);

    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            ConfigureElements(entityType);
        }
    }

    /// <summary>
    /// Makes each property of a type that holds a collection of value objects, and that Entity Framework Core would map,
    /// a primitive collection.
    /// </summary>
    /// <param name="typeBuilder">Builder of the entity type or the complex type.</param>
    private void Discover(IConventionTypeBaseBuilder typeBuilder)
    {
        foreach (var member in typeBuilder.Metadata.ClrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // The setter is looked up on the type declaring the property: a private one declared by a base class is out
            // of sight of the property the derived type reflects.
            if (member.GetIndexParameters().Length == 0
                && member.DeclaringType!.GetProperty(member.Name, Declared)!.SetMethod is not null
                && ElementTypeOf(member.PropertyType) is { } element
                && TryDescribe(Nullable.GetUnderlyingType(element) ?? element, out _)
                && typeBuilder.Property(member)?.Metadata is { } property)
            {
                // The property, not its builder: Entity Framework Core 11 has no
                // IConventionPropertyBuilder.SetElementType.
                property.SetElementType(element);
            }
        }
    }

    /// <summary>
    /// Configures each element of a value object a type holds, and those its complex types hold.
    /// </summary>
    /// <param name="type">The entity type or the complex type.</param>
    private void ConfigureElements(IConventionTypeBase type)
    {
        foreach (var property in type.GetDeclaredProperties())
        {
            if (property.GetElementType() is not { } elementType
                || elementType.GetValueConverterConfigurationSource() == ConfigurationSource.Explicit)
            {
                continue;
            }

            var optional = Nullable.GetUnderlyingType(elementType.ClrType);
            if (TryDescribe(optional ?? elementType.ClrType, out var descriptor))
            {
                // The length is the descriptor's, as for a property: a registration made by hand may give a value object
                // another schema than the one its type declares.
                descriptor.Accept(new ElementConversion(
                    elementType.Builder,
                    optional is not null,
                    strict,
                    descriptor.Schema.MaxLength));
            }
        }

        foreach (var complexProperty in type.GetDeclaredComplexProperties())
        {
            ConfigureElements(complexProperty.ComplexType);
        }
    }

    /// <summary>
    /// Finds the descriptor of a value object the convention maps, a construction of a generic one included.
    /// </summary>
    /// <param name="type">The type of an element.</param>
    /// <param name="descriptor">The descriptor of the value object, when the convention maps it.</param>
    /// <returns><see langword="true"/> when the convention maps the type.</returns>
    private bool TryDescribe(Type type, out ValueObjectDescriptor descriptor)
    {
        if (valueObjects.TryGetValue(type, out descriptor!))
        {
            return true;
        }

        if (!type.IsConstructedGenericType || !definitions.Contains(type.GetGenericTypeDefinition()))
        {
            return false;
        }

        // The registry describes every construction of a definition it holds, by reflection where nothing registered it.
        _ = ValueObjectRegistry.TryResolve(type, out descriptor!);

        return true;
    }

    /// <summary>
    /// Reads the element type of an <see cref="IEnumerable{T}"/>, or of a type implementing one, a single-dimensional
    /// array among them.
    /// </summary>
    /// <param name="type">The type of a property.</param>
    /// <returns>The element type, or <see langword="null"/> when the type is no sequence.</returns>
    private static Type? ElementTypeOf(Type type)
        => (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? type
                : type.GetInterfaces().FirstOrDefault(static candidate =>
                    candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            ?.GetGenericArguments()[0];

    /// <summary>
    /// Sets the converter, the comparer and the length of an element, closed over the type arguments its descriptor hands
    /// back.
    /// </summary>
    /// <param name="element">Builder of the element.</param>
    /// <param name="optional">Whether the element is the nullable value object.</param>
    /// <param name="strict">Whether what is read is validated again.</param>
    /// <param name="maxLength">The length the descriptor declares, which sizes the element.</param>
    private sealed class ElementConversion(
        IConventionElementTypeBuilder element,
        bool optional,
        bool strict,
        int? maxLength) : IValueObjectVisitor<bool>
    {
        /// <summary>
        /// Sets the converter, the comparer and the length of the element, by their types, which a compiled model
        /// instantiates as they stand.
        /// </summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns><see langword="true"/>, once the element is configured.</returns>
        public bool Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            // An optional element over text takes the converter of a TSelf? property, which stores a value the value
            // object rejects as a null. Over a value type, Entity Framework Core cannot write the null such a converter
            // hands it, so the element takes the converter of TSelf, which refuses the value; a value object written by
            // hand over another reference type has no nullable converter, and keeps its own.
            var required = ConverterTypes.Required<TSelf, TValue>(strict);
            var converter = optional && !typeof(TValue).IsValueType
                ? ConverterTypes.Optional<TSelf, TValue>(strict) ?? required
                : required;

            // HasConverter, not HasConversion: on an element type builder, HasConversion(Type) sets the provider type.
            element.HasConverter(converter);
            element.HasValueComparer(
                optional ? typeof(NullableValueObjectComparer<TSelf>) : typeof(ValueObjectComparer<TSelf>));

            if (maxLength is { } length)
            {
                element.HasMaxLength(length);
            }

            return true;
        }
    }
}
