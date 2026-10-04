using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Identifiers;
using FsCheck;
using FsCheck.Fluent;
using Microsoft.AspNetCore.Http;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The code of a rule carried across an exception an integration throws in its own terms, read back through
/// <see cref="ValueObjectErrors.TryGetCode"/>, called directly with what no integration throws.
/// </summary>
public class ErrorCodeCarrierTests
{
    [Fact]
    public void The_code_is_read_from_a_value_object_exception()
    {
        var exception = new ValueObjectException("refused", typeof(Iban), ValueObjectErrorCodes.TooShort, null);

        ValueObjectErrors.TryGetCode(exception, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);
    }

    [Fact]
    public void The_code_is_read_from_a_JSON_refusal_and_from_its_data()
    {
        var exception = new ValueObjectJsonException("refused", typeof(Iban), ValueObjectErrorCodes.TooLong);

        exception.Should().BeAssignableTo<JsonException>();
        exception.Message.Should().Be("refused");
        exception.ValueObjectType.Should().Be<Iban>();
        exception.ErrorCode.Should().Be(ValueObjectErrorCodes.TooLong);
        exception.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.TooLong);
        ValueObjectErrors.TryGetCode(exception, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooLong);
    }

    /// <summary>
    /// The exception types of other ecosystems have no place for a code: it travels in their data, under the one key.
    /// </summary>
    [Fact]
    public void The_code_is_read_from_the_data_of_any_exception()
    {
        var exception = new DataException("refused");
        exception.Data[ValueObjectErrors.ErrorCodeKey] = "iban.check_digits";

        ValueObjectErrors.TryGetCode(exception, out var code).Should().BeTrue();
        code.Should().Be("iban.check_digits");
    }

    /// <summary>
    /// A framework wraps the exception that carries the code, as a minimal API wraps a body it cannot read in a
    /// <see cref="BadHttpRequestException"/>: the chain is read down to it, and the outermost code wins.
    /// </summary>
    [Fact]
    public void The_code_is_read_from_a_wrapped_exception_and_the_outermost_one_wins()
    {
        var refusal = new ValueObjectJsonException("refused", typeof(Iban), ValueObjectErrorCodes.TooShort);
        var wrapped = new BadHttpRequestException("Failed to read the body.", new InvalidOperationException("read", refusal));
        var outer = new InvalidOperationException("outer", new ValueObjectException("inner", typeof(Iban), ValueObjectErrorCodes.Required, null));
        outer.Data[ValueObjectErrors.ErrorCodeKey] = ValueObjectErrorCodes.NotParsable;

        ValueObjectErrors.TryGetCode(wrapped, out var fromWrapped).Should().BeTrue();
        ValueObjectErrors.TryGetCode(outer, out var fromOuter).Should().BeTrue();

        fromWrapped.Should().Be(ValueObjectErrorCodes.TooShort);
        fromOuter.Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// A value object exception built without a code, and data that holds no text under the key, say nothing: the
    /// chain is read on, and an exception no value object raised carries no code.
    /// </summary>
    [Fact]
    public void An_exception_carrying_no_code_gives_none()
    {
        var noCode = new ValueObjectException("no code");
        var notText = new InvalidOperationException("not text", noCode);
        notText.Data[ValueObjectErrors.ErrorCodeKey] = 42;
        var empty = new InvalidOperationException("empty", notText);
        empty.Data[ValueObjectErrors.ErrorCodeKey] = string.Empty;
        var codeBelow = new InvalidOperationException("below", new ValueObjectException("coded", typeof(Iban), ValueObjectErrorCodes.TooLong, null));
        var emptyCodeAbove = new ValueObjectException("empty code", typeof(Iban), string.Empty, null);
        emptyCodeAbove.Data[ValueObjectErrors.ErrorCodeKey] = ValueObjectErrorCodes.OutOfRange;
        var noCodeAbove = new ValueObjectException("above", codeBelow);

        ValueObjectErrors.TryGetCode(empty, out var none).Should().BeFalse();
        none.Should().BeNull();
        ValueObjectErrors.TryGetCode(new JsonException("plain"), out _).Should().BeFalse();
        ValueObjectErrors.TryGetCode(noCodeAbove, out var below).Should().BeTrue();
        below.Should().Be(ValueObjectErrorCodes.TooLong);
        ValueObjectErrors.TryGetCode(emptyCodeAbove, out var fromData).Should().BeTrue();
        fromData.Should().Be(ValueObjectErrorCodes.OutOfRange, "an empty code says nothing, and the data is read instead");
    }

    [Fact]
    public void Reading_no_exception_is_refused()
        => FluentActions.Invoking(() => ValueObjectErrors.TryGetCode(null!, out _)).Should().Throw<ArgumentNullException>();

    /// <summary>
    /// Thrown without a message, as the generated converter throws one for a number its underlying type cannot hold,
    /// a refusal is given by System.Text.Json the message it gives a token the reader cannot read, naming the type and
    /// the path; the code is there either way.
    /// </summary>
    [Fact]
    public void A_JSON_refusal_without_a_message_is_given_the_one_System_Text_Json_writes()
    {
        var exception = FluentActions.Invoking(() => JsonSerializer.Deserialize<Quantity>("1e400"))
            .Should().Throw<ValueObjectJsonException>().Which;

        exception.Message.Should().Be($"The JSON value could not be converted to {typeof(Quantity)}. Path: $ | LineNumber: 0 | BytePositionInLine: 5.");
        exception.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        new ValueObjectJsonException(null, typeof(Quantity), ValueObjectErrorCodes.NotParsable).Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void A_JSON_refusal_names_a_type_and_a_code()
    {
        FluentActions.Invoking(() => new ValueObjectJsonException("refused", null!, ValueObjectErrorCodes.TooLong))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ValueObjectJsonException("refused", typeof(Iban), null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ValueObjectJsonException("refused", typeof(Iban), string.Empty))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(ValueObjectErrorCodes.Required, "VALUE_OBJECT_REQUIRED")]
    [InlineData(ValueObjectErrorCodes.InvalidFormat, "VALUE_OBJECT_INVALID_FORMAT")]
    [InlineData(ValueObjectErrorCodes.OutOfRange, "VALUE_OBJECT_OUT_OF_RANGE")]
    [InlineData(ValueObjectErrorCodes.TooLong, "VALUE_OBJECT_TOO_LONG")]
    [InlineData(ValueObjectErrorCodes.TooShort, "VALUE_OBJECT_TOO_SHORT")]
    [InlineData(ValueObjectErrorCodes.NotAKnownValue, "VALUE_OBJECT_NOT_A_KNOWN_VALUE")]
    [InlineData(ValueObjectErrorCodes.NotParsable, "VALUE_OBJECT_NOT_PARSABLE")]
    [InlineData("iban.check_digits", "IBAN_CHECK_DIGITS")]
    [InlineData(IdentifierErrorCodes.UnknownPrefix, "VALUE_OBJECT_ID_UNKNOWN_PREFIX")]
    [InlineData("iso3166.alpha2", "ISO3166_ALPHA2")]
    [InlineData("..delivery-date..sunday__", "DELIVERY_DATE_SUNDAY")]
    [InlineData("café.prix", "CAF_PRIX")]
    [InlineData("ABC", "ABC")]
    [InlineData("a.b", "A_B")]
    public void A_code_maps_to_the_upper_snake_case_gRPC_takes_as_a_reason(string code, string reason)
        => ValueObjectErrorCodes.ToUpperSnakeCase(code).Should().Be(reason);

    /// <summary>
    /// A reason starts with a letter and holds from 3 to 63 characters: a code that maps to anything else is refused,
    /// rather than truncated or given a prefix, which could make two codes one.
    /// </summary>
    /// <param name="code">A code that maps to no valid reason.</param>
    [Theory]
    [InlineData("")]
    [InlineData("...")]
    [InlineData("ab")]
    [InlineData("3d.secure")]
    [InlineData("_9lives")]
    public void A_code_that_maps_to_no_valid_reason_is_refused(string code)
        => FluentActions.Invoking(() => ValueObjectErrorCodes.ToUpperSnakeCase(code))
            .Should().Throw<ArgumentException>().WithParameterName("errorCode");

    [Fact]
    public void A_reason_holds_no_more_than_63_characters()
    {
        var longest = new string('a', 63);

        ValueObjectErrorCodes.ToUpperSnakeCase(longest).Should().Be(new string('A', 63));
        ValueObjectErrorCodes.ToUpperSnakeCase("." + longest + ".").Should().Be(new string('A', 63));
        FluentActions.Invoking(() => ValueObjectErrorCodes.ToUpperSnakeCase(longest + "a"))
            .Should().Throw<ArgumentException>().WithParameterName("errorCode");
        FluentActions.Invoking(() => ValueObjectErrorCodes.ToUpperSnakeCase(null!)).Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Whatever the code, the mapping gives a reason gRPC takes, or refuses the code: never something in between.
    /// </summary>
    [Fact]
    public void Every_code_maps_to_a_valid_reason_or_is_refused()
    {
        var reason = new Regex("^[A-Z][A-Z0-9_]+[A-Z0-9]$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var mapped = 0;

        Prop.ForAll(
                Arb.From(Gen.OneOf(
                    ArbMap.Default.ArbFor<string>().Generator.Select(text => text ?? string.Empty),
                    Gen.Elements("value_object.too_long", "a.b.c", "x__y", "9z.ok", "ok.9z"))),
                code =>
                {
                    string result;
                    try
                    {
                        result = ValueObjectErrorCodes.ToUpperSnakeCase(code);
                    }
                    catch (ArgumentException)
                    {
                        return;
                    }

                    mapped++;
                    reason.IsMatch(result).Should().BeTrue("'{0}' maps to '{1}'", code, result);
                    result.Length.Should().BeLessThanOrEqualTo(63);
                    result.Should().NotContain("__");
                })
            .Check(Config.QuickThrowOnFailure.WithMaxTest(500).WithQuietOnSuccess(true));

        mapped.Should().BePositive("some codes must map, or the property says nothing");
    }
}
