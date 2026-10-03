using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Covers the boxed surface every integration package goes through.
/// </summary>
/// <remarks>
/// Model binding, FluentValidation, Dapper and Newtonsoft.Json all reach a value object through a
/// <see cref="ValueObjectDescriptor"/> rather than through its static abstract members. Anything the descriptor
/// drops on the floor is invisible to the strongly typed tests and yet visible to every consumer, so the
/// descriptor deserves coverage of its own.
/// </remarks>
public sealed class DescriptorTests
{
    private static ValueObjectDescriptor Descriptor<T>()
    {
        ValueObjectRegistry.TryGet(typeof(T), out var descriptor).Should().BeTrue();

        return descriptor!;
    }

    [Fact]
    public void A_rejected_parse_reports_the_rule_that_rejected_it_not_a_generic_failure()
    {
        // The check digits are wrong: the last digit should be a 9. Length and pattern are both satisfied, so
        // only the value object's own rule can reject this, and that rule is what must be reported.
        var parsed = Descriptor<Iban>().TryParse(
            "FR7630006000011234567890188",
            CultureInfo.InvariantCulture,
            out var result,
            out var validation);

        parsed.Should().BeFalse();
        result.Should().BeNull();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
        validation.ErrorMessage.Should().Contain("check digits");
    }

    [Fact]
    public void Text_the_underlying_type_cannot_read_is_reported_as_unparsable()
    {
        var parsed = Descriptor<Amount>().TryParse(
            "not a number",
            CultureInfo.InvariantCulture,
            out _,
            out var validation);

        parsed.Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public void A_value_outside_a_closed_set_is_reported_as_such()
    {
        var parsed = Descriptor<CountryCode>().TryParse(
            "ZZ",
            CultureInfo.InvariantCulture,
            out _,
            out var validation);

        parsed.Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.NotAKnownValue);
    }

    [Fact]
    public void An_accepted_parse_normalizes_on_the_way_through()
    {
        var parsed = Descriptor<Iban>().TryParse(
            "fr76 3000 6000 0112 3456 7890 189",
            CultureInfo.InvariantCulture,
            out var result,
            out var validation);

        parsed.Should().BeTrue();
        validation.IsValid.Should().BeTrue();
        result.Should().Be(Iban.Create("FR7630006000011234567890189"));
    }

    [Fact]
    public void Every_rejection_carries_an_error_code()
    {
        // Consumers turn these codes into ProblemDetails, so a rejection without one surfaces to an API client
        // as a null. Sweep every registered type rather than trusting the handful spelled out above.
        foreach (var descriptor in ValueObjectRegistry.GetRegistered())
        {
            descriptor.TryParse(" an unlikely value ", CultureInfo.InvariantCulture, out _, out var validation);

            if (!validation.IsValid)
            {
                validation.ErrorCode.Should().NotBeNullOrEmpty(
                    "'{0}' must say why it rejected a value",
                    descriptor.ValueObjectType.Name);
            }
        }
    }

    /// <summary>
    /// A boxed null is "no value". A string value object says so through its own rule; a decimal one has no null to
    /// say it about, so the descriptor must, rather than quietly reading null as zero.
    /// </summary>
    [Fact]
    public void A_boxed_null_is_rejected_as_required_whatever_the_underlying_type()
    {
        foreach (var descriptor in new[] { Descriptor<CountryCode>(), Descriptor<Amount>(), Descriptor<Quantity>() })
        {
            descriptor.TryCreate(null, out var result, out var validation).Should().BeFalse(descriptor.ValueObjectType.Name);
            result.Should().BeNull();
            validation.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);

            var create = () => descriptor.Create(null);
            create.Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        }
    }

    [Fact]
    public void The_boxed_creation_path_reports_the_same_rule()
    {
        var created = Descriptor<Iban>().TryCreate("FR7630006000011234567890188", out _, out var validation);

        created.Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    /// <summary>
    /// The boxed factory throws the exception of the generated <c>Create</c>: a value object classified as personal
    /// data leaves the rejected value off it there too, and one nobody classified hands it over.
    /// </summary>
    [Fact]
    public void The_boxed_creation_path_leaves_the_value_of_a_classified_value_object_off_its_exception()
    {
        var classified = () => Descriptor<PassportNumber>().Create("X1");
        var unclassified = () => Descriptor<Amount>().Create(-1m);

        var hidden = classified.Should().Throw<ValueObjectException>().Which;
        hidden.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort);
        hidden.AttemptedValue.Should().BeNull();
        hidden.Message.Should().NotContain("X1");
        unclassified.Should().Throw<ValueObjectException>().Which.AttemptedValue.Should().Be(-1m);
    }

    [Fact]
    public void A_closed_value_set_hands_out_the_same_box_every_time()
    {
        var descriptor = Descriptor<CountryCode>();

        var first = descriptor.Create("FR");
        var second = descriptor.Create("FR");

        // Boxing a value object allocates; a closed set has a fixed number of them, so they are boxed once.
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void The_shared_box_is_also_used_on_the_parsing_path()
    {
        var descriptor = Descriptor<CountryCode>();

        descriptor.TryParse("FR", CultureInfo.InvariantCulture, out var parsed, out _).Should().BeTrue();
        descriptor.TryCreate("FR", out var created, out _).Should().BeTrue();

        parsed.Should().BeSameAs(created);
        parsed.Should().BeSameAs(descriptor.Create("FR"));
    }

    [Fact]
    public void An_open_value_set_is_not_cached_and_stays_correct()
    {
        var descriptor = Descriptor<Iban>();

        var first = descriptor.Create("FR7630006000011234567890189");
        var second = descriptor.Create("FR7630006000011234567890189");

        first.Should().NotBeSameAs(second, "only a closed set has a fixed number of instances to share");
        first.Should().Be(second);
    }

    [Fact]
    public void The_boxed_creation_path_accepts_an_open_set_value_into_a_box_of_its_own()
    {
        Descriptor<Amount>().TryCreate(12.5m, out var created, out var validation).Should().BeTrue();

        validation.IsValid.Should().BeTrue();
        created.Should().Be(Amount.Create(12.50m));
    }

    /// <summary>
    /// The shared boxes are keyed with the default comparer, so a spelling the type accepts through its
    /// case-insensitive comparison misses them. A miss costs an allocation, never correctness.
    /// </summary>
    [Fact]
    public void A_closed_set_compared_case_insensitively_accepts_another_spelling_through_the_boxed_paths()
    {
        var descriptor = Descriptor<DocumentStatus>();

        descriptor.TryCreate("DRAFT", out var created, out _).Should().BeTrue();
        descriptor.TryParse("DRAFT", CultureInfo.InvariantCulture, out var parsed, out _).Should().BeTrue();

        created.Should().Be(DocumentStatus.Draft);
        parsed.Should().Be(DocumentStatus.Draft);
        created.Should().NotBeSameAs(descriptor.Create("draft"));
    }

    [Fact]
    public void A_closed_schema_whose_known_values_are_not_of_the_underlying_type_shares_no_box()
    {
        var descriptor = ValueObjectDescriptor.For<CountryCode, string>(
            new ValueObjectSchema { IsClosedValueSet = true, KnownValues = [1, 2] });

        var first = descriptor.Create("FR");
        var second = descriptor.Create("FR");

        first.Should().Be(second);
        first.Should().NotBeSameAs(second);
    }

    /// <summary>
    /// The trusted path builds whatever it is handed, valid or not, and still hands out the shared box of a
    /// closed set's member. Nothing in the library calls it; it is public for callers reading their own storage.
    /// </summary>
    [Fact]
    public void The_trusted_boxed_path_skips_validation_and_shares_the_boxes_of_a_closed_set()
    {
        var iban = Descriptor<Iban>();
        var country = Descriptor<CountryCode>();

        iban.GetValue(iban.CreateUnchecked("not an iban")).Should().Be("not an iban");
        country.CreateUnchecked("FR").Should().BeSameAs(country.Create("FR"));
        country.GetValue(country.CreateUnchecked("ES")).Should().Be("ES", "a trusted value is never checked against the set");
        ((CountryCode)country.CreateUnchecked(null)).IsDefault.Should().BeTrue();
    }

    [Fact]
    public void A_parse_without_a_provider_reads_the_invariant_culture()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Descriptor<Amount>().TryParse("1234.5", null, out var parsed, out _).Should().BeTrue();

            parsed.Should().Be(Amount.Create(1234.50m));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>
    /// The descriptor hands a null provider to the generated parser as the invariant culture, which takes no group
    /// separator in a real: <c>1,234.5</c> is not a number there, where the type's own styles would read it. A culture
    /// that is not the invariant one keeps those styles.
    /// </summary>
    /// <param name="type">The value object, over a <c>decimal</c>, a <c>double</c> or a <c>float</c>.</param>
    [Theory]
    [InlineData(typeof(Amount))]
    [InlineData(typeof(Mass))]
    [InlineData(typeof(Luminance))]
    public void A_parse_without_a_provider_takes_no_group_separator_in_a_real(Type type)
    {
        ValueObjectRegistry.TryGet(type, out var descriptor).Should().BeTrue();

        foreach (var provider in new IFormatProvider?[] { null, CultureInfo.InvariantCulture })
        {
            descriptor!.TryParse("1,234.5", provider, out var refused, out var validation).Should().BeFalse();
            refused.Should().BeNull();
            validation.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        }

        descriptor!.TryParse("1,234.5", CultureInfo.GetCultureInfo("en-US"), out var parsed, out _).Should().BeTrue();
        Convert.ToDecimal(descriptor.GetValue(parsed!), CultureInfo.InvariantCulture).Should().Be(1234.5m);
    }

    /// <summary>
    /// A hand-written parser may answer no without saying why. The descriptor supplies the reason, so a caller can
    /// still rely on a rejection carrying a code.
    /// </summary>
    [Fact]
    public void A_hand_written_parser_that_gives_no_reason_is_reported_as_unparsable()
    {
        ValueObjectRegistry.TryResolve(typeof(HandWrittenCounter), out var descriptor).Should().BeTrue();

        descriptor!.TryParse("abc", CultureInfo.InvariantCulture, out var result, out var validation).Should().BeFalse();

        result.Should().BeNull();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        validation.ErrorMessage.Should().Contain(nameof(HandWrittenCounter));
    }

    [Fact]
    public void The_descriptor_exposes_the_underlying_value_unwrapped()
    {
        var descriptor = Descriptor<Iban>();
        var iban = Iban.Create("FR7630006000011234567890189");

        descriptor.GetValue(iban).Should().Be("FR7630006000011234567890189");
        descriptor.Format(iban).Should().Be("FR7630006000011234567890189");
        descriptor.ValueType.Should().Be<string>();
    }
}
