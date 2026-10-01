using System.Text.Json;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The JSON converter factory on a value object written by hand, which is half of what the factory is for: such a
/// value object registers no converter of its own, so the factory builds its general-purpose one.
/// </summary>
public class HandWrittenJsonTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions().AddValueObjects();

    [Fact]
    public void A_hand_written_value_object_travels_as_its_bare_value_through_the_factory()
    {
        JsonSerializer.Serialize(HandWrittenCode.Create("abc"), Options).Should().Be("\"ABC\"");
        JsonSerializer.Deserialize<HandWrittenCode>("\" abc \"", Options).Should().Be(HandWrittenCode.Create("ABC"));
    }

    [Fact]
    public void A_hand_written_value_object_enforces_its_rules_when_read()
    {
        var invalid = () => JsonSerializer.Deserialize<HandWrittenCode>("\"ab1\"", Options);
        var missing = () => JsonSerializer.Deserialize<HandWrittenCode>("null", Options);

        invalid.Should().Throw<JsonException>().WithMessage("The value is not a valid HandWrittenCode: A code holds ASCII letters only.");
        missing.Should().Throw<JsonException>().WithMessage("The value is not a valid HandWrittenCode: A code is required.");
    }

    /// <summary>
    /// A key is the underlying value as System.Text.Json writes a key of its type, which a string does at any
    /// length.
    /// </summary>
    [Fact]
    public void A_hand_written_value_object_works_as_a_dictionary_key_of_any_length()
    {
        var dictionary = new Dictionary<HandWrittenCode, int>
        {
            [HandWrittenCode.Create("abc")] = 1,
            [HandWrittenCode.Create(new string('a', 100))] = 2,
        };

        var json = JsonSerializer.Serialize(dictionary, Options);

        json.Should().Be($$"""{"ABC":1,"{{new string('A', 100)}}":2}""");
        JsonSerializer.Deserialize<Dictionary<HandWrittenCode, int>>(json, Options).Should().BeEquivalentTo(dictionary);
    }

    /// <summary>
    /// The value object prints "3 items", which its own parser does not read: the key carries the underlying value
    /// instead, as the JSON value does, and reads back.
    /// </summary>
    [Fact]
    public void A_hand_written_dictionary_key_carries_the_underlying_value_whatever_the_type_prints()
    {
        var stock = new Dictionary<HandWrittenItemCount, string>
        {
            [HandWrittenItemCount.Create(3)] = "pens",
            [HandWrittenItemCount.Create(12)] = "folders",
        };

        var json = JsonSerializer.Serialize(stock, Options);

        $"{HandWrittenItemCount.Create(3)}".Should().Be("3 items");
        json.Should().Be("""{"3":"pens","12":"folders"}""");
        JsonSerializer.Deserialize<Dictionary<HandWrittenItemCount, string>>(json, Options).Should().Equal(stock);
    }

    [Fact]
    public void A_dictionary_key_the_hand_written_value_object_rejects_is_refused_as_JSON()
    {
        var act = () => JsonSerializer.Deserialize<Dictionary<HandWrittenCode, int>>("""{"ab1":1}""", Options);

        act.Should().Throw<JsonException>().WithMessage("The dictionary key is not a valid HandWrittenCode.");
    }

    /// <summary>
    /// The serializer never asks for it, since the factory does not claim such a type; a caller using the factory
    /// directly gets no converter rather than an exception.
    /// </summary>
    [Fact]
    public void The_factory_builds_no_converter_for_a_type_carrying_only_the_marker()
    {
        new ValueObjectJsonConverterFactory().CreateConverter(typeof(MarkerOnlyValue), new JsonSerializerOptions()).Should().BeNull();
    }
}
