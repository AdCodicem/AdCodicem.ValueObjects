using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The JSON converter factory on a value object written by hand, which is half of what the factory is for: such a
/// value object registers no converter of its own, so the factory builds its general-purpose one.
/// </summary>
public partial class HandWrittenJsonTests
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions().AddValueObjects();

    /// <summary>Value objects written by hand: as a value, as a nullable one, and as a key.</summary>
    internal sealed record Bookmark(HandWrittenLink Link, HandWrittenLink? Mirror, Dictionary<HandWrittenCode, int> PerCode);

    [JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
    [JsonSerializable(typeof(Bookmark))]
    [JsonSerializable(typeof(HandWrittenId<RegisteredProfile>))]
    internal sealed partial class HandWrittenContext : JsonSerializerContext;

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

        var refusal = invalid.Should().Throw<ValueObjectJsonException>()
            .WithMessage("The value is not a valid HandWrittenCode: A code holds ASCII letters only.").Which;
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
        refusal.ValueObjectType.Should().Be<HandWrittenCode>();
        missing.Should().Throw<ValueObjectJsonException>().WithMessage("The value is not a valid HandWrittenCode: A code is required.")
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
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
    /// The value object prints and parses "3 items". The key carries the underlying value instead, as the JSON value
    /// does, and is read back the same way: as System.Text.Json reads a key of the underlying type, then through
    /// <c>TryCreate</c>, never through the value object's own parser, which refuses the bare number.
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
        HandWrittenItemCount.TryParse("3", null, out _).Should().BeFalse("the type parses only what it prints");
        json.Should().Be("""{"3":"pens","12":"folders"}""");
        JsonSerializer.Deserialize<Dictionary<HandWrittenItemCount, string>>(json, Options).Should().Equal(stock);
    }

    /// <summary>
    /// A key is validated as a value is, through <c>TryCreate</c>, and refused with the rule that fired.
    /// </summary>
    [Fact]
    public void A_dictionary_key_the_hand_written_value_object_rejects_is_refused_as_JSON()
    {
        var code = () => JsonSerializer.Deserialize<Dictionary<HandWrittenCode, int>>("""{"ab1":1}""", Options);
        var count = () => JsonSerializer.Deserialize<Dictionary<HandWrittenItemCount, int>>("""{"0":1}""", Options);

        code.Should().Throw<ValueObjectJsonException>()
            .WithMessage("The dictionary key is not a valid HandWrittenCode: A code holds ASCII letters only.")
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
        count.Should().Throw<ValueObjectJsonException>()
            .WithMessage("The dictionary key is not a valid HandWrittenItemCount: A count of items is positive.")
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// The general-purpose converter refuses to write what the value object rejects, as the generated one does: only
    /// an instance equal to the default can hold such a value, as a value or as a key. A value object that accepts its
    /// zero writes it.
    /// </summary>
    [Fact]
    public void A_hand_written_value_object_writes_no_value_it_rejects()
    {
#pragma warning disable VO0010 // The uninitialized instance is what the writer refuses.
        var code = () => JsonSerializer.Serialize(default(HandWrittenCode), Options);
        var key = () => JsonSerializer.Serialize(new Dictionary<HandWrittenItemCount, int> { [default] = 1 }, Options);
        var counter = JsonSerializer.Serialize(default(HandWrittenCounter), Options);
#pragma warning restore VO0010

        code.Should().Throw<ValueObjectJsonException>()
            .WithMessage("The value to write is not a valid HandWrittenCode: A code is required.")
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        key.Should().Throw<ValueObjectJsonException>()
            .WithMessage("The value to write is not a valid HandWrittenItemCount: A count of items is positive.")
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        counter.Should().Be("0", "the counter accepts its zero");
    }

    /// <summary>
    /// A factory written by hand may refuse a value without saying why: the refusal still carries a code, as a value and
    /// as a key.
    /// </summary>
    [Fact]
    public void A_value_a_hand_written_factory_refuses_without_a_reason_is_refused_as_not_parsable()
    {
        var text = EntityIdFormat.Create(MuteProfile.Prefix, IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);

        var value = () => JsonSerializer.Deserialize<HandWrittenId<MuteProfile>>(JsonSerializer.Serialize(text), Options);
        var key = () => JsonSerializer.Deserialize<Dictionary<HandWrittenId<MuteProfile>, int>>($$"""{"{{text}}":1}""", Options);

        value.Should().Throw<ValueObjectJsonException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        key.Should().Throw<ValueObjectJsonException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// A key that is not the underlying type's key at all is refused as System.Text.Json refuses it in a dictionary
    /// keyed by that type, the value object's own text included.
    /// </summary>
    [Theory]
    [InlineData("three")]
    [InlineData("3 items")]
    public void A_dictionary_key_that_is_not_one_of_the_underlying_type_is_refused_as_JSON(string key)
    {
        var json = $$"""{"{{key}}":"pens"}""";

        var asValueObject = () => JsonSerializer.Deserialize<Dictionary<HandWrittenItemCount, string>>(json, Options);
        var asUnderlying = () => JsonSerializer.Deserialize<Dictionary<int, string>>(json, Options);

        asValueObject.Should().Throw<JsonException>();
        asUnderlying.Should().Throw<JsonException>();
    }

    /// <summary>
    /// A serializer context reaches a value object written by hand through the factory too, over an underlying type the
    /// generator does not carry. The general-purpose converter reads and writes the underlying value through the
    /// context's contract for it, which listing the value object brings along.
    /// </summary>
    [Fact]
    public void A_serializer_context_serializes_a_hand_written_value_object_through_its_contract_for_the_underlying_type()
    {
        var bookmark = new Bookmark(HandWrittenLink.Create(new Uri("https://example.com/a")), null, new() { [HandWrittenCode.Create("abc")] = 1 });

        var json = JsonSerializer.Serialize(bookmark, HandWrittenContext.Default.Bookmark);
        var read = JsonSerializer.Deserialize(json, HandWrittenContext.Default.Bookmark)!;

        json.Should().Be("""{"Link":"https://example.com/a","Mirror":null,"PerCode":{"ABC":1}}""");
        read.Link.Should().Be(bookmark.Link);
        read.Mirror.Should().BeNull();
        read.PerCode.Should().Equal(bookmark.PerCode);
        HandWrittenContext.Default.GetTypeInfo(typeof(Uri)).Should().NotBeNull("listing the value object lists its underlying type");
    }

    /// <summary>
    /// Through a serializer context, what a value object written by hand rejects is refused with the code of the rule
    /// and the path, as a value, as a nullable value and as a key.
    /// </summary>
    /// <param name="json">The payload.</param>
    /// <param name="path">The path of the refused member.</param>
    /// <param name="message">The message of the refusal.</param>
    [Theory]
    [InlineData("""{"Link":"/a","Mirror":null,"PerCode":{}}""", "$.Link", "The value is not a valid HandWrittenLink: A link is an absolute URI.")]
    [InlineData("""{"Link":"https://example.com/a","Mirror":"/b","PerCode":{}}""", "$.Mirror", "The value is not a valid HandWrittenLink: A link is an absolute URI.")]
    [InlineData("""{"Link":"https://example.com/a","Mirror":null,"PerCode":{"ab1":1}}""", "$.PerCode.ab1", "The dictionary key is not a valid HandWrittenCode: A code holds ASCII letters only.")]
    public void A_serializer_context_refuses_what_a_hand_written_value_object_rejects_with_the_code_and_the_path(string json, string path, string message)
    {
        var refusal = FluentActions.Invoking(() => JsonSerializer.Deserialize(json, HandWrittenContext.Default.Bookmark))
            .Should().Throw<ValueObjectJsonException>().WithMessage(message).Which;

        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
        refusal.Path.Should().Be(path);
    }

    /// <summary>
    /// The factory closes the general-purpose converter over the type arguments the descriptor hands back, the
    /// underlying type included, here one the generator does not carry.
    /// </summary>
    [Fact]
    public void The_factory_closes_the_general_purpose_converter_over_the_descriptor()
    {
        new ValueObjectJsonConverterFactory().CreateConverter(typeof(HandWrittenLink), Options)
            .Should().BeOfType<ValueObjectJsonConverter<HandWrittenLink, Uri>>();
    }

    /// <summary>
    /// A value object written by hand and registered without a converter, as native AOT asks, has a descriptor built
    /// in code rather than by reflection, and the factory serves it the general-purpose converter through it.
    /// </summary>
    [Fact]
    public void A_hand_written_value_object_registered_without_a_converter_gets_the_general_purpose_one()
    {
        ValueObjectRegistry.Register<HandWrittenId<RegisteredProfile>, string>(HandWrittenId<RegisteredProfile>.Schema);
        var id = HandWrittenId<RegisteredProfile>.New();

        var json = JsonSerializer.Serialize(id, HandWrittenContext.Default.HandWrittenIdRegisteredProfile);

        ValueObjectRegistry.TryGet(typeof(HandWrittenId<RegisteredProfile>), out var descriptor).Should().BeTrue();
        descriptor!.JsonConverter.Should().BeNull("it was registered without one");
        HandWrittenContext.Default.Options.GetConverter(typeof(HandWrittenId<RegisteredProfile>))
            .Should().BeOfType<ValueObjectJsonConverter<HandWrittenId<RegisteredProfile>, string>>();
        json.Should().Be($"\"{id.Value}\"");
        JsonSerializer.Deserialize(json, HandWrittenContext.Default.HandWrittenIdRegisteredProfile).Should().Be(id);
    }

    /// <summary>
    /// Called directly, with options no serializer has used yet and no resolver set, the general-purpose converter
    /// resolves the underlying type as the serializer itself would, rather than finding no contract for it.
    /// </summary>
    [Fact]
    public void The_general_purpose_converter_called_directly_with_fresh_options_resolves_the_underlying_type()
    {
        var converter = new ValueObjectJsonConverter<HandWrittenLink, Uri>();
        var link = HandWrittenLink.Create(new Uri("https://example.com/a"));
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            converter.Write(writer, link, new JsonSerializerOptions());
            writer.WriteStartObject();
            converter.WriteAsPropertyName(writer, link, new JsonSerializerOptions());
            writer.WriteNumberValue(1);
            writer.WriteEndObject();
            writer.WriteEndArray();
        }

        var value = new Utf8JsonReader("\"https://example.com/a\""u8);
        value.Read();
        var key = new Utf8JsonReader("{\"https://example.com/a\":1}"u8);
        key.Read();
        key.Read();

        System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan).Should().Be("""["https://example.com/a",{"https://example.com/a":1}]""");
        converter.Read(ref value, typeof(HandWrittenLink), new JsonSerializerOptions()).Should().Be(link);
        converter.ReadAsPropertyName(ref key, typeof(HandWrittenLink), new JsonSerializerOptions()).Should().Be(link);
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

    /// <summary>
    /// Nor does it ask for <see cref="Nullable{T}"/> over a value object, which it wraps around the value object's own
    /// converter: built for the nullable type, the converter would hand back the wrong type.
    /// </summary>
    [Fact]
    public void The_factory_builds_no_converter_for_a_nullable_value_object()
    {
        new ValueObjectJsonConverterFactory().CreateConverter(typeof(HandWrittenCode?), new JsonSerializerOptions()).Should().BeNull();
    }
}
