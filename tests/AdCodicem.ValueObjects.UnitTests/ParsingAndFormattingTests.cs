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

    [Fact]
    public void Parse_reports_the_offending_text()
    {
        var act = () => Iban.Parse("not-an-iban");

        act.Should().Throw<ValueObjectException>()
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
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
}
