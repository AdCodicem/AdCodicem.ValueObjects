using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.Testing.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using SystemTextJsonDefaults = System.Text.Json.JsonSerializerDefaults;
using SystemTextJsonOptions = System.Text.Json.JsonSerializerOptions;
using SystemTextJsonSerializer = System.Text.Json.JsonSerializer;

namespace AdCodicem.ValueObjects.UnitTests.SchemaGeneration;

/// <summary>
/// Describes one value object in every wire NJsonSchema's settings describe, and validates what each wire's serializer
/// writes of it against the schema, as Confluent's serializer validates a message before producing it.
/// </summary>
internal static class NJsonSchemaWires
{
    /// <summary>
    /// Checks a value object: the type of its definition on each wire, and the payload of its two instances, of every
    /// value at an edge of its rules and of values drawn from them, alone, optional, absent and in a list, valid on each.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">One instance.</param>
    /// <param name="large">Another.</param>
    public static void Check<TSelf, TValue>(TSelf small, TSelf large)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var sampler = new ValueObjectSampler(new Random(1));
        List<TSelf> values = [small, large];
        foreach (var edge in sampler.Boundaries<TSelf, TValue>())
        {
            values.Add(TSelf.Create(edge));
        }

        for (var draw = 0; draw < 20; draw++)
        {
            values.Add(sampler.Next<TSelf, TValue>());
        }

        var numbers = new JsonSerializerSettings().AddValueObjects();
        var web = new SystemTextJsonOptions(SystemTextJsonDefaults.Web);
        var plain = new SystemTextJsonOptions();
        var literals = new SystemTextJsonOptions { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
        var jsonType = JsonTypeOf(typeof(TValue));
        var numeric = jsonType is "integer" or "number";
        var wires = new List<(string Name, JsonSchemaGeneratorSettings Settings, Func<object, string> Write, string Type)>
        {
            ("System.Text.Json", SchemaJson.SystemTextJson(plain), value => SystemTextJsonSerializer.Serialize(value, plain), jsonType),
            ("System.Text.Json web", SchemaJson.SystemTextJson(web), value => SystemTextJsonSerializer.Serialize(value, web), numeric ? $"[\"{jsonType}\",\"string\"]" : jsonType),
            ("System.Text.Json named literals", SchemaJson.SystemTextJson(literals), value => SystemTextJsonSerializer.Serialize(value, literals), jsonType),
            ("Newtonsoft.Json with the converter", SchemaJson.NewtonsoftWithConverter(), value => JsonConvert.SerializeObject(value, numbers), jsonType),
        };

        // Without the converter, Newtonsoft.Json writes the type converter's text, which a formatting hook may write in a
        // form the type does not read back: the schema then holds it to the rules of the text it reads.
        var converter = TypeDescriptor.GetConverter(typeof(TSelf));
        var text = converter.ConvertToInvariantString(small)!;
        if (TSelf.TryParse(text, CultureInfo.InvariantCulture, out var parsed) && parsed.Equals(small))
        {
            wires.Add(("Newtonsoft.Json without the converter", SchemaJson.NewtonsoftText(), static value => JsonConvert.SerializeObject(value), "string"));
        }
        else
        {
            typeof(TSelf).Name.Should().BeOneOf(["Floor", "Celsius"], "only a formatting hook writes text its type does not read");
        }

        foreach (var (name, settings, write, type) in wires)
        {
            var schema = JsonSchemaGenerator.FromType<SchemaHolder<TSelf>>(settings);
            var json = SchemaJson.Parse(schema.ToJson());
            var member = (JObject)(json["properties"]!["Value"] ?? json["properties"]!["value"])!;
            var definition = json.SelectToken(member["$ref"]!.ToString().TrimStart('#').Replace('/', '.').TrimStart('.'))!;
            var described = definition["type"] ?? definition["anyOf"]![0]!["type"]!;
            SchemaJson.Compact(described).Trim('"').Should().Be(type, "{0} is described on {1}", typeof(TSelf).Name, name);

            var validated = 0;
            foreach (var value in values)
            {
                var payload = write(new SchemaHolder<TSelf> { Value = value, Optional = value, Values = [value, small] });
                if (HoldsAnIntegerBeyondInt64(payload))
                {
                    continue;
                }

                SchemaJson.Errors(schema, payload).Should().BeEmpty("{0} writes {1} on {2}", typeof(TSelf).Name, payload, name);
                validated++;
            }

            validated.Should().BeGreaterThanOrEqualTo(2, "both instances of {0} are checked on {1}", typeof(TSelf).Name, name);
            SchemaJson.Errors(schema, write(new SchemaHolder<TSelf> { Value = large })).Should().BeEmpty("{0} may be absent on {1}", typeof(TSelf).Name, name);
        }
    }

    /// <summary>
    /// Tells whether JSON holds an integer beyond <see cref="long"/>, on which NJsonSchema's validator throws whatever
    /// the schema says (pinned by <c>JsonSchemaGenerationTests</c>).
    /// </summary>
    private static bool HoldsAnIntegerBeyondInt64(string json)
        => ((JContainer)JToken.Parse(json)).DescendantsAndSelf().OfType<JValue>().Any(static token => token.Value is BigInteger);

    /// <summary>Gets the JSON type a value of an underlying type is written as.</summary>
    private static string JsonTypeOf(Type valueType) => Type.GetTypeCode(valueType) switch
    {
        TypeCode.Boolean => "boolean",
        TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => "integer",
        TypeCode.Decimal or TypeCode.Double or TypeCode.Single => "number",
        _ => "string",
    };
}
