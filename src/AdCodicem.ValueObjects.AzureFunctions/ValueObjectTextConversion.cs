using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using Microsoft.Azure.Functions.Worker.Converters;

namespace AdCodicem.ValueObjects.AzureFunctions;

/// <summary>
/// Converts the text of one value object, closed over its type through the descriptor's visitor rather than by
/// reflection.
/// </summary>
internal abstract class ValueObjectTextConversion
{
    /// <summary>Parses the text, and reports the value object or the rule that refused it.</summary>
    /// <param name="text">The text the host bound to the parameter.</param>
    /// <returns>The conversion's result.</returns>
    public abstract ConversionResult Convert(string text);

    /// <summary>Creates the conversion of the value object a descriptor stands for.</summary>
    internal sealed class For : IValueObjectVisitor<ValueObjectTextConversion>
    {
        public static readonly For Instance = new();

        /// <summary>Creates the conversion.</summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns>The conversion of the value object.</returns>
        public ValueObjectTextConversion Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ValueObjectTextConversion<TSelf, TValue>();
    }
}

/// <summary>Converts the text of a value object through <see cref="ValueObjectBindingText.TryParse{TSelf, TValue}"/>.</summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// A refusal without a reason, which a parser written by hand may return, is reported as the minimal API package reports
/// it: "The value is not a valid …", with <see cref="ValueObjectErrorCodes.NotParsable"/>.
/// </remarks>
internal sealed class ValueObjectTextConversion<TSelf, TValue> : ValueObjectTextConversion
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    public override ConversionResult Convert(string text)
    {
        if (ValueObjectBindingText.TryParse<TSelf, TValue>(text, out var result, out var validation))
        {
            return ConversionResult.Success(result);
        }

        var code = validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable;
        var name = TypeNames.Of(typeof(TSelf));
        var refusal = new FormatException(validation.ErrorMessage is { } message
            ? $"The value is not a valid {name}: {message} ({code})"
            : $"The value is not a valid {name}. ({code})");
        refusal.Data[ValueObjectErrors.ErrorCodeKey] = code;

        return ConversionResult.Failed(refusal);
    }
}
