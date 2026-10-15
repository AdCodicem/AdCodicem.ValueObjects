using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.Testing.Data;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// What a value object becomes as a HotChocolate scalar, beside what its own rules make of the same values, for the
/// samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class GraphQLParity
{
    /// <summary>
    /// Gets the patterns the wire forms of a duration, a time of day and a <see cref="DateTime"/> follow, which a scalar
    /// over one publishes when its type declares none, as the OpenAPI packages do.
    /// </summary>
    public static IReadOnlyDictionary<Type, string> WirePatterns { get; } = new Dictionary<Type, string>
    {
        [typeof(TimeSpan)] = @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$",
        [typeof(TimeOnly)] = @"^(?:[01]\d|2[0-3]):[0-5]\d(?::[0-5]\d(?:\.\d{1,7})?)?$",
        [typeof(DateTime)] =
            @"^\d{4}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])(?:T(?:[01]\d|2[0-3]):[0-5]\d(?::[0-5]\d(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})?)?$",
    };

    /// <summary>
    /// Checks that the value object is a <see cref="ValueObjectScalarType{TSelf, TValue}"/> named by the conventions,
    /// described by its schema, serialized by its underlying type and carrying its pattern; that the two instances, and
    /// every value at an edge of its rules, are read as a literal, as a variable and as the variable its JSON converter
    /// writes, alone and in a list, and written back as the underlying value; that every value its schema rules out is
    /// refused with the code its rules give it; that the refused text of a String scalar is refused with the code its
    /// <c>TryParse</c> gives; and that an optional argument takes <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="small">One instance.</param>
    /// <param name="large">Another.</param>
    /// <param name="refused">Text the type refuses.</param>
    /// <returns>The check.</returns>
    public static async Task CheckAsync<TSelf, TValue>(TSelf small, TSelf large, string refused)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<EchoQuery<TSelf>>().AddValueObjects());
        var name = typeof(TSelf).Name;
        var scalar = executor.Schema.Types.OfType<ValueObjectScalarType<TSelf, TValue>>().Should().ContainSingle().Subject;
        var kind = KindOf(typeof(TValue));
        scalar.Name.Should().Be(name);
        scalar.Description.Should().Be(TSelf.Schema.Description);
        scalar.SerializationType.Should().Be(kind);
        scalar.Pattern.Should().Be(
            kind == ScalarSerializationType.String ? TSelf.Schema.Pattern ?? WirePatterns.GetValueOrDefault(typeof(TValue)) : null);

        var sampler = new ValueObjectSampler(new Random(1));
        List<TValue> accepted = [small.Value, large.Value, .. sampler.Boundaries<TSelf, TValue>()];
        foreach (var value in accepted)
        {
            var literal = Literal(kind, Text(value));
            var json = JsonSerializer.Serialize(TSelf.Create(value));
            Echoed(await executor.ExecuteAsync($"{{ echo(value: {literal}) }}")).Should().Be(literal, "{0} reads {1}", name, literal);
            Echoed(await executor.ExecuteAsync($"query($v: {name}!) {{ echo(value: $v) }}", $$"""{"v":{{literal}}}"""))
                .Should().Be(literal, "{0} reads the variable {1}", name, literal);
            Echoed(await executor.ExecuteAsync($"query($v: {name}!) {{ echo(value: $v) }}", $$"""{"v":{{json}}}"""))
                .Should().Be(literal, "{0} reads the variable its converter writes, {1}", name, json);
        }

        var list = string.Join(", ", accepted.Select(value => Literal(kind, Text(value))));
        Echoed(await executor.ExecuteAsync($"{{ many(values: [{list}]) }}"), "many").Should().Be($"[{list.Replace(", ", ",", StringComparison.Ordinal)}]");
        Echoed(await executor.ExecuteAsync("{ optional(value: null) }"), "optional").Should().Be("null");

        foreach (var (value, rule) in sampler.RejectedValues<TSelf, TValue>())
        {
            TSelf.TryCreate(value, out _, out var validation).Should().BeFalse();
            var literal = Literal(kind, Text(value));
            var error = (await executor.ExecuteAsync($"query($v: {name}!) {{ echo(value: $v) }}", $$"""{"v":{{literal}}}""")).Error();
            error.Code().Should().Be(validation.ErrorCode, "{0} refuses {1}, which breaks {2}", name, literal, rule);
            error.Message().Should().Be(validation.ErrorMessage);
        }

        if (kind == ScalarSerializationType.String)
        {
            TSelf.TryParse(refused, CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse();
            var error = (await executor.ExecuteAsync($"{{ echo(value: {JsonSerializer.Serialize(refused)}) }}")).Error();
            error.Code().Should().Be(validation.ErrorCode, "{0} refuses '{1}'", name, refused);
            error.Message().Should().Be(validation.ErrorMessage);
            error.Member("inputPath").Should().Be("""["value"]""");
        }
    }

    /// <summary>Gets the kind a scalar over an underlying type is serialized to.</summary>
    internal static ScalarSerializationType KindOf(Type type)
        => type == typeof(bool) ? ScalarSerializationType.Boolean
            : type == typeof(sbyte) || type == typeof(byte) || type == typeof(short) || type == typeof(ushort)
              || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ? ScalarSerializationType.Int
            : type == typeof(float) || type == typeof(double) || type == typeof(decimal) ? ScalarSerializationType.Float
            : ScalarSerializationType.String;

    /// <summary>
    /// Gets the invariant text of an underlying value, in the form its parser reads and its default format writes: a date
    /// or a time in its round-trip form, a duration in its constant one.
    /// </summary>
    internal static string Text<TValue>(TValue value) => value switch
    {
        string text => text,
        bool boolean => boolean ? "true" : "false",
        TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
        DateOnly or TimeOnly or DateTime or DateTimeOffset => ((IFormattable)value).ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value!.ToString()!,
    };

    /// <summary>Gets the GraphQL literal of a value's text, which is its JSON too: a string quoted, anything else as it is.</summary>
    internal static string Literal(ScalarSerializationType kind, string text)
        => kind == ScalarSerializationType.String ? JsonSerializer.Serialize(text) : text;

    /// <summary>Gets a field of a result's data, as compact JSON a string of which is quoted as a literal is.</summary>
    private static string Echoed(JsonObject result, string field = "echo")
    {
        var node = JsonNode.Parse(result.Data())![field];
        return node switch
        {
            null => "null",
            JsonArray array => $"[{string.Join(",", array.Select(element => Quote(element!)))}]",
            _ => Quote(node),
        };
    }

    private static string Quote(JsonNode node)
        => node.GetValueKind() == JsonValueKind.String ? JsonSerializer.Serialize(node.GetValue<string>()) : node.ToJsonString();
}
