using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests;

public class ArithmeticTests
{
    [Fact]
    public void Addition_and_subtraction_stay_inside_the_type()
    {
        var total = Amount.Create(10.50m) + Amount.Create(4.50m);

        total.Should().Be(Amount.Create(15m));
        (total - Amount.Create(5m)).Should().Be(Amount.Create(10m));
    }

    [Fact]
    public void An_operation_that_leaves_the_valid_range_throws_instead_of_escaping_the_type()
    {
        var act = () => Amount.Create(10m) - Amount.Create(20m);

        act.Should().Throw<ValueObjectException>()
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void Scaling_by_a_scalar_works_in_both_directions()
    {
        (Amount.Create(10m) * 3m).Should().Be(Amount.Create(30m));
        (3m * Amount.Create(10m)).Should().Be(Amount.Create(30m));
        (Amount.Create(30m) / 3m).Should().Be(Amount.Create(10m));
    }

    [Fact]
    public void Dividing_two_amounts_yields_a_bare_ratio_rather_than_an_amount()
    {
        var ratio = Amount.Create(30m) / Amount.Create(4m);

        ratio.Should().Be(7.5m);
    }

    [Fact]
    public void A_narrow_integer_survives_the_promotion_to_int()
    {
        var total = Quantity.Create(300) + Quantity.Create(400);

        total.Value.Should().Be((short)700);
    }

    [Fact]
    public void The_generic_math_helpers_are_available_through_the_interface()
    {
        Amount.Sum([Amount.Create(1m), Amount.Create(2m), Amount.Create(3m)]).Should().Be(Amount.Create(6m));

        Amount.Zero.Value.Should().Be(0m);
        Amount.Max(Amount.Create(1m), Amount.Create(2m)).Should().Be(Amount.Create(2m));
        Amount.Create(0m).IsZero.Should().BeTrue();
    }

    [Fact]
    public void A_sum_is_validated_once_rather_than_at_every_step()
    {
        // Intermediate totals never leave the underlying type, so an ordering that dips below zero is fine.
        var values = new[] { Percentage.Create(60m), Percentage.Create(60m) };

        var act = () => Percentage.Sum(values);

        act.Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void A_domain_operation_can_cross_two_value_object_types()
    {
        Percentage.Create(20m).Of(Amount.Create(150m)).Should().Be(Amount.Create(30m));
    }
}

public class KnownValueTests
{
    [Fact]
    public void The_named_constants_are_exposed_as_static_members()
    {
        CountryCode.France.Value.Should().Be("FR");
        CountryCode.KnownValues.Should().HaveCount(3);
        CountryCode.KnownValues.Should().Contain(CountryCode.Luxembourg);
    }

    [Fact]
    public void A_closed_value_set_rejects_anything_it_does_not_declare()
    {
        CountryCode.TryCreate("ES", out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.NotAKnownValue);
    }

    [Fact]
    public void A_closed_value_set_still_normalizes_its_input()
    {
        CountryCode.Create(" be ").Should().Be(CountryCode.Belgium);
    }

    [Fact]
    public void The_schema_publishes_the_declared_values()
    {
        CountryCode.Schema.IsClosedValueSet.Should().BeTrue();
        CountryCode.Schema.KnownValues.Should().Equal("FR", "BE", "LU");
    }

    [Fact]
    public void The_schema_carries_the_declarative_rules_once_for_every_consumer()
    {
        Iban.Schema.MinLength.Should().Be(15);
        Iban.Schema.MaxLength.Should().Be(34);
        Iban.Schema.Format.Should().Be("iban");
        Iban.Schema.Example.Should().Be("FR7630006000011234567890189");
        Iban.Schema.Pattern.Should().NotBeNullOrEmpty();

        Amount.Schema.Minimum.Should().Be("0");
        Percentage.Schema.Maximum.Should().Be("100");
    }

    [Fact]
    public void The_schema_description_falls_back_to_the_XML_summary()
    {
        Iban.Schema.Description.Should().Be("An International Bank Account Number, stored in its electronic form.");
    }
}

public class RegistryTests
{
    [Fact]
    public void Every_value_object_of_the_assembly_registers_itself()
    {
        ValueObjectRegistry.TryGet(typeof(Iban), out var descriptor).Should().BeTrue();

        descriptor!.ValueObjectType.Should().Be<Iban>();
        descriptor.ValueType.Should().Be<string>();
        descriptor.Schema.MaxLength.Should().Be(34);
    }

    [Fact]
    public void A_nullable_value_object_resolves_to_the_same_descriptor()
    {
        ValueObjectRegistry.TryGet(typeof(Iban?), out var descriptor).Should().BeTrue();
        descriptor!.ValueObjectType.Should().Be<Iban>();
    }

    [Fact]
    public void The_descriptor_can_build_and_read_a_value_without_knowing_its_type()
    {
        ValueObjectRegistry.TryGet(typeof(Amount), out var descriptor);

        var boxed = descriptor!.Create(12.345m);

        descriptor.GetValue(boxed).Should().Be(12.34m);
        descriptor.Format(boxed).Should().Be("12.34");
    }

    [Fact]
    public void The_descriptor_reports_a_rejected_value_without_throwing()
    {
        ValueObjectRegistry.TryGet(typeof(Amount), out var descriptor);

        descriptor!.TryCreate(-1m, out var result, out var validation).Should().BeFalse();
        result.Should().BeNull();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void The_descriptor_rejects_a_value_of_the_wrong_type()
    {
        ValueObjectRegistry.TryGet(typeof(Amount), out var descriptor);

        descriptor!.TryCreate("nonsense", out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public void A_type_that_is_not_a_value_object_is_reported_as_such()
    {
        ValueObjectRegistry.IsValueObject(typeof(string)).Should().BeFalse();
        ValueObjectRegistry.IsValueObject(typeof(Iban)).Should().BeTrue();
        ValueObjectRegistry.GetUnderlyingType(typeof(Iban)).Should().Be<string>();
        ValueObjectRegistry.GetUnderlyingType(typeof(Guid)).Should().BeNull();
    }
}
