using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// System.Text.Json leaves the number handling of a custom converter to the converter. The generated one honours it as
/// the built-in converter of the underlying type does, so that a value object crosses the boundary exactly as its
/// underlying type would.
/// </summary>
public partial class JsonNumberHandlingTests
{
    private const JsonNumberHandling Named = JsonNumberHandling.AllowNamedFloatingPointLiterals;
    private const JsonNumberHandling FromString = JsonNumberHandling.AllowReadingFromString;
    private const JsonNumberHandling AsString = JsonNumberHandling.WriteAsString;

    public static TheoryData<JsonNumberHandling> EveryHandling =>
    [
        JsonNumberHandling.Strict,
        Named,
        FromString,
        AsString,
        Named | FromString,
        Named | AsString,
        FromString | AsString,
        Named | FromString | AsString,
    ];

    /// <summary>
    /// Written under any number handling, a value object is the JSON its underlying value is, NaN and the infinities
    /// of a type that accepts them included, and what the bare value cannot be written as, it cannot either.
    /// </summary>
    /// <param name="handling">The number handling.</param>
    [Theory]
    [MemberData(nameof(EveryHandling))]
    public void A_number_is_written_as_its_underlying_value_is(JsonNumberHandling handling)
    {
        var options = new JsonSerializerOptions { NumberHandling = handling };

        foreach (var value in new[] { 0d, -1.5d, 48.8566d, 1e300, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            SameWriting(Signal.Create(value), value, options);
        }

        foreach (var value in new[] { 0f, 0.1f, float.NaN, float.NegativeInfinity })
        {
            SameWriting(Level.Create(value), value, options);
        }

        SameWriting(Amount.Create(1250m), 1250.00m, options);
        SameWriting(Port.Create(8080), (ushort)8080, options);
        SameWriting(Adjustment.Create(-1), (sbyte)-1, options);
        SameWriting(FileSize.Create(5_000_000_000L), 5_000_000_000L, options);
    }

    /// <summary>
    /// An indented writer lays a value object out as it lays out its underlying value, a number written as text
    /// included, in an array as in an object.
    /// </summary>
    /// <param name="handling">The number handling.</param>
    [Theory]
    [MemberData(nameof(EveryHandling))]
    public void A_number_is_laid_out_as_its_underlying_value_is(JsonNumberHandling handling)
    {
        var options = new JsonSerializerOptions { NumberHandling = handling, WriteIndented = true };
        double[] reals = [1.5, 1e20, 2];
        decimal[] amounts = [1.5m, 2m];

        SameWriting(reals.Select(Signal.Create).ToArray(), reals, options);
        SameWriting(new { Readings = reals.Select(Signal.Create).ToArray() }, new { Readings = reals }, options);
        SameWriting(amounts.Select(Measure.Create).ToArray(), amounts, options);
        SameWriting(new[] { Tally.Create(1), Tally.Create(2) }, new[] { 1, 2 }, options);

        if ((handling & Named) != 0)
        {
            SameWriting(new[] { Signal.Create(1), Signal.Create(double.NaN) }, new[] { 1, double.NaN }, options);
        }
    }

    /// <summary>
    /// Read under any number handling, JSON gives a value object exactly what it gives the underlying type, and is
    /// refused where the underlying type refuses it.
    /// </summary>
    /// <param name="handling">The number handling.</param>
    [Theory]
    [MemberData(nameof(EveryHandling))]
    public void A_number_is_read_as_its_underlying_value_is(JsonNumberHandling handling)
    {
        var options = new JsonSerializerOptions { NumberHandling = handling };

        string[] reals =
        [
            "1.5", "\"1.5\"", "\"NaN\"", "\"Infinity\"", "\"-Infinity\"", "\"nan\"", "\"Infinity \"", "\"1e999\"", "\"abc\"",
            "\" 1.5\"", "\"1.5 \"", "\"+1.5\"", "\"1,000\"", "\"1,5\"", "\"1.5e3\"", "\"1.5e+3\"", "\"\\u0031.5\"", "\"N\\u0061N\"", "\"\"",
        ];
        foreach (var json in reals)
        {
            SameReading<Signal, double>(json, options);
            SameReading<Level, float>(json, options);
        }

        string[] integers =
        [
            "5", "\"5\"", "\" 5\"", "\"5 \"", "\"+5\"", "\"-5\"", "\"5-\"", "\"1,000\"", "\"1e3\"", "\"1.5\"", "\"0x10\"",
            "\"\\u0035\"", "\"00012\"", "\"\"", "\"NaN\"",
        ];
        foreach (var json in integers)
        {
            SameReading<Tally, int>(json, options);
            SameReading<Measure, decimal>(json, options);
        }

        foreach (var json in new[] { "8080", "\"8080\"", "\"NaN\"", "\"+8080\"", "\"-0\"", "\" 8080\"" })
        {
            SameReading<Port, ushort>(json, options);
        }
    }

    /// <summary>
    /// A number written as text may be longer than any number needs, padded with zeros, and is read all the same.
    /// </summary>
    [Fact]
    public void A_number_written_as_long_text_is_read_as_its_underlying_value_is()
    {
        var options = new JsonSerializerOptions { NumberHandling = FromString };
        var json = $"\"{new string('0', 300)}42\"";

        SameReading<Tally, int>(json, options);
        JsonSerializer.Deserialize<Tally>(json, options).Value.Should().Be(42);
    }

    [Fact]
    public void NaN_and_the_infinities_round_trip_under_AllowNamedFloatingPointLiterals()
    {
        var options = new JsonSerializerOptions { NumberHandling = Named };

        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var json = JsonSerializer.Serialize(Signal.Create(value), options);

            JsonSerializer.Deserialize<Signal>(json, options).Value.Should().Be(value);
        }

        JsonSerializer.Serialize(Signal.Create(double.NaN), options).Should().Be("\"NaN\"");
        JsonSerializer.Serialize(Signal.Create(double.NegativeInfinity), options).Should().Be("\"-Infinity\"");
    }

    /// <summary>
    /// NaN is still a value the type judges: a bounded type that refuses it refuses it, whatever the options let the
    /// reader read.
    /// </summary>
    [Fact]
    public void A_named_literal_a_bounded_type_refuses_is_a_JsonException_carrying_the_rule()
    {
        var options = new JsonSerializerOptions { NumberHandling = Named };

        var read = () => JsonSerializer.Deserialize<Latitude>("\"NaN\"", options);

        read.Should().Throw<JsonException>().WithMessage("The value is not a valid Latitude: *");
    }

    private static void SameWriting<TSelf, TValue>(TSelf valueObject, TValue value, JsonSerializerOptions options)
    {
        var expected = Attempt(() => JsonSerializer.Serialize(value, options));
        var actual = Attempt(() => JsonSerializer.Serialize(valueObject, options));

        actual.Should().Be(expected, $"{typeof(TSelf).Name} {value} is written under {options.NumberHandling} as {typeof(TValue).Name} is");
    }

    private static void SameReading<TSelf, TValue>(string json, JsonSerializerOptions options)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var expected = Attempt(() => JsonSerializer.Deserialize<TValue>(json, options)!.ToString());
        var actual = Attempt(() => JsonSerializer.Deserialize<TSelf>(json, options).Value!.ToString());

        // Only the outcome is compared for a refusal: the messages name different types.
        (actual.StartsWith("threw", StringComparison.Ordinal) ? "threw" : actual).Should().Be(
            expected.StartsWith("threw", StringComparison.Ordinal) ? "threw" : expected,
            $"{json} is read under {options.NumberHandling} as {typeof(TValue).Name} reads it");
    }

    private static string Attempt(Func<string?> action)
    {
        try
        {
            return action() ?? "null";
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return $"threw {exception.GetType().Name}";
        }
    }

    /// <summary>A signal over a double nothing bounds, so it holds NaN and the infinities.</summary>
    [ValueObject<double>]
    public readonly partial struct Signal;

    /// <summary>A level over a float nothing bounds, so it holds NaN and the infinities.</summary>
    [ValueObject<float>]
    public readonly partial struct Level;

    /// <summary>A measure over a decimal nothing bounds or rounds, so it reads as the bare decimal does.</summary>
    [ValueObject<decimal>]
    public readonly partial struct Measure;

    /// <summary>A tally over an int nothing bounds, so it reads as the bare int does.</summary>
    [ValueObject<int>]
    public readonly partial struct Tally;
}
