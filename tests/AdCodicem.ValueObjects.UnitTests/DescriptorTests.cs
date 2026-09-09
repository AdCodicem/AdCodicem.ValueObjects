using System.Globalization;
using AdCodicem.ValueObjects.Metadata;

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

    [Fact]
    public void The_boxed_creation_path_reports_the_same_rule()
    {
        var created = Descriptor<Iban>().TryCreate("FR7630006000011234567890188", out _, out var validation);

        created.Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
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
    public void The_descriptor_exposes_the_underlying_value_unwrapped()
    {
        var descriptor = Descriptor<Iban>();
        var iban = Iban.Create("FR7630006000011234567890189");

        descriptor.GetValue(iban).Should().Be("FR7630006000011234567890189");
        descriptor.Format(iban).Should().Be("FR7630006000011234567890189");
        descriptor.ValueType.Should().Be<string>();
    }
}
