using System.Globalization;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// Probes one value object through the visitor its descriptor accepts, which hands over its type arguments, so that
/// everything below is typed code the AOT compiler sees whole: the typed path, the boxed descriptor beside it, the
/// source-generated JSON context and the JSON Schema exported for it.
/// </summary>
/// <param name="descriptor">The descriptor visited.</param>
/// <param name="report">Where each scenario is written.</param>
/// <param name="extraTexts">Texts to probe beside those of the underlying type, such as an identifier minted earlier.</param>
internal sealed class ValueObjectProbe(ValueObjectDescriptor descriptor, Report report, IReadOnlyList<string> extraTexts)
    : IValueObjectVisitor<bool>
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public bool Visit<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = Names.Of(typeof(TSelf));
        if (typeof(TSelf) != descriptor.ValueObjectType || typeof(TValue) != descriptor.ValueType)
        {
            report.Fail(name, $"visited as {Names.Of(typeof(TSelf))} over {Names.Of(typeof(TValue))}, described as {Names.Of(descriptor.ValueObjectType)} over {Names.Of(descriptor.ValueType)}");
            return false;
        }

        if (AppJsonContext.Default.GetTypeInfo(typeof(TSelf)) is not JsonTypeInfo<TSelf> typeInfo
            || AppJsonContext.Default.GetTypeInfo(typeof(TValue)) is not JsonTypeInfo<TValue> valueTypeInfo)
        {
            report.Fail(name, "is missing from AppJsonContext, or its underlying type is");
            return false;
        }

        report.Line(name, $"over {Names.Of(typeof(TValue))}; max length {TSelf.Schema.MaxLength?.ToString(Invariant) ?? "none"}, {TSelf.Schema.KnownValues.Length} known values");
        foreach (var profile in (ReadOnlySpan<ValueObjectJsonSchemaProfile>)[ValueObjectJsonSchemaProfile.OpenApi, ValueObjectJsonSchemaProfile.LanguageModel])
        {
            var schema = JsonSchemaExporter.GetJsonSchemaAsNode(
                typeInfo,
                new JsonSchemaExporterOptions { TransformSchemaNode = ValueObjectJsonSchema.CreateTransform(profile) });
            report.Line(name, $"JSON Schema for {profile}: {schema.ToJsonString()}");
        }

        foreach (var text in Underlying.Texts(typeof(TValue)).Concat(extraTexts))
        {
            Probe<TSelf, TValue>(name, text, typeInfo, valueTypeInfo);
        }

        return true;
    }

    private void Probe<TSelf, TValue>(string name, string text, JsonTypeInfo<TSelf> typeInfo, JsonTypeInfo<TValue> valueTypeInfo)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var subject = $"{name} {Underlying.Quote(text)}";
        var outcome = new List<string>();
        try
        {
            // The text, through the typed path and through the descriptor.
            if (TSelf.TryParse(text, Invariant, out var parsed, out var validation))
            {
                outcome.Add($"TryParse {Underlying.Show(parsed)}");
                outcome.Add(Same("Parse", TSelf.Parse(text, Invariant), parsed));
            }
            else
            {
                outcome.Add($"TryParse refused {validation.ErrorCode}");
                outcome.Add(Throws("Parse", () => TSelf.Parse(text, Invariant)));
            }

            outcome.Add(descriptor.TryParse(text, Invariant, out var boxed, out var boxedValidation)
                ? $"boxed TryParse {descriptor.Format(boxed!)}"
                : $"boxed TryParse refused {boxedValidation.ErrorCode}");

            // The raw value the text reads as, through the typed path, the descriptor and JSON.
            if (!Underlying.TryRead<TValue>(text, out var raw))
            {
                outcome.Add($"not a {Names.Of(typeof(TValue))}");
            }
            else
            {
                var rawJson = Underlying.Json(raw, valueTypeInfo);
                if (TSelf.TryCreate(raw, out var created, out var createValidation))
                {
                    outcome.Add($"TryCreate {Underlying.Show(created)}");
                    outcome.Add(Same("Create", TSelf.Create(raw), created));
                    outcome.Add(Formats(created));
                    var written = JsonSerializer.Serialize(created, typeInfo);
                    outcome.Add(rawJson is null
                        ? $"JSON holds no such number, written {written}"
                        : $"JSON {rawJson} read as {Underlying.Show(JsonSerializer.Deserialize(rawJson, typeInfo))}, written {written}");
                }
                else
                {
                    outcome.Add($"TryCreate refused {createValidation.ErrorCode}");
                    outcome.Add(Throws("Create", () => TSelf.Create(raw)));
                    outcome.Add(rawJson is null
                        ? "JSON holds no such number"
                        : Throws($"JSON {rawJson}", () => JsonSerializer.Deserialize(rawJson, typeInfo)));
                }

                outcome.Add(descriptor.TryCreate(raw, out var boxedCreated, out var boxedCreateValidation)
                    ? $"boxed TryCreate {descriptor.Format(boxedCreated!)}, value {Underlying.Show(descriptor.GetValue(boxedCreated!))}"
                    : $"boxed TryCreate refused {boxedCreateValidation.ErrorCode}");
            }
        }
        catch (Exception exception)
        {
            outcome.Add($"threw {exception.GetType().Name}: {exception.Message}");
            report.Fail(subject, string.Join("; ", outcome));
            return;
        }

        report.Line(subject, string.Join("; ", outcome));
    }

    private static string Same<TSelf>(string what, TSelf actual, TSelf expected)
        where TSelf : struct, IEquatable<TSelf>
        => actual.Equals(expected) ? $"{what} same" : $"{what} {Underlying.Show(actual)}";

    private static string Formats<TSelf>(TSelf value)
        where TSelf : struct, ISpanFormattable
    {
        var text = value.ToString(null, Invariant);
        Span<char> destination = stackalloc char[512];
        return value.TryFormat(destination, out var written, default, Invariant)
            ? destination[..written].SequenceEqual(text) ? "formats same" : $"formats {Underlying.Quote(text)} but TryFormat {Underlying.Quote(destination[..written].ToString())}"
            : $"formats {Underlying.Quote(text)} but TryFormat refuses";
    }

    /// <summary>Runs what should refuse the value, and describes the refusal; anything but a refusal propagates.</summary>
    private static string Throws<T>(string what, Func<T> run)
    {
        try
        {
            return $"{what} accepted {Underlying.Show(run())}";
        }
        catch (FormatException exception)
        {
            return $"{what} threw {Report.Describe(exception)}";
        }
        catch (JsonException exception)
        {
            return $"{what} threw {Report.Describe(exception)}";
        }
    }
}
