using System.ComponentModel;
using System.Globalization;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// Converts each construction of a generic value object to and from text and its underlying value.
/// </summary>
/// <remarks>
/// <para>
/// The generator puts this converter in the <c>[TypeConverter]</c> attribute of a generic value object, where a value
/// object that is not generic names its own converter: an attribute cannot name a type through a type parameter.
/// <see cref="TypeDescriptor"/> hands it the construction it converts, and it converts through the construction's
/// descriptor, exactly as the converter the generator writes on a value object that is not generic does.
/// </para>
/// <para>
/// It closes nothing at run time. A construction registered with
/// <see cref="ValueObjectRegistry.Register{TSelf, TValue}(ValueObjectSchema, System.Text.Json.Serialization.JsonConverter{TSelf})"/>,
/// as native AOT requires, is found without reflection; any other is described by the registry the first time it is
/// asked for it.
/// </para>
/// </remarks>
public sealed class GenericValueObjectTypeConverter : TypeConverter
{
    private const string Resolution =
        "A construction registered by hand, as native AOT requires, is found without reflection. Any other is described "
        + "by the registry by reflection, which TypeDescriptor, itself reflection-based, already depends on.";

    private readonly ValueObjectDescriptor _descriptor;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericValueObjectTypeConverter"/> class for a construction.
    /// </summary>
    /// <param name="type">The construction, <c>Code&lt;Order&gt;</c>, as <see cref="TypeDescriptor"/> hands it over.</param>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not a value object.</exception>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Resolution)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = Resolution)]
    public GenericValueObjectTypeConverter(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!ValueObjectRegistry.TryResolve(type, out var descriptor))
        {
            throw new ArgumentException($"'{type}' is not a value object.", nameof(type));
        }

        _descriptor = descriptor;
    }

    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || sourceType == _descriptor.ValueType || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    /// <exception cref="ValueObjectException">The text or the value is not one the value object accepts.</exception>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            if (_descriptor.TryParse(text, culture ?? CultureInfo.InvariantCulture, out var parsed, out var validation))
            {
                return parsed;
            }

            throw new ValueObjectException(
                $"'{text}' is not a valid {Name(_descriptor.ValueObjectType)}: {validation.ErrorMessage}",
                _descriptor.ValueObjectType,
                validation.ErrorCode,
                text);
        }

        return _descriptor.ValueType.IsInstanceOfType(value)
            ? _descriptor.Create(value)
            : base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || destinationType == _descriptor.ValueType || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is not null && _descriptor.ValueObjectType.IsInstanceOfType(value))
        {
            if (destinationType == typeof(string))
            {
                return ((IFormattable)value).ToString(null, culture ?? CultureInfo.InvariantCulture);
            }

            if (destinationType == _descriptor.ValueType)
            {
                return _descriptor.GetValue(value);
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    /// <summary>
    /// Names a construction as its declaration names it, without the arity the run time appends.
    /// </summary>
    private static string Name(Type type)
        => type.Name.IndexOf('`', StringComparison.Ordinal) is var tick and >= 0 ? type.Name[..tick] : type.Name;
}
