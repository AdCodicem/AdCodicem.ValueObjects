using AdCodicem.ValueObjects.FluentValidation;
using FluentValidation;
using FluentValidation.Results;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The options FluentValidation reads from <see cref="ValidatorOptions.Global"/>, which a failure of
/// <see cref="ValueObjectRuleBuilderExtensions.MustParseAs{T}"/> honours as any other failure does. They are global, so
/// these tests run alone.
/// </summary>
[Collection(nameof(FluentValidationGlobalOptions))]
public sealed class FluentValidationGlobalOptionsTests
{
    [Fact]
    public void A_failure_takes_the_global_severity_unless_one_is_chained()
    {
        var unchained = new InlineValidator<FluentValidationTests.ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };
        var chained = new InlineValidator<FluentValidationTests.ImportCommand>
        {
            v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)).WithSeverity(Severity.Error),
        };
        var command = new FluentValidationTests.ImportCommand("FR7630006000011234567890188");
        var original = ValidatorOptions.Global.Severity;

        try
        {
            ValidatorOptions.Global.Severity = Severity.Warning;

            unchained.Validate(command).Errors.Should().ContainSingle().Which.Severity.Should().Be(Severity.Warning);
            chained.Validate(command).Errors.Should().ContainSingle().Which.Severity.Should().Be(Severity.Error);
        }
        finally
        {
            ValidatorOptions.Global.Severity = original;
        }
    }

    [Fact]
    public void A_failure_goes_through_the_global_callback_on_its_creation()
    {
        var validator = new InlineValidator<FluentValidationTests.ImportCommand> { v => v.RuleFor(x => x.Iban).MustParseAs(typeof(Iban)) };
        var original = ValidatorOptions.Global.OnFailureCreated;

        try
        {
            ValidatorOptions.Global.OnFailureCreated = (failure, _, value, _, _) =>
                new ValidationFailure(failure.PropertyName, $"[{value}] {failure.ErrorMessage}") { ErrorCode = failure.ErrorCode };

            var failure = validator.Validate(new FluentValidationTests.ImportCommand("FR7630006000011234567890188")).Errors
                .Should().ContainSingle().Which;

            failure.ErrorMessage.Should().Be("[FR7630006000011234567890188] The IBAN check digits are incorrect.");
            failure.ErrorCode.Should().Be(ValueObjectErrorCodes.InvalidFormat);
        }
        finally
        {
            ValidatorOptions.Global.OnFailureCreated = original;
        }
    }
}

/// <summary>Runs the tests that change <see cref="ValidatorOptions.Global"/> alone, so that no other test sees them.</summary>
[CollectionDefinition(nameof(FluentValidationGlobalOptions), DisableParallelization = true)]
public sealed class FluentValidationGlobalOptions;
