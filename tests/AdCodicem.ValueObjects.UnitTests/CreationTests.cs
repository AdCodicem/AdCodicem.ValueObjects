namespace AdCodicem.ValueObjects.UnitTests;

public class CreationTests
{
    [Theory]
    [InlineData("FR7630006000011234567890189")]
    [InlineData("fr76 3000 6000 0112 3456 7890 189")]
    [InlineData("FR76-3000-6000-0112-3456-7890-189")]
    [InlineData("  DE89370400440532013000  ")]
    public void Create_normalizes_before_validating(string input)
    {
        var iban = Iban.Create(input);

        iban.Value.Should().NotContain(" ").And.NotContain("-");
        iban.Value.Should().Be(iban.Value.ToUpperInvariant());
    }

    [Fact]
    public void Create_produces_the_same_value_for_every_spelling_of_it()
    {
        var electronic = Iban.Create("FR7630006000011234567890189");
        var printed = Iban.Create("fr76 3000 6000 0112 3456 7890 189");

        printed.Should().Be(electronic);
        printed.GetHashCode().Should().Be(electronic.GetHashCode());
    }

    [Fact]
    public void Create_throws_with_the_violated_rule_attached()
    {
        var act = () => Iban.Create("FR7630006000011234567890188");

        act.Should().Throw<ValueObjectException>()
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    [Fact]
    public void Create_reports_the_value_it_rejected()
    {
        var act = () => Amount.Create(-1m);

        var exception = act.Should().Throw<ValueObjectException>().Which;
        exception.AttemptedValue.Should().Be(-1m);
        exception.ValueObjectType.Should().Be<Amount>();
        exception.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Theory]
    [InlineData(null, ValueObjectErrorCodes.Required)]
    [InlineData("", ValueObjectErrorCodes.Required)]
    [InlineData("FR76", ValueObjectErrorCodes.TooShort)]
    [InlineData("FR7630006000011234567890189000000000000", ValueObjectErrorCodes.TooLong)]
    [InlineData("76FR30006000011234567890189", ValueObjectErrorCodes.InvalidFormat)]
    public void TryCreate_reports_the_first_violated_rule(string? input, string expectedErrorCode)
    {
        var created = Iban.TryCreate(input!, out var iban, out var validation);

        created.Should().BeFalse();
#pragma warning disable VO0010 // Asserting on the uninitialized value is the point of this test.
        iban.Should().Be(default(Iban));
#pragma warning restore VO0010
        validation.IsValid.Should().BeFalse();
        validation.ErrorCode.Should().Be(expectedErrorCode);
        validation.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryCreate_never_throws_on_a_rejected_value()
    {
        var act = () => Iban.TryCreate("nonsense", out _);

        act.Should().NotThrow();
        Iban.TryCreate("nonsense", out _).Should().BeFalse();
    }

    [Fact]
    public void Validate_is_reachable_without_building_a_value()
    {
        Amount.Validate(12.34m).IsValid.Should().BeTrue();
        Amount.Validate(-0.01m).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Success_carries_no_error()
    {
        var result = ValidationResult.Success;

        result.IsValid.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.Should().Be(default(ValidationResult));
    }

    [Fact]
    public void CreateUnchecked_trusts_the_caller()
    {
        // The trusted path exists for values the application itself wrote, such as rows it read back.
        var iban = Iban.CreateUnchecked("not an iban at all");

        iban.Value.Should().Be("not an iban at all");
    }

    [Fact]
    public void Normalization_is_idempotent()
    {
        var once = Iban.Normalize("fr76 3000 6000 0112 3456 7890 189");
        var twice = Iban.Normalize(once);

        twice.Should().Be(once);
    }

    [Fact]
    public void An_empty_value_is_rejected_by_default()
    {
        EmailAddress.TryCreate(string.Empty, out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
    }

    [Fact]
    public void A_custom_rule_runs_after_the_declarative_ones()
    {
        CustomerId.TryCreate(Guid.Empty, out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);

        CustomerId.TryCreate(Guid.CreateVersion7(), out _).Should().BeTrue();
    }

    [Fact]
    public void A_decimal_value_object_normalizes_its_own_precision()
    {
        Amount.Create(10.005m).Value.Should().Be(10.00m);
        Amount.Create(10.015m).Value.Should().Be(10.02m);
    }

    // VO0010 is exactly what stops the expressions below from reaching real code. They are written here to pin
    // down what happens if someone disables it, or reaches a default across an assembly the analyzer cannot see.
#pragma warning disable VO0010
    [Fact]
    public void IsDefault_flags_an_instance_that_never_went_through_validation()
    {
        default(Iban).IsDefault.Should().BeTrue();
        Iban.Create("DE89370400440532013000").IsDefault.Should().BeFalse();
    }

    [Fact]
    public void The_default_instance_exposes_an_empty_value_rather_than_null()
    {
        // A default must never surface as a NullReferenceException.
        default(Iban).Value.Should().BeEmpty();
        default(Iban).ToString().Should().BeEmpty();
    }

    [Fact]
    public void The_analyzer_is_what_keeps_a_default_out_of_real_code()
    {
        // Kept as documentation: every expression in this region is a build error without the pragma above.
        var uninitialized = default(Iban);

        uninitialized.IsDefault.Should().BeTrue();
    }
#pragma warning restore VO0010
}
