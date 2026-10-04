using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.Json;

/// <summary>
/// Describes a value object in a JSON Schema exported by System.Text.Json as its underlying value, carrying the rules
/// declared on it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JsonSchemaExporter"/> builds a schema from <see cref="JsonTypeInfo"/>, and a type serialized by a converter
/// of its own has none to read: it describes every value object as <c>true</c>, the schema that accepts anything, leaves
/// <c>items</c> out of a collection of them, and loses the <c>null</c> a nullable one accepts. Plugged into
/// <see cref="JsonSchemaExporterOptions.TransformSchemaNode"/>, this fills each of them in from the
/// <see cref="ValueObjectSchema"/> of the value object, the rules its <c>Validate</c> enforces:
/// </para>
/// <code>
/// var schema = JsonSchemaExporter.GetJsonSchemaAsNode(
///     AppJsonContext.Default.Options,
///     typeof(Order),
///     new JsonSchemaExporterOptions { TransformSchemaNode = ValueObjectJsonSchema.TransformSchemaNode });
/// </code>
/// <para>
/// The underlying type gives <c>type</c>, and <c>"null"</c> joins it, and the <c>enum</c>, for a nullable value object.
/// <c>MinLength</c> and <c>MaxLength</c> give <c>minLength</c> and <c>maxLength</c>, a pattern <c>pattern</c>, and a
/// bound <c>minimum</c> or <c>maximum</c> on a number; on a value written as a string, a date, a time or a character,
/// which those keywords cannot bound, a sentence of the description states it. A closed set lists its values in
/// <c>enum</c>, and the example goes to <c>examples</c>, each written by the value object's own converter. The
/// description and the format follow, the format as the profile says (<see cref="ValueObjectJsonSchemaProfile"/>).
/// </para>
/// <para>
/// A collection or a dictionary of value objects gets its <c>items</c> or <c>additionalProperties</c>, which the
/// exporter leaves out when the element's schema is <c>true</c>, and a dictionary keyed by a value object written as a
/// string gets the key's rules in <c>propertyNames</c>. A value object is described in place, never as a reference.
/// </para>
/// <para>
/// The schema a host hands over is completed rather than replaced: a description it already wrote comes first, the
/// value object's following it, and a keyword the value object does not declare, such as a <c>default</c>, stays.
/// Every host built on the exporter exposes the <see cref="JsonTypeInfo"/> <see cref="Apply"/> takes, whatever its own
/// context is called: Microsoft.Extensions.AI, the Model Context Protocol SDK, Semantic Kernel.
/// </para>
/// <para>
/// A value object is looked up in <see cref="ValueObjectRegistry"/>, without reflection. One nothing registered, a value
/// object written by hand or a construction of a generic one, is described by reflection where the runtime supports
/// dynamic code, and left as the exporter described it under native AOT, where it has to be registered through
/// <see cref="ValueObjectRegistry.Register{TSelf, TValue}(Func{JsonConverter{TSelf}})"/>, as serializing it through
/// <see cref="ValueObjectJsonConverterFactory"/> asks already.
/// </para>
/// </remarks>
public static class ValueObjectJsonSchema
{
    /// <summary>
    /// Formats JSON Schema defines, which a language model is expected to know.
    /// </summary>
    private static readonly HashSet<string> StandardFormats = new(StringComparer.Ordinal)
    {
        "date-time", "date", "time", "duration", "email", "idn-email", "hostname", "idn-hostname", "ipv4", "ipv6", "uri",
        "uri-reference", "iri", "iri-reference", "uuid", "uri-template", "json-pointer", "relative-json-pointer", "regex",
    };

    private static readonly Func<JsonSchemaExporterContext, JsonNode, JsonNode> OpenApiTransform
        = static (context, schema) => Apply(context.TypeInfo, schema, ValueObjectJsonSchemaProfile.OpenApi);

    private static readonly Func<JsonSchemaExporterContext, JsonNode, JsonNode> LanguageModelTransform
        = static (context, schema) => Apply(context.TypeInfo, schema, ValueObjectJsonSchemaProfile.LanguageModel);

    /// <summary>
    /// Describes the value objects of a schema the exporter builds, under the
    /// <see cref="ValueObjectJsonSchemaProfile.OpenApi"/> profile: the delegate
    /// <see cref="JsonSchemaExporterOptions.TransformSchemaNode"/> takes.
    /// </summary>
    /// <param name="context">The context of the node, which names its type.</param>
    /// <param name="schema">The schema the exporter built for the node.</param>
    /// <returns>The schema, completed when it describes a value object or a container of value objects.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is <see langword="null"/>.</exception>
    public static JsonNode TransformSchemaNode(JsonSchemaExporterContext context, JsonNode schema)
        => Apply(context.TypeInfo, schema);

    /// <summary>
    /// Creates the transform <see cref="JsonSchemaExporterOptions.TransformSchemaNode"/> takes for a profile.
    /// </summary>
    /// <param name="profile">Whom the schema describes a value object for.</param>
    /// <returns>The transform, the same instance for a given profile.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is not a defined profile.</exception>
    /// <remarks>
    /// <code>
    /// var options = new JsonSchemaExporterOptions
    /// {
    ///     TransformSchemaNode = ValueObjectJsonSchema.CreateTransform(ValueObjectJsonSchemaProfile.LanguageModel),
    /// };
    /// </code>
    /// </remarks>
    public static Func<JsonSchemaExporterContext, JsonNode, JsonNode> CreateTransform(ValueObjectJsonSchemaProfile profile)
        => profile switch
        {
            ValueObjectJsonSchemaProfile.OpenApi => OpenApiTransform,
            ValueObjectJsonSchemaProfile.LanguageModel => LanguageModelTransform,
            _ => throw UndefinedProfile(profile),
        };

    /// <summary>
    /// Describes the value object a schema node stands for, or the value objects a collection or a dictionary holds.
    /// </summary>
    /// <param name="typeInfo">The contract of the node's type, under the options the schema describes the wire with.</param>
    /// <param name="schema">The schema built so far for the node, <c>true</c> for a value object.</param>
    /// <param name="profile">Whom the schema describes a value object for.</param>
    /// <returns>
    /// The schema: a new object in place of <c>true</c>, the same object completed otherwise, or the schema unchanged when
    /// the node is neither a value object nor a container of them, or the schema <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeInfo"/> or <paramref name="schema"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> is not a defined profile.</exception>
    /// <remarks>
    /// <para>
    /// This is the piece every host shares: each one that builds on <see cref="JsonSchemaExporter"/> hands a transform of
    /// its own the <see cref="JsonTypeInfo"/> of the node, whatever its context is called. With Microsoft.Extensions.AI:
    /// </para>
    /// <code>
    /// var options = new AIJsonSchemaCreateOptions
    /// {
    ///     TransformSchemaNode = (context, schema) => ValueObjectJsonSchema.Apply(
    ///         context.TypeInfo, schema, ValueObjectJsonSchemaProfile.LanguageModel),
    /// };
    /// </code>
    /// <para>
    /// The node of a value object is <see cref="JsonTypeInfoKind.None"/>, its converter being its own. A value object
    /// System.Text.Json serializes as an object with properties, one written by hand without a converter, has a contract
    /// of another kind and is left as it is.
    /// </para>
    /// </remarks>
    public static JsonNode Apply(
        JsonTypeInfo typeInfo,
        JsonNode schema,
        ValueObjectJsonSchemaProfile profile = ValueObjectJsonSchemaProfile.OpenApi)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        ArgumentNullException.ThrowIfNull(schema);
        if (profile is not (ValueObjectJsonSchemaProfile.OpenApi or ValueObjectJsonSchemaProfile.LanguageModel))
        {
            throw UndefinedProfile(profile);
        }

        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.None when ConvertedAsValue(typeInfo) && TryDescribe(typeInfo.Type, out var descriptor):
                return Describe(schema, descriptor, IsNullable(typeInfo.Type), typeInfo.Options, profile);
            case JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary when schema is JsonObject container:
                DescribeContainer(container, typeInfo, profile);
                return schema;
            default:
                return schema;
        }
    }

    private static ArgumentOutOfRangeException UndefinedProfile(ValueObjectJsonSchemaProfile profile)
        => new(nameof(profile), profile, "The profile is not one ValueObjectJsonSchemaProfile defines.");

    private static bool IsNullable(Type type) => Nullable.GetUnderlyingType(type) is not null;

    /// <summary>
    /// Tells whether the converter of a type writes it as a value: always for a value object, whose contract is of no
    /// kind; for a nullable one, only when the value object's own contract is of no kind either.
    /// </summary>
    /// <param name="typeInfo">The contract of the node's type, of no kind.</param>
    /// <returns><see langword="false"/> for a nullable struct the serializer writes as an object.</returns>
    private static bool ConvertedAsValue(JsonTypeInfo typeInfo)
        => Nullable.GetUnderlyingType(typeInfo.Type) is not { } underlying
           || !typeInfo.Options.TryGetTypeInfo(underlying, out var contract)
           || contract.Kind == JsonTypeInfoKind.None;

    /// <summary>
    /// Finds the descriptor of a value object, registered, or described by reflection where the runtime supports
    /// dynamic code.
    /// </summary>
    /// <param name="type">The type, possibly nullable.</param>
    /// <param name="descriptor">The descriptor of the value object.</param>
    /// <returns><see langword="true"/> when the type is a value object the schema can describe.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Only reached for a value object nothing registered: one written by hand, or a construction of a "
                        + "generic one, which trimming has to keep as its serialization through "
                        + "ValueObjectJsonConverterFactory asks already. Under native AOT, where dynamic code is not "
                        + "supported, it is left as the exporter described it.")]
    private static bool TryDescribe(Type type, [NotNullWhen(true)] out ValueObjectDescriptor? descriptor)
    {
        if (ValueObjectRegistry.TryGet(type, out descriptor))
        {
            return true;
        }

        if (!RuntimeFeature.IsDynamicCodeSupported || !ValueObjectRegistry.IsValueObject(type))
        {
            return false;
        }

        return ValueObjectRegistry.TryResolve(type, out descriptor);
    }

    /// <summary>
    /// Describes the elements of a collection, and the values and keys of a dictionary, that are value objects.
    /// </summary>
    /// <param name="container">The schema of the collection or the dictionary.</param>
    /// <param name="typeInfo">Its contract.</param>
    /// <param name="profile">Whom the schema describes a value object for.</param>
    /// <remarks>
    /// The exporter leaves <c>items</c> and <c>additionalProperties</c> out when the element's schema is <c>true</c>, and
    /// never calls the transform for that element: the value object is described here, from the container's contract,
    /// completing what a host may have written there instead, such as the <c>{}</c> Microsoft.Extensions.AI writes. A
    /// key is written as text, so only a value object written as a string describes it, in <c>propertyNames</c>: the text
    /// of a property name is never a number or a boolean.
    /// </remarks>
    private static void DescribeContainer(JsonObject container, JsonTypeInfo typeInfo, ValueObjectJsonSchemaProfile profile)
    {
        var options = typeInfo.Options;
        if (typeInfo.ElementType is { } elementType && TryDescribe(elementType, out var element))
        {
            var keyword = typeInfo.Kind == JsonTypeInfoKind.Dictionary ? "additionalProperties" : "items";
            Complete(container, keyword, element, IsNullable(elementType), options, profile);
        }

        if (typeInfo.KeyType is { } keyType
            && TryDescribe(keyType, out var key)
            && ValueObjectSchemaKeywords.TypeOf(key.ValueType) == ValueObjectSchemaKeywords.String)
        {
            Complete(container, "propertyNames", key, nullable: false, options, profile);
        }
    }

    /// <summary>
    /// Describes the value object a keyword of a container stands for, in the schema already there or in a new one.
    /// </summary>
    private static void Complete(
        JsonObject container,
        string keyword,
        ValueObjectDescriptor descriptor,
        bool nullable,
        JsonSerializerOptions options,
        ValueObjectJsonSchemaProfile profile)
    {
        var existing = container[keyword];
        var described = Describe(existing ?? new JsonObject(), descriptor, nullable, options, profile);
        if (!ReferenceEquals(described, existing))
        {
            container[keyword] = described;
        }
    }

    /// <summary>
    /// Describes a value object as its underlying value, carrying the rules declared on it.
    /// </summary>
    /// <param name="schema">The schema built so far, <c>true</c> or an object.</param>
    /// <param name="descriptor">Descriptor of the value object.</param>
    /// <param name="nullable">Whether the node is a nullable value object.</param>
    /// <param name="options">The options the schema describes the wire with.</param>
    /// <param name="profile">Whom the schema describes a value object for.</param>
    /// <returns>The schema completed.</returns>
    private static JsonNode Describe(
        JsonNode schema,
        ValueObjectDescriptor descriptor,
        bool nullable,
        JsonSerializerOptions options,
        ValueObjectJsonSchemaProfile profile)
    {
        JsonObject node;
        if (schema is JsonObject given)
        {
            node = given;
        }
        else if (schema.GetValueKind() == JsonValueKind.True)
        {
            node = [];
        }
        else
        {
            // The schema false accepts nothing, and nothing the value object declares widens it.
            return schema;
        }

        var declared = descriptor.Schema;
        var openApi = profile == ValueObjectJsonSchemaProfile.OpenApi;
        var jsonType = ValueObjectSchemaKeywords.TypeOf(descriptor.ValueType);
        var numeric = jsonType is ValueObjectSchemaKeywords.Integer or ValueObjectSchemaKeywords.Number;
        var handling = options.NumberHandling;

        // Under the OpenAPI profile, a number the options let be written or read as text is a number or a string, as
        // System.Text.Json documents the bare number under the same options, the string held to the form a number is
        // written in. A language model is asked for the number alone, which the serializer always reads.
        var asText = openApi && numeric
                     && (handling & (JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)) != 0;

        // minimum and maximum bound a number only: a value written as a string has its bounds stated in a sentence too.
        // One that may only be read from a string is still written as a number, which they bound.
        var writtenAsText = !numeric || (openApi && (handling & JsonNumberHandling.WriteAsString) != 0);

        var format = declared.Format;
        var description = new List<string>(4);
        if (!string.IsNullOrEmpty(declared.Description))
        {
            description.Add(declared.Description);
        }

        if (writtenAsText && (declared.Minimum is not null || declared.Maximum is not null))
        {
            var (minimum, maximum) = ValueObjectSchemaKeywords.WriteBounds(declared, descriptor, options);
            description.Add(ValueObjectSchemaKeywords.BoundsSentence(minimum, maximum));
        }

        if (!openApi && format is not null && !StandardFormats.Contains(format))
        {
            description.Add($"Format: {format}.");
            format = null;
        }

        JsonArray? values = null;
        if (declared.IsClosedValueSet && !declared.KnownValues.IsDefaultOrEmpty)
        {
            values =
            [
                .. declared.KnownValues.Select(value
                    => Written(ValueObjectSchemaKeywords.WriteKnownValue(value, descriptor, options), numeric, openApi)),
            ];
            if (!openApi && ValueObjectSchemaKeywords.DetailsListTheKnownValues(declared))
            {
                description.Add(NameValues(declared, values));
            }
        }

        // Whatever was inferred about the wrapper type is wrong by construction: it is the underlying value that goes on
        // the wire. A null the host already allows stays allowed.
        node.Remove("properties");
        node.Remove("required");
        nullable |= AllowsNull(node);

        foreach (var piece in description)
        {
            AppendDescription(node, piece);
        }

        node["type"] = TypeOf(jsonType, asText, nullable);
        Set(node, "format", format);
        Set(node, "pattern", declared.Pattern ?? ValueObjectSchemaKeywords.WirePattern(descriptor.ValueType, asText));

        if (declared.MinLength is { } minLength)
        {
            node["minLength"] = minLength;
        }

        if (declared.MaxLength is { } maxLength)
        {
            node["maxLength"] = maxLength;
        }

        if (numeric)
        {
            SetBound(node, "minimum", declared.Minimum, descriptor.ValueType);
            SetBound(node, "maximum", declared.Maximum, descriptor.ValueType);
        }

        if (values is not null)
        {
            if (nullable)
            {
                values.Add(null);
            }

            node["enum"] = values;
        }

        if (!string.IsNullOrEmpty(declared.Example))
        {
            var example = ValueObjectSchemaKeywords.WriteText(declared.Example, descriptor, options);
            node["examples"] = new JsonArray(Written(example, numeric, openApi));
        }

        if (openApi
            && (handling & JsonNumberHandling.AllowNamedFloatingPointLiterals) != 0
            && (descriptor.ValueType == typeof(double) || descriptor.ValueType == typeof(float))
            && ValueObjectSchemaKeywords.NamedLiterals(declared) is { Count: > 0 } literals)
        {
            AllowNamedLiterals(node, literals);
        }

        return node;
    }

    /// <summary>
    /// Builds the <c>type</c> of a value object: its JSON type, preceded by <c>string</c> for a number that may travel as
    /// text, and followed by <c>null</c> for a nullable one, in the order System.Text.Json writes them.
    /// </summary>
    private static JsonNode TypeOf(string jsonType, bool asText, bool nullable)
    {
        if (!asText && !nullable)
        {
            return jsonType;
        }

        var types = new List<JsonNode?>(3);
        if (asText)
        {
            types.Add(JsonValue.Create(ValueObjectSchemaKeywords.String));
        }

        types.Add(JsonValue.Create(jsonType));
        if (nullable)
        {
            types.Add(JsonValue.Create("null"));
        }

        return new JsonArray([.. types]);
    }

    /// <summary>
    /// Sets a keyword the value object decides alone, or removes the one a host wrote when the value object has none.
    /// </summary>
    private static void Set(JsonObject node, string keyword, string? value)
    {
        if (value is null)
        {
            node.Remove(keyword);
        }
        else
        {
            node[keyword] = value;
        }
    }

    /// <summary>
    /// Sets <c>minimum</c> or <c>maximum</c> to a declared bound, written as the number the type enforces.
    /// </summary>
    private static void SetBound(JsonObject node, string keyword, string? bound, Type valueType)
    {
        if (bound is not null && ValueObjectSchemaKeywords.FormatBound(bound, valueType) is { } number)
        {
            node[keyword] = JsonNode.Parse(number);
        }
    }

    /// <summary>
    /// Keeps a value the type wrote, or, for a language model, writes a number the options wrote as text as the number it
    /// is, which the serializer reads as well.
    /// </summary>
    private static JsonNode Written(JsonNode value, bool numeric, bool openApi)
    {
        if (openApi || !numeric || value is not JsonValue text || !text.TryGetValue<string>(out var written))
        {
            return value;
        }

        try
        {
            // The text of a number the options wrote as a string is that number's JSON.
            return JsonNode.Parse(written)!;
        }
        catch (JsonException)
        {
            // A named literal, which no JSON number writes.
            return value;
        }
    }

    /// <summary>
    /// Names each value of a closed set, with the description it was declared with, one line per value.
    /// </summary>
    /// <param name="declared">The declared rules, whose details list the known values one for one.</param>
    /// <param name="values">The values, as the type writes them.</param>
    /// <returns>The lines.</returns>
    private static string NameValues(ValueObjectSchema declared, JsonArray values)
        => string.Join(
            "\n",
            declared.KnownValueDetails.Select((detail, index) =>
            {
                var line = $"{ValueObjectSchemaKeywords.Quote(values[index]!)}: {detail.Name}";
                return string.IsNullOrWhiteSpace(detail.Description) || detail.Description == detail.Name
                    ? line
                    : $"{line} — {detail.Description}";
            }));

    /// <summary>
    /// Appends a paragraph to the description, after the one a host already wrote, unless it holds it already.
    /// </summary>
    private static void AppendDescription(JsonObject node, string piece)
    {
        var current = node["description"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        if (string.IsNullOrEmpty(current))
        {
            node["description"] = piece;
        }
        else if (!$"\n\n{current}\n\n".Contains($"\n\n{piece}\n\n", StringComparison.Ordinal))
        {
            node["description"] = $"{current}\n\n{piece}";
        }
    }

    /// <summary>
    /// Tells whether the type a host wrote allows <c>null</c>.
    /// </summary>
    private static bool AllowsNull(JsonObject node)
        => node["type"] is JsonArray types ? types.Any(IsNull) : IsNull(node["type"]);

    private static bool IsNull(JsonNode? type) => type is JsonValue value && value.TryGetValue<string>(out var name) && name == "null";

    /// <summary>
    /// Describes a real as System.Text.Json describes one under <c>AllowNamedFloatingPointLiterals</c>: the number, or
    /// one of the named literals.
    /// </summary>
    /// <param name="node">The schema, describing the number.</param>
    /// <param name="literals">The named literals the value object lets through.</param>
    /// <remarks>
    /// What describes the number moves to the first alternative, <c>null</c> included; what describes the value object as
    /// a whole, its description, its example and its known values, stays where it is.
    /// </remarks>
    private static void AllowNamedLiterals(JsonObject node, List<JsonNode> literals)
    {
        var number = new JsonObject();
        foreach (var keyword in (ReadOnlySpan<string>)["type", "format", "pattern", "minimum", "maximum"])
        {
            if (node.Remove(keyword, out var value))
            {
                number[keyword] = value;
            }
        }

        node["anyOf"] = new JsonArray(number, new JsonObject { ["enum"] = new JsonArray([.. literals]) });
    }
}
