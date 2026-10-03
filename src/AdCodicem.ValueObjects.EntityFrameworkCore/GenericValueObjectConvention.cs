using AdCodicem.ValueObjects.Metadata;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Maps each property of a construction of a generic value object, which no configuration made in advance can name.
/// </summary>
/// <remarks>
/// <para>
/// The convention configures a value object that is not generic up front, closing its converter over it. A generic
/// value object has no such type: its definition is configured up front, which makes every construction of it a
/// scalar property, and this convention closes the converter, the comparer and the length over the construction of
/// each property, as the model meets it.
/// </para>
/// <para>
/// A property of a nullable construction gets the converter that stores a value the value object rejects as
/// <c>NULL</c>, where a property of the construction itself refuses it.
/// </para>
/// <para>
/// A property configured explicitly, through <c>HasValueObjectConversion</c> or a converter of the application's own,
/// keeps that configuration: an explicit one outranks a convention.
/// </para>
/// </remarks>
/// <param name="definitions">The generic definitions the convention configured.</param>
/// <param name="strict">Whether values read from the database are validated again.</param>
internal sealed class GenericValueObjectConvention(IReadOnlySet<Type> definitions, bool strict) : IPropertyAddedConvention
{
    /// <inheritdoc />
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        var optional = Nullable.GetUnderlyingType(propertyBuilder.Metadata.ClrType);
        var type = optional ?? propertyBuilder.Metadata.ClrType;
        if (!type.IsConstructedGenericType
            || !definitions.Contains(type.GetGenericTypeDefinition())
            || !ValueObjectRegistry.TryResolve(type, out var descriptor))
        {
            return;
        }

        // A nullable construction stores a value the value object rejects as NULL, where any other throws.
        var converter = (optional is null ? null : ConverterTypes.Optional(type, descriptor.ValueType, strict))
            ?? ConverterTypes.Required(type, descriptor.ValueType, strict);
        var comparer = typeof(ValueObjectComparer<>).MakeGenericType(type);

        propertyBuilder.HasConversion((ValueConverter)Activator.CreateInstance(converter)!);
        propertyBuilder.HasValueComparer((ValueComparer)Activator.CreateInstance(comparer)!);

        if (descriptor.Schema.MaxLength is { } maxLength)
        {
            propertyBuilder.HasMaxLength(maxLength);
        }
    }
}
