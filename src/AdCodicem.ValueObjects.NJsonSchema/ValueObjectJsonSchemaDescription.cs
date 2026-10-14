using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using JsonSerializerOptions = System.Text.Json.JsonSerializerOptions;

namespace AdCodicem.ValueObjects.NJsonSchema;

/// <summary>
/// Describes a value object on NJsonSchema's object model, as its underlying value, carrying the rules declared on it.
/// </summary>
/// <remarks>
/// <para>
/// What each rule becomes is read from <see cref="ValueObjectSchemaKeywords"/>, which the OpenAPI, Swashbuckle and JSON
/// Schema packages share: this is the host file for NJsonSchema's <see cref="JsonSchema"/>, built as the one those
/// OpenAPI packages share for Microsoft.OpenApi's object model. NJsonSchema holds a bound as a <see cref="decimal"/>, an
/// example and the values of an enumeration as what Newtonsoft.Json writes, and writes the names of the values of an
/// enumeration itself, as <c>x-enumNames</c> and <c>x-enum-descriptions</c>.
/// </para>
/// <para>
/// Describing a schema a second time leaves it as the first time did.
/// </para>
/// </remarks>
internal static class ValueObjectJsonSchemaDescription
{
    /// <summary>
    /// Describes a value object as its underlying value, carrying the rules declared on it.
    /// </summary>
    /// <param name="schema">The schema to fill in: the value object's definition, or text held to its rules.</param>
    /// <param name="wire">How the value object is written.</param>
    /// <param name="nullable">Whether the schema allows <c>null</c>, which then joins the values of a closed set, though
    /// NJsonSchema says so itself: a definition never does, a reference to it saying so; nor does Swagger 2.0, which has no
    /// <c>null</c>.</param>
    /// <param name="settings">The settings the schema is generated with.</param>
    internal static void Describe(JsonSchema schema, ValueObjectWire wire, bool nullable, JsonSchemaGeneratorSettings settings)
    {
        var descriptor = wire.Descriptor;
        var declared = descriptor.Schema;
        var options = wire.Options;
        var valueType = descriptor.ValueType;

        var jsonType = wire.AsText ? ValueObjectSchemaKeywords.String : ValueObjectSchemaKeywords.TypeOf(valueType);
        var numeric = jsonType is ValueObjectSchemaKeywords.Integer or ValueObjectSchemaKeywords.Number;

        // A number the options let be written or read as text is a number or a string, held to the form a number is
        // written in: only in a JSON Schema, a document's options reading and writing numbers as numbers alone.
        var numberAsText = numeric
                           && (options.NumberHandling & (JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)) != 0;

        // Whatever NJsonSchema inferred about the wrapper type is wrong by construction: the value goes on the wire. A
        // null NJsonSchema allowed stays allowed, in its own terms: in the type of a JSON Schema, beside it in a document.
        schema.Properties.Clear();
        schema.RequiredProperties.Clear();
        schema.AllowAdditionalProperties = true;
        var allowsNull = schema.Type & JsonObjectType.Null;
        nullable = (nullable || allowsNull != 0 || schema.IsNullableRaw == true) && settings.SchemaType != SchemaType.Swagger2;

        schema.Type = MapType(jsonType) | (numberAsText ? JsonObjectType.String : JsonObjectType.None) | allowsNull;
        schema.Format = declared.Format;
        schema.Pattern = declared.Pattern ?? (wire.AsText
            ? ValueObjectSchemaKeywords.KeyPattern(valueType, declared)
            : ValueObjectSchemaKeywords.WirePattern(valueType, numberAsText));

        var (minLength, maxLength) = ValueObjectSchemaKeywords.Lengths(declared, valueType);
        schema.MinLength = minLength;
        schema.MaxLength = maxLength;

        // minimum and maximum bound a number, which NJsonSchema holds as a decimal: a real beyond its range or below its
        // precision, and a value written as text, carry their bounds otherwise.
        var beyond = false;
        if (numeric)
        {
            schema.Minimum = Bound(declared.Minimum, valueType, ref beyond);
            schema.Maximum = Bound(declared.Maximum, valueType, ref beyond);
        }

        string? bounds = null;
        var writtenAsText = !numeric || (options.NumberHandling & JsonNumberHandling.WriteAsString) != 0;
        if ((writtenAsText || beyond) && (declared.Minimum is not null || declared.Maximum is not null))
        {
            bounds = DescribeBounds(schema, declared, descriptor, options, wire.AsKey);
        }

        if (!string.IsNullOrEmpty(declared.Description))
        {
            schema.Description = declared.Description;
        }

        // Stated once, though the schema be described again.
        if (bounds is not null && schema.Description?.EndsWith(bounds, StringComparison.Ordinal) != true)
        {
            schema.Description = string.IsNullOrEmpty(schema.Description) ? bounds : $"{schema.Description}\n\n{bounds}";
        }

        if (declared.Example is { } example && example is not "")
        {
            schema.Example = ToToken(ValueObjectSchemaKeywords.WriteExample(example, descriptor, options, wire.AsKey), valueType);
        }

        if (declared.IsClosedValueSet && !declared.KnownValues.IsDefaultOrEmpty)
        {
            DescribeKnownValues(schema, declared, descriptor, wire, nullable, settings);
        }

        if (!wire.AsText
            && (options.NumberHandling & JsonNumberHandling.AllowNamedFloatingPointLiterals) != 0
            && (valueType == typeof(double) || valueType == typeof(float))
            && ValueObjectSchemaKeywords.NamedLiterals(declared) is { Count: > 0 } literals)
        {
            AllowNamedLiterals(schema, literals);
        }
    }

    /// <summary>
    /// Copies the rules a declaration put on a schema, a validation attribute of a member, which describing it as a
    /// value object replaces.
    /// </summary>
    /// <param name="schema">The schema, as NJsonSchema described it.</param>
    /// <returns>Its lengths and pattern.</returns>
    internal static (int? MinLength, int? MaxLength, string? Pattern) RulesOf(JsonSchema schema)
        => (schema.MinLength, schema.MaxLength, schema.Pattern);

    /// <summary>
    /// Keeps a rule the declaration of a member or a parameter put on its schema that is stricter than the value
    /// object's, or that the value object leaves out: the text must satisfy both.
    /// </summary>
    /// <param name="schema">The schema, described as the value object's text.</param>
    /// <param name="declared">What the declaration had put on it, as <see cref="RulesOf"/> copied it.</param>
    /// <remarks>A pattern is kept only when the value object neither declares nor implies one, a schema holding one.</remarks>
    internal static void KeepStricterRules(JsonSchema schema, (int? MinLength, int? MaxLength, string? Pattern) declared)
    {
        if (declared.MinLength is { } minLength && !(schema.MinLength >= minLength))
        {
            schema.MinLength = minLength;
        }

        if (declared.MaxLength is { } maxLength && !(schema.MaxLength <= maxLength))
        {
            schema.MaxLength = maxLength;
        }

        schema.Pattern ??= declared.Pattern;
    }

    /// <summary>
    /// Turns a value written by System.Text.Json into the token NJsonSchema writes, keeping a string that looks like a
    /// date a string, and every digit of a decimal.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="valueType">The underlying type of the value object it is a value of.</param>
    /// <returns>The token.</returns>
    /// <remarks>
    /// Only a decimal is read as one: any other real is read as the double it was written from, which a decimal would
    /// refuse beyond its range, as <c>1E+300</c>, or round below its precision, <c>9.1E-31</c> to <c>0</c>.
    /// </remarks>
    internal static JToken ToToken(JsonNode node, Type valueType)
        => Read(node.ToJsonString(), valueType == typeof(decimal) ? FloatParseHandling.Decimal : FloatParseHandling.Double);

    /// <summary>
    /// Turns a value of a closed set into the token a JSON Schema lists it as: read as NJsonSchema's validator reads the
    /// payload, a real as a double, since it compares the two by their text; in a document, which no such validator
    /// reads, as <see cref="ToToken"/> reads it.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="valueType">The underlying type of the value object it is a value of.</param>
    /// <param name="schemaType">The kind of schema.</param>
    /// <returns>The token.</returns>
    /// <remarks>
    /// Read as a decimal, the <c>20.0</c> a closed set of decimals holds would be compared, as <c>20.0</c>, with the
    /// <c>20</c> the double read from the same payload is, and refused: Confluent's serializer validates every message
    /// against the schema it generates, before it registers it. A JSON Schema therefore lists a decimal no double holds
    /// rounded, which a document, read by people and client generators, does not.
    /// </remarks>
    internal static JToken ToEnumerationToken(JsonNode node, Type valueType, SchemaType schemaType)
        => schemaType == SchemaType.JsonSchema ? Read(node.ToJsonString(), FloatParseHandling.Double) : ToToken(node, valueType);

    /// <summary>Reads JSON, keeping a string that looks like a date a string.</summary>
    private static JToken Read(string json, FloatParseHandling floats)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = floats };

        return JToken.ReadFrom(reader);
    }

    /// <summary>
    /// Reads a bound on a number as the decimal NJsonSchema holds it in.
    /// </summary>
    /// <param name="bound">The bound as declared, if any.</param>
    /// <param name="valueType">The underlying type.</param>
    /// <param name="beyond">Set when a bound is a number no decimal holds.</param>
    /// <returns>The bound, or <see langword="null"/>.</returns>
    private static decimal? Bound(string? bound, Type valueType, ref bool beyond)
    {
        if (bound is null || ValueObjectSchemaKeywords.FormatBound(bound, valueType) is not { } text)
        {
            return null;
        }

        // A decimal rounds a real below its precision to another number, 9.1E-31 to 0, which would loosen the bound.
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && Holds(number, text, valueType))
        {
            return number;
        }

        beyond = true;
        return null;

        static bool Holds(decimal number, string text, Type valueType)
            => (valueType != typeof(double) && valueType != typeof(float))
               || (double)number == double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Publishes the bounds of a value object written as text, or beyond what a decimal holds, in <c>x-minimum</c> and
    /// <c>x-maximum</c>, in the form the type writes them, for tools that read extensions, and in a sentence of the
    /// description, for people.
    /// </summary>
    /// <returns>The sentence.</returns>
    private static string DescribeBounds(
        JsonSchema schema,
        ValueObjectSchema declared,
        ValueObjectDescriptor descriptor,
        JsonSerializerOptions options,
        bool asKey)
    {
        var (minimum, maximum) = ValueObjectSchemaKeywords.WriteBounds(declared, descriptor, options, asKey);

        schema.ExtensionData ??= new Dictionary<string, object?>();
        if (minimum is not null)
        {
            schema.ExtensionData["x-minimum"] = ToToken(minimum, descriptor.ValueType);
        }

        if (maximum is not null)
        {
            schema.ExtensionData["x-maximum"] = ToToken(maximum, descriptor.ValueType);
        }

        return ValueObjectSchemaKeywords.BoundsSentence(minimum, maximum);
    }

    /// <summary>
    /// Lists the values of a closed set in <c>enum</c>, each as the type writes it, <c>null</c> after them when the schema
    /// allows it, and names each value beside it.
    /// </summary>
    /// <remarks>
    /// The names go where each generator reads them: NJsonSchema's own <c>x-enumNames</c>, which NSwag reads, and
    /// <c>x-enum-descriptions</c>, in its array form, <c>x-enum-varnames</c> for openapi-generator and Scalar, and
    /// <c>x-ms-enum</c> for Kiota and AutoRest. Nothing is named when the details do not list the values one for one.
    /// </remarks>
    private static void DescribeKnownValues(
        JsonSchema schema,
        ValueObjectSchema declared,
        ValueObjectDescriptor descriptor,
        ValueObjectWire wire,
        bool nullable,
        JsonSchemaGeneratorSettings settings)
    {
        schema.Enumeration.Clear();
        schema.EnumerationNames.Clear();
        schema.EnumerationDescriptions.Clear();

        List<JToken> values =
        [
            .. declared.KnownValues.Select(value => ToEnumerationToken(
                ValueObjectSchemaKeywords.WriteKnownValue(value, descriptor, wire.Options, wire.AsKey),
                descriptor.ValueType,
                settings.SchemaType)),
        ];
        foreach (var value in values)
        {
            schema.Enumeration.Add(value);
        }

        if (nullable)
        {
            schema.Enumeration.Add(null);
        }

        if (!ValueObjectSchemaKeywords.DetailsListTheKnownValues(declared))
        {
            return;
        }

        var details = declared.KnownValueDetails;
        var described = false;
        var entries = new JArray();
        for (var index = 0; index < details.Length; index++)
        {
            var detail = details[index];
            schema.EnumerationNames.Add(detail.Name);
            schema.EnumerationDescriptions.Add(detail.Description);
            described |= !string.IsNullOrWhiteSpace(detail.Description);

            var entry = new JObject { ["value"] = values[index].DeepClone(), ["name"] = detail.Name };
            if (!string.IsNullOrWhiteSpace(detail.Description))
            {
                entry["description"] = detail.Description;
            }

            entries.Add(entry);
        }

        // As NJsonSchema describes an enumeration: no description at all is no x-enum-descriptions.
        if (!described)
        {
            schema.EnumerationDescriptions.Clear();
        }

        schema.ExtensionData ??= new Dictionary<string, object?>();
        schema.ExtensionData["x-enum-varnames"] = new JArray(details.Select(static detail => detail.Name));
        // The name a client generator gives the enumeration: the one NJsonSchema files the value object's definition
        // under, unless another type took it first.
        schema.ExtensionData["x-ms-enum"] = new JObject
        {
            ["name"] = settings.SchemaNameGenerator.Generate(descriptor.ValueObjectType),
            ["modelAsString"] = false,
            ["values"] = entries,
        };
    }

    /// <summary>
    /// Describes a real as System.Text.Json writes one under <c>AllowNamedFloatingPointLiterals</c>: the number, or one of
    /// the named literals; what describes the value object as a whole stays where it is.
    /// </summary>
    private static void AllowNamedLiterals(JsonSchema schema, List<JsonNode> literals)
    {
        var number = new JsonSchema
        {
            Type = schema.Type,
            Format = schema.Format,
            Pattern = schema.Pattern,
            Minimum = schema.Minimum,
            Maximum = schema.Maximum,
        };
        var named = new JsonSchema();
        foreach (var literal in literals)
        {
            named.Enumeration.Add(ToToken(literal, typeof(double)));
        }

        schema.Type = JsonObjectType.None;
        schema.Format = null;
        schema.Pattern = null;
        schema.Minimum = null;
        schema.Maximum = null;
        schema.AnyOf.Clear();
        schema.AnyOf.Add(number);
        schema.AnyOf.Add(named);
    }

    /// <summary>Gets the JSON type a value object travels as, its text's for a value written as text.</summary>
    /// <param name="wire">How the value object is written.</param>
    /// <returns>The type.</returns>
    internal static JsonObjectType TypeOf(ValueObjectWire wire)
        => wire.AsText ? JsonObjectType.String : MapType(ValueObjectSchemaKeywords.TypeOf(wire.Descriptor.ValueType));

    /// <summary>Gets the JSON type of NJsonSchema a value object is described as.</summary>
    private static JsonObjectType MapType(string jsonType) => jsonType switch
    {
        ValueObjectSchemaKeywords.Boolean => JsonObjectType.Boolean,
        ValueObjectSchemaKeywords.Integer => JsonObjectType.Integer,
        ValueObjectSchemaKeywords.Number => JsonObjectType.Number,
        _ => JsonObjectType.String,
    };
}
