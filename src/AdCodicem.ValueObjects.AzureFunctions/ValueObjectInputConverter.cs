using System.Collections.Concurrent;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using Microsoft.Azure.Functions.Worker.Converters;

namespace AdCodicem.ValueObjects.AzureFunctions;

/// <summary>
/// Binds a value object from the text of a route value, a query value or other binding data, through its
/// <c>TryParse</c>.
/// </summary>
/// <remarks>
/// <para>
/// The isolated worker reads no <c>[TypeConverter]</c>: without this converter, the text of a value object falls through
/// to the worker's JSON conversion, which refuses a valid <c>Guid</c>, <c>string</c> or <c>DateOnly</c> and fails every
/// such invocation. <see cref="ValueObjectFunctionsWorkerApplicationBuilderExtensions.UseValueObjects"/> puts this
/// converter first among the worker's registered converters, or right behind the ASP.NET Core integration's, which binds
/// an <c>HttpRequest</c> and an <c>HttpRequestData</c> alone.
/// </para>
/// <para>
/// It reads a parameter whose type, <see cref="Nullable{T}"/> unwrapped, is a value object the registry describes, and
/// whose source is text: the text is parsed as it stands, then, when that fails and it is a JSON string literal, as the
/// string it encodes, since the host sends binding data of another type than <see cref="string"/> as JSON, and, for a
/// value object over <see cref="decimal"/>, when it is a JSON number, as the number the worker's JSON conversion reads,
/// an exponent included, which a decimal's <c>TryParse</c> refuses. Empty text is parsed too, and refused by the type's
/// own rule. A parameter with no binding data is left to the worker, which binds <see langword="null"/> to a nullable
/// value object and its default value to an optional one, and fails a required one itself. A parameter carrying an input
/// or trigger binding attribute is left to that binding and to the worker's serializer, which honours the generated JSON
/// converter: naming this converter on the parameter with <c>[InputConverter(typeof(ValueObjectInputConverter))]</c>
/// reads a raw-text payload instead, and one that names another converter is left to it.
/// </para>
/// <para>
/// A refusal is <see cref="ConversionResult.Failed(Exception)"/> with a <see cref="FormatException"/> whose message names
/// the type, the rule and its code, and whose <see cref="Exception.Data"/> holds the code under
/// <see cref="ValueObjectErrors.ErrorCodeKey"/>; neither holds the refused text. The worker keeps only the exception's
/// text, in the <c>FunctionInputConverterException</c> it throws, which is why the message carries the code.
/// </para>
/// <para>
/// A generated value object registers itself; a value object written by hand and a construction of a generic one are
/// described by reflection where the runtime supports dynamic code, and otherwise once registered by hand. Only the types
/// described are kept, so that one registered after a first miss is read from then on.
/// </para>
/// </remarks>
public sealed class ValueObjectInputConverter : IInputConverter
{
    private static readonly ConcurrentDictionary<Type, ValueObjectTextConversion> Conversions = new();

    /// <summary>Parses the text the host bound to the parameter, or leaves the parameter to the next converter.</summary>
    /// <param name="context">The parameter's conversion context.</param>
    /// <returns>
    /// The value object, a failure carrying the rule's code, or <see cref="ConversionResult.Unhandled"/> for a parameter
    /// that is not this converter's.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Source is not string text
            || !ValueObjectBindingText.IsReadAsText(context.Properties, typeof(ValueObjectInputConverter))
            || !TryGetConversion(context.TargetType, out var conversion))
        {
            return new(ConversionResult.Unhandled());
        }

        return new(conversion.Convert(text));
    }

    /// <summary>Finds the conversion of a type, keeping it only once the type is described.</summary>
    private static bool TryGetConversion(Type type, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ValueObjectTextConversion? conversion)
    {
        if (Conversions.TryGetValue(type, out conversion))
        {
            return true;
        }

        if (!ValueObjectBindingText.TryDescribe(type, out var descriptor))
        {
            return false;
        }

        conversion = Conversions.GetOrAdd(type, descriptor.Accept(ValueObjectTextConversion.For.Instance));
        return true;
    }
}
