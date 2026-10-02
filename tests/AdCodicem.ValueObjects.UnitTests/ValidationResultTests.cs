namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The value semantics of a rejection, which callers compare, hash and log.
/// </summary>
public class ValidationResultTests
{
    [Fact]
    public void A_result_equals_another_only_when_code_and_message_both_match()
    {
        // Two values below the same minimum break the same rule with the same message; a value above another
        // bound breaks a rule of the same code with another message; a closed set reports another code.
        var belowZero = Amount.Validate(-1m);
        var alsoBelowZero = Amount.Validate(-2m);
        var aboveHundred = Percentage.Validate(101m);
        var unknown = CountryCode.Validate("ZZ");

        (belowZero == alsoBelowZero).Should().BeTrue();
        belowZero.GetHashCode().Should().Be(alsoBelowZero.GetHashCode());
        belowZero.Equals((object)alsoBelowZero).Should().BeTrue();

        aboveHundred.ErrorCode.Should().Be(belowZero.ErrorCode);
        (belowZero != aboveHundred).Should().BeTrue();
        (belowZero != unknown).Should().BeTrue();
        belowZero.Equals("value_object.out_of_range").Should().BeFalse();
    }

    [Fact]
    public void A_result_reads_as_its_code_and_message()
    {
        ValidationResult.Success.ToString().Should().Be("Valid");
        Amount.Validate(-1m).ToString().Should().Be("value_object.out_of_range: The value must be greater than or equal to 0.");
    }

    [Fact]
    public void Throwing_on_a_successful_result_does_nothing()
    {
        var act = () => ValidationResult.Success.ThrowIfInvalid(typeof(Amount), 1m);

        act.Should().NotThrow();
    }
}

/// <summary>
/// The constructors every exception type is expected to offer, which carry no rule of their own.
/// </summary>
public class ValueObjectExceptionTests
{
    [Fact]
    public void The_standard_constructors_carry_a_message_and_a_cause_but_no_rule()
    {
        var cause = new InvalidOperationException("cause");

        var bare = new ValueObjectException();
        var described = new ValueObjectException("described");
        var wrapped = new ValueObjectException("wrapped", cause);

        bare.Message.Should().NotBeNullOrEmpty();
        described.Message.Should().Be("described");
        wrapped.Message.Should().Be("wrapped");
        wrapped.InnerException.Should().BeSameAs(cause);

        foreach (var exception in new[] { bare, described, wrapped })
        {
            exception.ValueObjectType.Should().BeNull();
            exception.ErrorCode.Should().BeNull();
            exception.AttemptedValue.Should().BeNull();
        }
    }
}
