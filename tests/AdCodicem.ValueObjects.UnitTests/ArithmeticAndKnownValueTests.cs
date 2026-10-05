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

public partial class KnownValueTests
{
    [Fact]
    public void The_named_constants_are_exposed_as_static_members()
    {
        CountryCode.France.Value.Should().Be("FR");
        CountryCode.KnownValues.Should().HaveCount(3);
        CountryCode.KnownValues.Should().Contain(CountryCode.Luxembourg);
    }

    /// <summary>
    /// The known values go through <c>Known</c>, and so through the pattern, while the type initializes. The pattern
    /// therefore has to be ready before them, which a source-generated one is: the option it replaced compiled a field,
    /// which, initialized after them, made the type initializer throw inside the module initializer, and the whole
    /// assembly fail to load before any code ran.
    /// </summary>
    [Fact]
    public void A_pattern_is_ready_before_the_named_constants_go_through_it()
    {
        CurrencyCode.Euro.Value.Should().Be("EUR");
        CurrencyCode.KnownValues.Should().Equal(CurrencyCode.Euro, CurrencyCode.UsDollar);
        CurrencyCode.Create(" usd ").Should().Be(CurrencyCode.UsDollar);

        CurrencyCode.TryCreate("EURO", out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    /// <summary>
    /// The generated part of a type initializes after the author's, so the membership lookup of a closed set, built from
    /// the known values, holds the values they were created with rather than their defaults, a property's as a field's.
    /// </summary>
    [Fact]
    public void A_closed_value_set_builds_its_lookup_from_the_known_values_once_they_are_created()
    {
        DocumentStatus.KnownValues.Should().Equal(DocumentStatus.Draft, DocumentStatus.Final);
        DocumentStatus.KnownValues.Select(known => known.Value).Should().Equal("draft", "final");
        DocumentStatus.Create("FINAL").Should().Be(DocumentStatus.Final);
        DocumentStatus.Schema.KnownValues.Should().Equal("draft", "final");
    }

    /// <summary>
    /// A static member of a closed value object created through <c>Create</c> is validated before the lookup of its
    /// known values exists, and is refused with the reason, which the type initializer carries out. Generic, so that
    /// nothing initializes a construction but the test asking for it.
    /// </summary>
    [Fact]
    public void A_closed_value_set_refuses_a_static_member_created_through_Create_before_its_lookup_exists()
    {
        var act = () => EarlyBird<KnownValueTests>.Dawn;

        act.Should().Throw<TypeInitializationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("'EarlyBird' was validated while its type initializes, before the lookup of its known values exists*");
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

    /// <summary>
    /// The schema keeps what the attribute declares beside each value: the name of its property, which a client
    /// generated from the OpenAPI document names its member after, and its description, where one was declared.
    /// </summary>
    [Fact]
    public void The_schema_keeps_the_name_and_the_description_of_each_known_value()
    {
        CountryCode.Schema.KnownValueDetails.Should().Equal(
            new KnownValueInfo("FR", nameof(CountryCode.France), "France"),
            new KnownValueInfo("BE", nameof(CountryCode.Belgium), "Belgium"),
            new KnownValueInfo("LU", nameof(CountryCode.Luxembourg), "Luxembourg"));
        VatRate.Schema.KnownValueDetails.Should().Equal(
            new KnownValueInfo(20.0m, nameof(VatRate.Standard)),
            new KnownValueInfo(5.5m, nameof(VatRate.Reduced), "Food, books and medicine."));
        PageNumber.Schema.KnownValueDetails.Should().ContainSingle()
            .Which.Should().Be(new KnownValueInfo(1, nameof(PageNumber.First), "The first page."));
        DocumentStatus.Schema.KnownValueDetails.Should().Equal(
            new KnownValueInfo("draft", nameof(DocumentStatus.Draft)),
            new KnownValueInfo("final", nameof(DocumentStatus.Final), "Signed off, and no longer edited."));
        Iban.Schema.KnownValueDetails.Should().BeEmpty();
    }

    /// <summary>
    /// The schema holds the underlying value of the example a type declares, read through the hook however the type
    /// implements it, explicitly included.
    /// </summary>
    [Fact]
    public void The_schema_holds_the_underlying_value_of_the_declared_example()
    {
        Iban.Schema.Example.Should().Be("FR7630006000011234567890189");
        Amount.Schema.Example.Should().Be(1250.00m);
        Consent.Schema.Example.Should().Be(true);
        Duration.Schema.Example.Should().Be(new TimeSpan(1, 30, 0));
        CountryCode.Schema.Example.Should().BeNull("it declares no example");
    }

    /// <summary>
    /// A known value is a value with a name, which a schema written by hand states as the generator does.
    /// </summary>
    [Fact]
    public void A_known_value_needs_a_value_and_a_name()
    {
        var create = static (object? value, string? name) => new KnownValueInfo(value!, name!);

        create.Invoking(build => build(null, "France")).Should().Throw<ArgumentNullException>().WithParameterName("value");
        create.Invoking(build => build("FR", null)).Should().Throw<ArgumentNullException>().WithParameterName("name");
        create.Invoking(build => build("FR", " ")).Should().Throw<ArgumentException>().WithParameterName("name");
        new KnownValueInfo("FR", "France").Description.Should().BeNull();
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

    /// <summary>A closed set one of whose static members is created through <c>Create</c> while the type initializes.</summary>
    /// <typeparam name="TOwner">The owner of the value.</typeparam>
    [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
    private readonly partial struct EarlyBird<TOwner>
    {
        [KnownValue]
        public static readonly EarlyBird<TOwner> Dawn = Known("dawn");

        public static readonly EarlyBird<TOwner> Default = Create("dawn");
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
