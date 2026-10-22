using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// What NSwag's OpenAPI 3.0 document says of each value object, and of each value object's text, against what the
/// built-in stack's OpenAPI 3.1 document says for the same application, through the shared keyword table: the same
/// rules, written each in its version's form.
/// </summary>
/// <param name="fixture">The documents.</param>
public sealed class NSwagParityTests(NSwagDocument fixture) : IClassFixture<NSwagDocument>
{
    public static TheoryData<string> EveryValueObjectMember => [.. Members(typeof(EveryValueObject)).Where(static member => member != "mass")];

    public static TheoryData<string> EveryTextMember => [.. Members(typeof(EveryValueObjectAsText))];

    private NSwagDocuments Documents => fixture.Parity;

    [Theory]
    [MemberData(nameof(EveryValueObjectMember))]
    public void Each_value_object_is_described_as_the_built_in_stack_describes_it(string member)
        => Compare(nameof(EveryValueObject), member);

    [Theory]
    [MemberData(nameof(EveryTextMember))]
    public void Each_value_object_s_text_is_described_as_the_built_in_stack_describes_it(string member)
        => Compare(nameof(EveryValueObjectAsText), member);

    /// <summary>
    /// A bound a decimal does not hold, which NJsonSchema keeps its <c>minimum</c> and <c>maximum</c> in, is published as
    /// an extension and in the description: rounded, 9.1E-31 would read as a minimum of 0.
    /// </summary>
    [Fact]
    public void A_bound_no_decimal_holds_is_an_extension_where_the_built_in_stack_writes_a_number()
    {
        var nswag = (JObject)Documents.Schemas["Mass"]!;
        var builtIn = (JObject)Documents.BuiltIn!["components"]!["schemas"]!["Mass"]!;

        nswag["x-minimum"]!.ToString(Formatting.None).Should().Be("9.1E-31");
        nswag["x-maximum"]!.ToString(Formatting.None).Should().Be("2E+32");
        nswag.ContainsKey("minimum").Should().BeFalse();
        nswag["description"]!.ToString().Should().EndWith("\n\nBetween 9.1E-31 and 2E+32, inclusive.");
        builtIn["minimum"]!.Value<double>().Should().Be(9.1e-31);
        builtIn["maximum"]!.Value<double>().Should().Be(2e32);
    }

    private static IEnumerable<string> Members(Type body)
        => body.GetConstructors().Single().GetParameters().Select(static parameter => JsonNamingPolicyCamelCase(parameter.Name!));

    private static string JsonNamingPolicyCamelCase(string name) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name);

    private void Compare(string component, string member)
    {
        var builtInSchemas = Documents.BuiltIn!["components"]!["schemas"]!;
        var nswag = Normalize(Documents.Document, Documents.Property(component, member));
        var builtIn = Normalize(Documents.BuiltIn!, (JObject)builtInSchemas[component]!["properties"]![member]!);

        JToken.DeepEquals(nswag, builtIn).Should().BeTrue(
            "{0}.{1} is described alike, NSwag's {2} and the built-in stack's {3}",
            component,
            member,
            nswag.ToString(Formatting.None),
            builtIn.ToString(Formatting.None));
    }

    /// <summary>
    /// Writes a schema in a form both versions share: a reference followed, a reference beside <c>null</c> followed,
    /// <c>null</c> left out of <c>type</c> and <c>enum</c>, <c>nullable</c> dropped, the first of 3.1's <c>examples</c> as
    /// <c>example</c>, every number written as the decimal it is, and what only one of them writes left out:
    /// NJsonSchema's own <c>x-enum-descriptions</c>, the name <c>x-ms-enum</c> gives the enumeration of a set, and the
    /// <c>propertyNames</c> of a dictionary, which OpenAPI 3.0 lacks.
    /// </summary>
    private static JObject Normalize(JObject document, JObject schema)
    {
        var schemas = document["components"]!["schemas"]!;
        while (true)
        {
            if (schema["$ref"] is { } reference)
            {
                schema = (JObject)schemas[reference.ToString().Split('/')[^1]]!;
                continue;
            }

            if ((schema["oneOf"] ?? schema["anyOf"] ?? schema["allOf"]) is JArray alternatives
                && alternatives.Where(static alternative => alternative["$ref"] is not null).ToList() is [JObject referred]
                && alternatives.All(alternative => alternative == referred || JToken.DeepEquals(alternative, new JObject { ["type"] = "null" })))
            {
                schema = referred;
                continue;
            }

            break;
        }

        var copy = (JObject)schema.DeepClone();
        if (copy["type"] is JArray types)
        {
            var kept = types.Where(static type => type.ToString() != "null").ToList();
            copy["type"] = kept.Count == 1 ? kept[0] : new JArray(kept);
        }

        if (copy["examples"] is JArray { Count: > 0 } examples)
        {
            copy["example"] = examples[0];
            copy.Remove("examples");
        }

        copy.Remove("x-enum-descriptions");
        copy.Remove("nullable");
        copy.Remove("propertyNames");
        (copy["x-ms-enum"] as JObject)?.Remove("name");
        if (copy["enum"] is JArray values)
        {
            copy["enum"] = new JArray(values.Where(static value => value.Type != JTokenType.Null));
        }

        foreach (var nested in new[] { "items", "additionalProperties" })
        {
            if (copy[nested] is JObject inner)
            {
                copy[nested] = Normalize(document, inner);
            }
        }

        return (JObject)Canonical(copy);
    }

    /// <summary>Writes a number as the decimal it is, or as it is written when no decimal holds it.</summary>
    private static string Decimal(string number)
        => decimal.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact) ? exact.ToString("G29", CultureInfo.InvariantCulture) : number;

    /// <summary>Writes every number as the decimal it is, and orders the keys of every object.</summary>
    private static JToken Canonical(JToken token) => token switch
    {
        JObject value => new JObject(value.Properties().OrderBy(static property => property.Name, StringComparer.Ordinal).Select(static property => new JProperty(property.Name, Canonical(property.Value)))),
        JArray value => new JArray(value.Select(Canonical)),
        JValue { Type: JTokenType.Integer or JTokenType.Float } value => new JValue(Decimal(Convert.ToString(value.Value, CultureInfo.InvariantCulture)!)),
        _ => token.DeepClone(),
    };
}
