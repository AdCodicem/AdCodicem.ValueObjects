using System.Globalization;

namespace AdCodicem.ValueObjects.UnitTests;

public class ParsingAndFormattingTests
{
    [Fact]
    public void Parse_and_ToString_round_trip()
    {
        var iban = Iban.Create("FR7630006000011234567890189");

        Iban.Parse(iban.ToString()).Should().Be(iban);
    }

    [Fact]
    public void Parse_accepts_a_span_without_going_through_a_string()
    {
        ReadOnlySpan<char> text = "  de89370400440532013000  ";

        Iban.Parse(text, CultureInfo.InvariantCulture).Value.Should().Be("DE89370400440532013000");
    }

    /// <summary>
    /// Parse throws with the rule TryParse reports, so a caller catching the exception can act on the same code a
    /// caller of TryParse gets.
    /// </summary>
    [Theory]
    [InlineData("not-an-iban", ValueObjectErrorCodes.TooShort)] // nine characters once the dashes are stripped
    [InlineData("1R7630006000011234567890189", ValueObjectErrorCodes.InvalidFormat)] // the declared pattern
    [InlineData("FR7630006000011234567890188", ValueObjectErrorCodes.InvalidFormat)] // the check digits of the hook
    [InlineData("", ValueObjectErrorCodes.Required)]
    public void Parse_reports_the_offending_text_and_the_rule_it_broke(string text, string expectedErrorCode)
    {
        Iban.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse();

        var exception = FluentActions.Invoking(() => Iban.Parse(text)).Should().Throw<ValueObjectException>().Which;

        exception.ErrorCode.Should().Be(expectedErrorCode).And.Be(validation.ErrorCode);
        exception.Message.Should().Be($"'{text}' is not a valid Iban: {validation.ErrorMessage}");
        exception.AttemptedValue.Should().Be(text);
        exception.ValueObjectType.Should().Be<Iban>();
    }

    [Theory]
    [InlineData("not-a-number", ValueObjectErrorCodes.NotParsable)]
    [InlineData("-5", ValueObjectErrorCodes.OutOfRange)]
    public void Parse_reports_not_parsable_only_for_text_that_is_not_of_the_underlying_type(
        string text,
        string expectedErrorCode)
    {
        FluentActions.Invoking(() => Amount.Parse(text, CultureInfo.InvariantCulture))
            .Should().Throw<ValueObjectException>()
            .Which.ErrorCode.Should().Be(expectedErrorCode);
    }

    [Fact]
    public void Parse_reports_a_value_outside_a_closed_set_as_such()
    {
        FluentActions.Invoking(() => CountryCode.Parse("zz"))
            .Should().Throw<ValueObjectException>()
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotAKnownValue);
    }

    [Theory]
    [InlineData("not-an-iban")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_returns_false_rather_than_throwing(string? text)
    {
        Iban.TryParse(text, out var iban).Should().BeFalse();
#pragma warning disable VO0010 // Asserting on the uninitialized value is the point of this test.
        iban.Should().Be(default(Iban));
#pragma warning restore VO0010
    }

    [Fact]
    public void A_value_object_over_a_non_string_parses_through_its_underlying_type()
    {
        var id = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001"));

        CustomerId.Parse(id.ToString()).Should().Be(id);
        CustomerId.TryParse("nonsense", out _).Should().BeFalse();
    }

    [Fact]
    public void A_date_value_object_round_trips_through_the_ISO_form()
    {
        var birthDate = BirthDate.Create(new DateOnly(1980, 5, 17));

        birthDate.ToString().Should().Be("1980-05-17");
        BirthDate.Parse("1980-05-17").Should().Be(birthDate);
    }

    [Fact]
    public void ToString_is_culture_independent_so_that_a_value_round_trips_anywhere()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var amount = Amount.Create(1234.5m);

            amount.ToString().Should().Be("1234.50");
            Amount.Parse(amount.ToString()).Should().Be(amount);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ToString_honours_an_explicit_format_provider()
    {
        var amount = Amount.Create(1234.5m);

        // The group separator glyph is an ICU detail, so assert on the decimal separator, which is the point.
        amount.ToString("N2", new CultureInfo("fr-FR")).Should().EndWith("234,50");
        amount.ToString("N2", CultureInfo.InvariantCulture).Should().Be("1,234.50");
    }

    [Theory]
    [InlineData(Iban.Formats.Electronic, "FR7630006000011234567890189")]
    [InlineData(Iban.Formats.Print, "FR76 3000 6000 0112 3456 7890 189")]
    public void A_named_format_reaches_the_authors_own_formatter(string format, string expected)
    {
        var iban = Iban.Create("FR7630006000011234567890189");

        iban.ToString(format, CultureInfo.InvariantCulture).Should().Be(expected);
        string.Format(CultureInfo.InvariantCulture, "{0:" + format + "}", iban).Should().Be(expected);
    }

    [Fact]
    public void The_masked_format_keeps_only_the_country_and_the_last_four_characters()
    {
        var masked = Iban.Create("FR7630006000011234567890189").ToString(Iban.Formats.Masked, CultureInfo.InvariantCulture);

        masked.Should().HaveLength(27).And.StartWith("FR").And.EndWith("0189");
        masked[2..^4].ToCharArray().Should().AllBeEquivalentTo('*');
    }

    [Fact]
    public void TryFormat_writes_into_a_caller_owned_buffer()
    {
        var iban = Iban.Create("FR7630006000011234567890189");
        Span<char> buffer = stackalloc char[64];

        iban.TryFormat(buffer, out var written, Iban.Formats.Print, CultureInfo.InvariantCulture).Should().BeTrue();
        buffer[..written].ToString().Should().Be("FR76 3000 6000 0112 3456 7890 189");
    }

    [Fact]
    public void TryFormat_reports_a_buffer_that_is_too_small()
    {
        var iban = Iban.Create("FR7630006000011234567890189");
        Span<char> buffer = stackalloc char[4];

        iban.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture).Should().BeFalse();
        written.Should().Be(0);
    }

    [Theory]
    [InlineData("FR7630006000011234567890188", ValueObjectErrorCodes.InvalidFormat)]
    [InlineData("FR76", ValueObjectErrorCodes.TooShort)]
    public void TryParse_separates_a_malformed_text_from_a_violated_rule(string text, string expectedErrorCode)
    {
        Iban.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse();

        validation.ErrorCode.Should().Be(expectedErrorCode);
    }

    [Fact]
    public void TryParse_reports_text_that_is_not_even_of_the_underlying_shape()
    {
        Amount.TryParse("not-a-number", CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse();

        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        validation.ErrorMessage.Should().Contain("decimal");
    }

    [Fact]
    public void TryParse_reports_a_rule_violated_by_text_that_does_parse()
    {
        Amount.TryParse("-5", CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse();

        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void Interpolation_goes_through_the_span_formatter()
    {
        var amount = Amount.Create(42m);

        $"total: {amount}".Should().Be("total: 42.00");
    }

    [Fact]
    public void A_time_keeps_its_seconds_through_text()
    {
        var opening = OpeningTime.Create(new TimeOnly(9, 30, 15));

        var text = opening.ToString(null, CultureInfo.InvariantCulture);

        OpeningTime.Parse(text, CultureInfo.InvariantCulture).Should().Be(opening);
    }

    [Fact]
    public void Dates_and_times_interpolate_in_the_form_ToString_writes()
    {
        var birthDate = BirthDate.Create(new DateOnly(1980, 5, 17));
        var recorded = RecordedAt.Create(new DateTime(2024, 6, 1, 12, 30, 45, 123, DateTimeKind.Utc));
        var occurred = OccurredAt.Create(new DateTimeOffset(2024, 6, 1, 12, 30, 45, 123, TimeSpan.FromHours(2)));

        $"{birthDate}".Should().Be("1980-05-17");
        $"{recorded}".Should().Be("2024-06-01T12:30:45.1230000Z");
        $"{occurred}".Should().Be("2024-06-01T12:30:45.1230000+02:00");
        recorded.ToString(null, CultureInfo.InvariantCulture).Should().Be(recorded.ToString());
    }

    [Fact]
    public void A_string_formatter_formats_interpolation_and_spans_too()
    {
        var phone = PhoneNumber.Create("+33123456789");
        var buffer = new char[32];

        $"{phone:G}".Should().Be("+33 123 456 789");
        phone.TryFormat(buffer, out var written, PhoneNumber.Grouped, null).Should().BeTrue();
        new string(buffer, 0, written).Should().Be("+33 123 456 789");
    }

    [Fact]
    public void With_both_formatting_hooks_the_string_formatter_answers_every_formatting_member()
    {
        var floor = Floor.Create(3);
        var buffer = new char[32];

        floor.ToString(null, CultureInfo.InvariantCulture).Should().Be("floor 3");
        $"{floor}".Should().Be("floor 3");
        floor.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture).Should().BeTrue();
        new string(buffer, 0, written).Should().Be("floor 3");
        $"{floor:D2}".Should().Be("03");
    }

    /// <summary>
    /// A formatting hook takes over formatting entirely, the default format included, so ToString() writes the
    /// text interpolation and ToString(null, null) write rather than the bare value.
    /// </summary>
    [Fact]
    public void ToString_goes_through_the_formatting_hook_with_the_default_format()
    {
        var celsius = Celsius.Create(21);
        var floor = Floor.Create(3);

        celsius.ToString().Should().Be("21 °C").And.Be(celsius.ToString(null, null)).And.Be($"{celsius}");
        floor.ToString().Should().Be("floor 3").And.Be(floor.ToString(null, null)).And.Be($"{floor}");
    }
}
