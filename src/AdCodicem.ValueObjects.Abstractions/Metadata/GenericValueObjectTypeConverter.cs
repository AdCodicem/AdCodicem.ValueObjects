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
/// <para>
/// Over a number, it converts from and to every numeric type a value object may wrap, with the checked conversion the
/// converter the generator writes performs: a number the underlying type cannot hold whole is refused as
/// <see cref="ValueObjectErrorCodes.NotParsable"/>, and a numeric type that cannot hold the value whole is refused with
/// <see cref="NotSupportedException"/>. Nothing is ever truncated.
/// </para>
/// <para>
/// Text or a number it refuses is never quoted in the message of the <see cref="ValueObjectException"/> it throws, and
/// is left out of its <see cref="ValueObjectException.AttemptedValue"/> too when the generic definition carries an
/// attribute derived from <c>Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute</c>, other than
/// <c>NoDataClassificationAttribute</c>, as the <c>Parse</c> the generator writes does.
/// </para>
/// </remarks>
public sealed class GenericValueObjectTypeConverter : TypeConverter
{
    private const string Resolution =
        "A construction registered by hand, as native AOT requires, is found without reflection. Any other is described "
        + "by the registry by reflection, which TypeDescriptor, itself reflection-based, already depends on.";

    private readonly ValueObjectDescriptor _descriptor;

    /// <summary>Whether the underlying type is a number, which converts from and to any other.</summary>
    private readonly bool _numeric;

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
        _numeric = NumberConversion.IsNumber(descriptor.ValueType);
    }

    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string)
            || sourceType == _descriptor.ValueType
            || (_numeric && NumberConversion.IsNumber(sourceType))
            || base.CanConvertFrom(context, sourceType);

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

            // As the generated Parse: the message names the type and the rule, never the text, and the text stays off
            // AttemptedValue too when the value object is classified as sensitive data.
            throw new ValueObjectException(
                $"'{Name(_descriptor.ValueObjectType)}' rejected the supplied text: {validation.ErrorMessage}",
                _descriptor.ValueObjectType,
                validation.ErrorCode,
                Attempted(text));
        }

        if (_descriptor.ValueType.IsInstanceOfType(value))
        {
            return _descriptor.Create(value);
        }

        if (_numeric)
        {
            if (NumberConversion.TryConvert(value, _descriptor.ValueType, out var raw))
            {
                return _descriptor.Create(raw);
            }

            // Only a number is refused here: anything else, null included, is a type the converter does not read.
            if (IsNumber(value))
            {
                throw new ValueObjectException(
                    $"'{Name(_descriptor.ValueObjectType)}' rejected the supplied number: The number is not a valid "
                    + $"{NumberConversion.NameOf(_descriptor.ValueType)}.",
                    _descriptor.ValueObjectType,
                    ValueObjectErrorCodes.NotParsable,
                    Attempted(value));
            }
        }

        return base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string)
            || destinationType == _descriptor.ValueType
            || (_numeric && destinationType is not null && NumberConversion.IsNumber(destinationType))
            || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// The destination is not a type the converter writes, or is a numeric type that cannot hold the value whole.
    /// </exception>
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

            // A numeric type that cannot hold the value whole falls through to the base, which refuses it.
            if (_numeric && NumberConversion.TryConvert(_descriptor.GetValue(value)!, destinationType, out var number))
            {
                return number;
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    /// <summary>
    /// Gives what a refusal carries as its <see cref="ValueObjectException.AttemptedValue"/>: what was refused, or
    /// <see langword="null"/> when the value object is classified as sensitive data.
    /// </summary>
    private object? Attempted(object rejected)
        => DataClassificationReader.IsClassified(_descriptor.ValueObjectType) ? null : rejected;

    /// <summary>
    /// Tells a number of a type a value object may wrap, from what a caller hands over, which may be null.
    /// </summary>
    private static bool IsNumber(object? value) => value is not null && NumberConversion.IsNumber(value.GetType());

    /// <summary>
    /// Names a construction as its declaration names it, without the arity the run time appends.
    /// </summary>
    private static string Name(Type type)
        => type.Name.IndexOf('`', StringComparison.Ordinal) is var tick and >= 0 ? type.Name[..tick] : type.Name;
}
